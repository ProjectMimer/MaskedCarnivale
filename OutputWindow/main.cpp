#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <iostream>
#include "stDX11.h"
#include "sharedMemory.h"
#include "cShaderData.h"
#include "cRenderObject.h"
#include "stBasicTexture.h"

struct OutputWindowSetup
{
    byte isOutputActive;
    byte isGameActive;
    bool updateWindow;
    bool resetSharedHandle;
    bool doClose;
    byte topmost;
    byte newTopmost;
    bool t7;
    HANDLE sharedHandle;
    int top;
    int left;
    int width;
    int height;
    int newTop;
    int newLeft;
    int newWidth;
    int newHeight;
    

    OutputWindowSetup()
    {
        Reset();
    }

    void Reset()
    {
        isOutputActive = 0;
        isGameActive = 0;
        updateWindow = false;
        resetSharedHandle = false;
        doClose = false;
        sharedHandle = 0;
        top = 0;
        left = 0;
        width = 100;
        height = 100;
        topmost = 0;
        newTop = 0;
        newLeft = 0;
        newWidth = 0;
        newHeight = 0;
        newTopmost = 0;

        t7 = false;
    }
};


HWND hWnd = 0;
HINSTANCE hInstance = 0;
short outputClassId = 0;

std::wstring windowName = L"MaskedCarnivale";
std::wstring windowClass = L"OutputWindow";

stDX11 dx11 = stDX11();
ID3D11SamplerState* pSampleState = nullptr;
ShaderData psTexture = ps_Texture();
ShaderData vsTexture = vs_Texture();
RenderObject orthogSquare;

stBasicTexture mainRenderTarget = stBasicTexture();
IDXGIKeyedMutex* SharedBackBufferMutex = nullptr;

HANDLE hMapFile;
OutputWindowSetup* outputWindowData = new OutputWindowSetup();

bool RegisterOutputClass(HINSTANCE hInstance, std::wstring windClass);
bool CreateOutputWindow(HINSTANCE hInstance, std::wstring wndClass, std::wstring wndName, bool show, int width, int height);
WPARAM MessageLoop();
LRESULT CALLBACK WinProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam);
void DoRender();
bool ResizeMoveWindow();

bool CreateShaders();
void DestroyShaders();
bool CreateBuffers();
void DestroyBuffers();
bool CreateTextures();
void DestroyTextures();
bool CreateTextureShared(int width, int height);
void DestroyTextureShared();

int WINAPI WinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ LPSTR lpCmdLine, _In_ int nShowCmd)
{
    int shareMemType = OpenSharedMemory(&hMapFile, (unsigned char**)&outputWindowData, sizeof(OutputWindowSetup), "DebugTextureOutputWindow");
    if (!shareMemType)
        return false;
    if(outputWindowData->width == 0)
        outputWindowData->Reset();

    if (!CreateOutputWindow(hInstance, windowClass, windowName, true, outputWindowData->width, outputWindowData->height))
        return false;
    if (!dx11.createDevice())
        return false;
    if (!dx11.createSwapchain(hWnd, outputWindowData->width, outputWindowData->height))
        return false;
    if (!dx11.createBackBuffer())
        return false;
    if (!CreateShaders())
        return false;
    if (!CreateBuffers())
        return false;
    if (!CreateTextures())
        return false;

    outputWindowData->isOutputActive = shareMemType;
    MessageLoop();
    outputWindowData->isOutputActive = 0;
    
    DestroyTextures();
    DestroyBuffers();
    DestroyShaders();
    dx11.Release();

    outputWindowData->Reset();
    CloseSharedMemory(&hMapFile, (unsigned char**)&outputWindowData);

    return true;
}

bool RegisterOutputClass(HINSTANCE hInstance, std::wstring wndClass)
{
    if (outputClassId == 0)
    {
        WNDCLASSEX wc;
        ZeroMemory(&wc, sizeof(WNDCLASSEX));
        wc.cbSize = sizeof(WNDCLASSEX);
        wc.style = CS_HREDRAW | CS_VREDRAW;
        wc.lpfnWndProc = WinProc;
        wc.cbClsExtra = NULL;
        wc.cbWndExtra = NULL;
        wc.hInstance = hInstance;
        wc.hIcon = LoadIcon(NULL, IDI_APPLICATION);
        wc.hCursor = LoadCursor(NULL, IDC_ARROW);
        wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 2);
        wc.lpszMenuName = NULL;
        wc.lpszClassName = wndClass.c_str();
        wc.hIconSm = LoadIcon(NULL, IDI_APPLICATION);

        outputClassId = RegisterClassEx(&wc);
        if (outputClassId == 0)
        {
            MessageBox(NULL, L"Error registering class", L"Error", MB_OK | MB_ICONERROR);
            return false;
        }
    }
    return true;
}

bool CreateOutputWindow(HINSTANCE hInstance, std::wstring wndClass, std::wstring wndName, bool show, int width, int height)
{
    if (!RegisterOutputClass(hInstance, wndClass))
        return false;

    RECT wr = { 0, 0, width, height };
    AdjustWindowRect(&wr, WS_OVERLAPPEDWINDOW, FALSE);

    hWnd = CreateWindowEx(NULL,
        wndClass.c_str(),
        wndName.c_str(),
        WS_OVERLAPPEDWINDOW,
        wr.left,
        wr.top,
        wr.right - wr.left,
        wr.bottom - wr.top,
        NULL,
        NULL,
        hInstance,
        NULL);

    if (!hWnd)
    {
        MessageBox(NULL, L"Error creating window", L"Error", MB_OK | MB_ICONERROR);
        return false;
    }

    ShowWindow(hWnd, show);
    UpdateWindow(hWnd);

    return true;
}

//#include <sstream>

WPARAM MessageLoop()
{
    LARGE_INTEGER StartingTime, EndingTime;
    LARGE_INTEGER Frequency;
    QueryPerformanceFrequency(&Frequency);
    QueryPerformanceCounter(&StartingTime);

    double reqFrameRate = 60;
    double frameTiming = 1000 / reqFrameRate;
    double accumulator = 0;
    double PCFreq = double(Frequency.QuadPart) / 1000.0;

    //std::wstringstream ws;
    //unsigned int frames = 0;

    MSG msg;
    ZeroMemory(&msg, sizeof(MSG));
    while (TRUE)
    {
        if (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE))
        {
            if (msg.message == WM_QUIT)
                break;

            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
        else
        {
            if (outputWindowData->doClose)
                break;

            if (outputWindowData->updateWindow)
                if (!ResizeMoveWindow())
                    break;

            QueryPerformanceCounter(&EndingTime);
            double workTime = double(EndingTime.QuadPart - StartingTime.QuadPart) / PCFreq;
            accumulator += workTime;
            if (workTime < frameTiming)
            {
                double waitTime = frameTiming - workTime;
                Sleep(waitTime);
                accumulator += waitTime;
            }
            QueryPerformanceCounter(&StartingTime);
            double sleepTime = double(StartingTime.QuadPart - EndingTime.QuadPart) / PCFreq;

            DoRender();

            /*frames++;
            if (accumulator > 1000)
            {
                ws << "FrameTime was: " << workTime << " : " << sleepTime << " : " << frames << " : " << accumulator << " : " << PCFreq << "/s" << std::endl;
                OutputDebugString(ws.str().c_str());

                frames = 0;
                accumulator -= 1000;
            }*/
        }
    }

    return msg.wParam;
}

LRESULT CALLBACK WinProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_KEYDOWN:
        if (wParam == VK_ESCAPE) {
            DestroyWindow(hwnd);
        }
        //else if (wParam == 'W')
        //{
        //    posZ += 1;
        //}
        //else if (wParam == 'S')
        //{
        //    posZ -= 1;
        //}
        return 0;

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProc(hwnd, msg, wParam, lParam);
}

void DoRender()
{
    float bgColor[4] = { (0.0f, 1.0f, 0.0f, 1.0f) };

    HRESULT result = S_OK;
    UINT acqKey = 1;
    UINT relKey = 0;
    DWORD timeOut = 5;

    D3D11_VIEWPORT viewport;
    ZeroMemory(&viewport, sizeof(D3D11_VIEWPORT));
    viewport.TopLeftX = 0;
    viewport.TopLeftY = 0;
    viewport.Width = (float)outputWindowData->width;
    viewport.Height = (float)outputWindowData->height;
    viewport.MinDepth = 0.0f;
    viewport.MaxDepth = 1.0f;

    //result = SharedBackBufferMutex->AcquireSync(acqKey, timeOut);
    //if (result == WAIT_OBJECT_0)
    {
        dx11.devcon->ClearRenderTargetView(dx11.backbuffer, bgColor);
        dx11.devcon->OMSetRenderTargets(1, &dx11.backbuffer, NULL);
        dx11.devcon->RSSetViewports(1, &viewport);

        dx11.devcon->PSSetSamplers(0, 1, &pSampleState);
        dx11.devcon->PSSetShaderResources(0, 1, &mainRenderTarget.pShaderResource);
        dx11.devcon->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        orthogSquare.Render();

        dx11.swapchain->Present(0, 0);
    }
    //result = SharedBackBufferMutex->ReleaseSync(relKey);
}

bool ResizeMoveWindow()
{
    if (hWnd == 0)
        return false;

    RECT clientRect = RECT();
    clientRect.top = outputWindowData->newTop;
    clientRect.left = outputWindowData->newLeft;
    clientRect.right = outputWindowData->newWidth;
    clientRect.bottom = outputWindowData->newHeight;
    
    RECT rcClient, rcWind;
    POINT diff;
    GetClientRect(hWnd, &rcClient);
    GetWindowRect(hWnd, &rcWind);
    diff.x = (rcWind.right - rcWind.left) - rcClient.right;
    diff.y = (rcWind.bottom - rcWind.top) - rcClient.bottom;

    HWND wndPosition = HWND_NOTOPMOST;
    if (outputWindowData->newTopmost == 1)
        wndPosition = HWND_BOTTOM;
    else if (outputWindowData->newTopmost == 2)
        wndPosition = HWND_TOPMOST;

    AdjustWindowRect(&clientRect, GetWindowLongA(hWnd, GWL_STYLE), false);
    SetWindowPos(hWnd, wndPosition, clientRect.left, clientRect.top, outputWindowData->newWidth + diff.x, outputWindowData->newHeight + diff.y, SWP_NOACTIVATE | SWP_FRAMECHANGED);
    SendMessageA(hWnd, WM_EXITSIZEMOVE, WPARAM(0), LPARAM(0));

    if (outputWindowData->width != outputWindowData->newWidth || outputWindowData->height != outputWindowData->newHeight)
    {
        ID3D11RenderTargetView* nullRTV = nullptr;
        dx11.devcon->OMSetRenderTargets(1, &nullRTV, NULL);
        dx11.destroyBackBuffer();
        dx11.devcon->Flush();
        if (!dx11.resizeSwapchain(outputWindowData->newWidth, outputWindowData->newWidth))
            return false;
        if (!dx11.createBackBuffer())
            return false;

        DestroyTextureShared();
        CreateTextureShared(outputWindowData->newWidth, outputWindowData->newHeight);

        outputWindowData->resetSharedHandle = true;
    }

    outputWindowData->updateWindow = false;
    outputWindowData->top = outputWindowData->newTop;
    outputWindowData->left = outputWindowData->newLeft;
    outputWindowData->width = outputWindowData->newWidth;
    outputWindowData->height = outputWindowData->newHeight;
    outputWindowData->topmost = outputWindowData->newTopmost;

    return true;
}

bool CreateShaders()
{
    vsTexture.CompileShaderFromString(dx11.dev);
    psTexture.CompileShaderFromString(dx11.dev);

    return true;
}

void DestroyShaders()
{
    vsTexture.Release();
    psTexture.Release();
}

bool CreateBuffers()
{
    orthogSquare = RenderSquare(dx11.dev, dx11.devcon);
    orthogSquare.SetShadersLayout(vsTexture.Layout, vsTexture.VS, psTexture.PS);

    //result = SharedBackBuffer.pTexture->QueryInterface(__uuidof(IDXGIKeyedMutex), (LPVOID*)&SharedBackBufferMutex);
    //if(FAILED(result) || (SharedBackBufferMutex == NULL))
    //    return false;

    return true;
}

void DestroyBuffers()
{
    orthogSquare.Release();
}

bool CreateTextures()
{
    // Create the texture sampler state.
    D3D11_SAMPLER_DESC samplerDesc;
    ZeroMemory(&samplerDesc, sizeof(D3D11_SAMPLER_DESC));
    samplerDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
    samplerDesc.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
    samplerDesc.MipLODBias = 0.0f;
    samplerDesc.MaxAnisotropy = 1;
    samplerDesc.ComparisonFunc = D3D11_COMPARISON_ALWAYS;
    samplerDesc.BorderColor[0] = 0;
    samplerDesc.BorderColor[1] = 0;
    samplerDesc.BorderColor[2] = 0;
    samplerDesc.BorderColor[3] = 0;
    samplerDesc.MinLOD = 0;
    samplerDesc.MaxLOD = D3D11_FLOAT32_MAX;
    HRESULT result = dx11.dev->CreateSamplerState(&samplerDesc, &pSampleState);
    if (FAILED(result)) {
        MessageBoxA(0, "Error creating sampler state", "Error", MB_OK);
        return false;
    }
        
    if (!CreateTextureShared(outputWindowData->width, outputWindowData->height))
        return false;

    return true;
}

void DestroyTextures()
{
    DestroyTextureShared();
    if (pSampleState) { pSampleState->Release(); pSampleState = nullptr; }

    byte gA = outputWindowData->isGameActive;
    if(gA == 0)
        outputWindowData->Reset();
    outputWindowData->isGameActive = gA;
}

bool CreateTextureShared(int width, int height)
{
    mainRenderTarget.SetWidthHeight(width, height);
    mainRenderTarget.textureDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    //mainRenderTarget.textureDesc.MiscFlags = D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX;

    if (!mainRenderTarget.Create(dx11.dev, true, true, true))
    {
        MessageBox(NULL, L"Error creating Texture", L"Error", MB_OK | MB_ICONERROR);
        return false;
    }
    if (mainRenderTarget.pTexture == nullptr)
    {
        MessageBox(NULL, L"Error creating SharedBackBuffer ", L"Error", MB_OK | MB_ICONERROR);
        return false;
    }
    if (mainRenderTarget.pRenderTarget == nullptr)
    {
        MessageBox(NULL, L"Error creating SharedBackBufferRTV ", L"Error", MB_OK | MB_ICONERROR);
        return false;
    }
    if (mainRenderTarget.pShaderResource == nullptr)
    {
        MessageBox(NULL, L"Error creating SharedBackBufferSRV ", L"Error", MB_OK | MB_ICONERROR);
        return false;
    }
    if (mainRenderTarget.pSharedHandle == nullptr)
    {
        MessageBox(NULL, L"Error creating shared handle ", L"Error", MB_OK | MB_ICONERROR);
        return false;
    }
    outputWindowData->sharedHandle = mainRenderTarget.pSharedHandle;
    return true;
}

void DestroyTextureShared()
{
    mainRenderTarget.Release();
    outputWindowData->sharedHandle = 0;
}
