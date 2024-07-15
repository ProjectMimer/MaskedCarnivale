using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Timers;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Dalamud.Utility.Signatures;
using Dalamud.Hooking;

using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;
using KeyedMutex = SharpDX.DXGI.KeyedMutex;
using SwapChain11 = SharpDX.DXGI.SwapChain;
using Texture2D = SharpDX.Direct3D11.Texture2D;
using Device11 = SharpDX.Direct3D11.Device5;
using DeviceContext11 = SharpDX.Direct3D11.DeviceContext4;

using MaskedCarnivale.Windows;
using MaskedCarnivale.Structures;
using MaskedCarnivale.DirectX.ShaderData;
using MaskedCarnivale.DirectX.RenderObjects;

using MemoryManager.Structures;

namespace MaskedCarnivale;

public unsafe class Plugin : IDalamudPlugin
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static IFramework? Framework { get; private set; } = null!;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IPluginLog? Log { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider Interop { get; private set; } = null!;

    public string Name => "MaskedCarnivale";
    private const string CommandName = "/carnivale";

    public Configuration cfg { get; init; }

    public readonly WindowSystem WindowSystem = new("MaskedCarnivale");
    private ConfigWindow ConfigWindow { get; init; }

    private HookManager hookManager = new HookManager();
    private Texture2D? sharedTexture { get; set; } = null!;
    private KeyedMutex sharedMutex { get; set; } = null!;
    private RenderTargetView? sharedRTV { get; set; } = null!;
    private ShaderResourceView? sharedSRV { get; set; } = null!;
    private ShaderResourceView? selectedSRV { get; set; } = null!;
    private int oldRenderIndex = -1;

    private RenderTargetManager* renderTargetManager = RenderTargetManager.Instance();

    private SamplerState? pSamplerState { get; set; } = null;
    private ShaderData psTexture = new ps_Texture();
    private ShaderData vsTexture = new vs_Texture();
    private RenderObject? orthogSquare { get; set; } = null;
    private bool isEnabled = false;
    private int shareMemType = 0;
    private int gameWindowWithUI = 107;
    private int gameWindowWithoutUI = 71;

    [StructLayout(LayoutKind.Sequential)]
    struct OutputWindowSetup
    {
        [MarshalAs(UnmanagedType.I1)]
        public byte isOutputActive;
        [MarshalAs(UnmanagedType.I1)]
        public byte isGameActive;
        [MarshalAs(UnmanagedType.I1)]
        public bool updateWindow;
        [MarshalAs(UnmanagedType.I1)]
        public bool resetSharedHandle;
        [MarshalAs(UnmanagedType.I1)]
        public bool doClose;
        [MarshalAs(UnmanagedType.I1)]
        public byte topmost;
        [MarshalAs(UnmanagedType.I1)]
        public byte newTopmost;
        [MarshalAs(UnmanagedType.I1)]
        public bool t7;
        public IntPtr sharedHandle;
        public int top;
        public int left;
        public int width;
        public int height;
        public int newTop;
        public int newLeft;
        public int newWidth;
        public int newHeight;
        

        public OutputWindowSetup()
        {
            Reset();
        }
        public void Reset()
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
    }
    private SharedMemoryManager smm = new SharedMemoryManager();
    private int sharedBufferSize = 1024;
    private OutputWindowSetup* outputWindowData = null;

    public unsafe Plugin()
    {
        cfg = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        cfg.enable = false;
        cfg.doUpdate = false;
        cfg.renderIndex = gameWindowWithUI;

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.RemoveHandler(CommandName);
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "[enable|disable]"
        });

        Framework!.Update += Update;
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUI;

        Initialize();
    }


    public void Dispose()
    {
        Destroy();

        Framework!.Update -= Update;
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUI;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            ToggleConfigUI();
            return;
        }
        var regex = Regex.Match(argument, "^(\\w+) ?(.*)");
        var subcommand = regex.Success && regex.Groups.Count > 1 ? regex.Groups[1].Value : string.Empty;

        switch (subcommand.ToLower())
        {
            case "enable":
                {
                    Enable();
                    break;
                }
            case "disable":
                {
                    Disable();
                    break;
                }
        }
    }

    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();

    private void Update(IFramework framework)
    {
        if (!isEnabled && cfg.enable && outputWindowData != null && outputWindowData->isOutputActive == 0)
        {
            isEnabled = cfg.enable;
            Enable();
        }
        else if (isEnabled && !cfg.enable && outputWindowData != null && outputWindowData->isOutputActive != 0)
        {
            isEnabled = cfg.enable;
            Disable();
        }
        else if (!isEnabled && !cfg.enable && outputWindowData != null && outputWindowData->isOutputActive != 0)
        {
            isEnabled = true;
            cfg.enable = true;
        }
        else if (isEnabled && cfg.enable && outputWindowData != null && outputWindowData->isOutputActive == 0)
        {
            isEnabled = false;
            cfg.enable = false;
        }

        if (cfg.doUpdate && outputWindowData != null)
        {
            cfg.doUpdate = false;
            FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device* ffxivDevice = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
            SwapChain11 swapChain11 = (SwapChain11)(IntPtr)ffxivDevice->SwapChain->DXGISwapChain;

            outputWindowData->isGameActive = (byte)shareMemType;
            outputWindowData->newTop = cfg.yPosition;
            outputWindowData->newLeft = cfg.xPosition;
            outputWindowData->newWidth = (int)ffxivDevice->SwapChain->Width;
            outputWindowData->newHeight = (int)ffxivDevice->SwapChain->Height;
            outputWindowData->newTopmost = (byte)cfg.orderStatus;
            outputWindowData->updateWindow = true;
        }
    }

    private static class Signatures
    {
        internal const string DXGIPresent = "E8 ?? ?? ?? ?? C6 43 79 00";
    }

    private void Initialize()
    {
        Interop.InitializeFromAttributes(this);

        hookManager.SetFunctionHandles(this);
        hookManager.EnableFunctionHandles();


        shareMemType = smm.OpenSharedMemory(sharedBufferSize, "DebugTextureOutputWindow");
        if (shareMemType == 0)
        {
            *outputWindowData = new OutputWindowSetup();
        }
        else
        {
            byte* ptrAddress = null;
            smm.mmvAccessor!.SafeMemoryMappedViewHandle.AcquirePointer(ref ptrAddress);
            outputWindowData = (OutputWindowSetup*)ptrAddress;
            if (outputWindowData->width == 0)
                outputWindowData->Reset();
        }

        FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device* ffxivDevice = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
        Device11 dxDevice11 = (Device11)(IntPtr)ffxivDevice->D3D11Forwarder;
        DeviceContext11 dxDevCon11 = (DeviceContext11)(IntPtr)ffxivDevice->D3D11DeviceContext;
        
        outputWindowData->isGameActive = (byte)shareMemType;
        outputWindowData->newTop = cfg.yPosition;
        outputWindowData->newLeft = cfg.xPosition;
        outputWindowData->newWidth = (int)ffxivDevice->SwapChain->Width;
        outputWindowData->newHeight = (int)ffxivDevice->SwapChain->Height;
        outputWindowData->newTopmost = (byte)cfg.orderStatus;
        outputWindowData->updateWindow = true;

        CreateShaders(dxDevice11);
        CreateBuffers(dxDevice11, dxDevCon11);
        CreateTextures(dxDevice11, dxDevCon11);
    }

    private void Destroy()
    {
        outputWindowData->isGameActive = 0;
        outputWindowData->doClose = true;

        DestroyTextures();
        DestroyBuffers();
        DestroyShaders();

        hookManager.DisableFunctionHandles();
        hookManager.DisposeFunctionHandles();

        smm.CloseSharedMemory();
    }

    private void Enable()
    {
        string OutputWindow = Path.Combine(PluginInterface!.AssemblyLocation.DirectoryName!, "outputwindow.exe");
        if (File.Exists(OutputWindow))
            Process.Start(OutputWindow);
        else
            Log!.Error($"Can not find 'outputwindow.exe' in directory {PluginInterface!.AssemblyLocation.DirectoryName!}");
    }

    private void Disable()
    {
        outputWindowData->doClose = true;
    }


    private bool CreateShaders(Device11 dxDevice11)
    {
        psTexture.CompileShaderFromString(dxDevice11);
        vsTexture.CompileShaderFromString(dxDevice11);
        return true;
    }

    private void DestroyShaders()
    {
        psTexture.Release();
        vsTexture.Release();
    }

    private bool CreateBuffers(Device11 dxDevice11, DeviceContext11 dxDevCon11)
    {
        orthogSquare = new RenderSquare(dxDevice11, dxDevCon11);
        orthogSquare.SetShadersLayout(vsTexture.Layout!, vsTexture.VS!, psTexture.PS!);
        return true;
    }

    private void DestroyBuffers()
    {
        orthogSquare?.Release();
    }

    private bool CreateTextures(Device11 dxDevice11, DeviceContext11 dxDevCon11)
    {
        //Texture2D BackBuffer = swapChain11.GetBackBuffer<Texture2D>(0);
        //SharpDX.Direct3D11.Resource tmpResourceBB = BackBuffer.QueryInterface<SharpDX.Direct3D11.Resource>();
        //RenderTargetView backbufferRTV = new RenderTargetView(dxDevice11, tmpResourceBB);
        //tmpResourceBB.Dispose();

        pSamplerState = new SamplerState(dxDevice11, new SamplerStateDescription()
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            MipLodBias = 0.0f,
            MaximumAnisotropy = 1,
            ComparisonFunction = Comparison.Always,
            BorderColor = new RawColor4(0, 0, 0, 0),
            MinimumLod = float.MinValue,
            MaximumLod = float.MaxValue,
        });

        if (!CreateTexturesShared(dxDevice11, dxDevCon11))
            return false;

        return true;
    }

    private void DestroyTextures()
    {
        pSamplerState?.Dispose();
        selectedSRV?.Dispose();
    }

    private bool CreateTexturesShared(Device11 dxDevice11, DeviceContext11 dxDevCon11)
    {
        if (outputWindowData->isOutputActive > 0 && outputWindowData->sharedHandle > 0)
            sharedTexture = dxDevice11.OpenSharedResource<Texture2D>(outputWindowData->sharedHandle);

        if (sharedTexture != null)
        {
            //Resource sharedResource = sharedTexture.QueryInterface<Resource>();
            //Log!.Info($"dxDevice: {dxDevice11.NativePointer:x} | cont: {dxDevCon11.NativePointer:x} | SharedHandle: {sharedResource.SharedHandle:x} | SharedMutex: {sharedMutex}");
            //outputWindowData->sharedHandle = sharedResource.SharedHandle;
            //sharedResource.Dispose();

            SharpDX.Direct3D11.Resource tmpResource = sharedTexture.QueryInterface<SharpDX.Direct3D11.Resource>();
            sharedRTV = new RenderTargetView(dxDevice11, tmpResource);
            sharedSRV = new ShaderResourceView(dxDevice11, tmpResource);
            tmpResource.Dispose();
        }

        return true;
    }

    private void DestroyTexturesShared()
    {
        sharedRTV?.Dispose();
        sharedSRV?.Dispose();
        sharedTexture?.Dispose();
        sharedTexture = null;
    }


    //----
    // DXGIPresent
    //----
    private delegate void DXGIPresentDg(UInt64 a, UInt64 b);
    [Signature(Signatures.DXGIPresent, DetourName = nameof(DXGIPresentFn))]
    private Hook<DXGIPresentDg>? DXGIPresentHook = null;

    [HandleStatus("DXGIPresent")]
    public void DXGIPresentStatus(bool status, bool dispose)
    {
        if (dispose)
            DXGIPresentHook?.Dispose();
        else
        {
            if (status)
                DXGIPresentHook?.Enable();
            else
                DXGIPresentHook?.Disable();
        }
    }

    private unsafe void DXGIPresentFn(UInt64 a, UInt64 b)
    {
        FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device* ffxivDevice = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
        Device11 dxDevice11 = (Device11)(IntPtr)ffxivDevice->D3D11Forwarder;
        DeviceContext11 dxDevCon11 = (DeviceContext11)(IntPtr)ffxivDevice->D3D11DeviceContext;
        SwapChain11 swapChain11 = (SwapChain11)(IntPtr)ffxivDevice->SwapChain->DXGISwapChain;

        //----
        // If the window is open and we havent connected to the shared texture yet, connect to it
        //----
        if (outputWindowData->isOutputActive > 0 && outputWindowData->sharedHandle > 0 && sharedTexture == null)
        {
            outputWindowData->isGameActive = (byte)shareMemType;
            outputWindowData->newTop = cfg.yPosition;
            outputWindowData->newLeft = cfg.xPosition;
            outputWindowData->newWidth = (int)ffxivDevice->SwapChain->Width;
            outputWindowData->newHeight = (int)ffxivDevice->SwapChain->Height;
            outputWindowData->newTopmost = (byte)cfg.orderStatus;
            outputWindowData->updateWindow = true;

            CreateTexturesShared(dxDevice11, dxDevCon11);
        }
        //----
        // If the window is open and we have connected to the shared texture, render to it
        //----
        else if (outputWindowData->isOutputActive > 0 && outputWindowData->sharedHandle > 0 && sharedTexture != null)
        {
            if(outputWindowData->resetSharedHandle)
            {
                outputWindowData->resetSharedHandle = false;
                DestroyTexturesShared();
                CreateTexturesShared(dxDevice11, dxDevCon11);
            }

            if (cfg.showUI)
                cfg.renderIndex = gameWindowWithUI;
            else
                cfg.renderIndex = gameWindowWithoutUI;

            cfg.renderIndex = Math.Min(Math.Max(cfg.renderIndex, 0), 129);

            if (oldRenderIndex != cfg.renderIndex)
            {
                oldRenderIndex = cfg.renderIndex;
                selectedSRV = null;

                UInt64 rtManagerAddr = ((UInt64)renderTargetManager) + 0x20;
                Texture* rendText = *(Texture**)(rtManagerAddr + (ulong)(0x8 * cfg.renderIndex));

                if (rendText != null && rendText->D3D11Texture2D != null)
                {
                    Texture2DDescription rt0 = ((Texture2D)(IntPtr)rendText->D3D11Texture2D).Description;
                    //Log!.Info($"ID: {cfg.renderIndex} | {(UInt64)rendText:x} | Width: {rt0.Width} | Height: {rt0.Height} | Usage: {rt0.Usage:x} | Format: {rt0.Format:x} | BindFlags: {rt0.BindFlags:x} | OptionFlags: {rt0.OptionFlags}");

                    if ((rt0.BindFlags & BindFlags.ShaderResource) == BindFlags.ShaderResource)
                    {
                        SharpDX.Direct3D11.Resource tmpResource = ((Texture2D)(IntPtr)rendText->D3D11Texture2D).QueryInterface<SharpDX.Direct3D11.Resource>();
                        try
                        {
                            selectedSRV = new ShaderResourceView(dxDevice11, tmpResource);
                        }
                        catch (Exception)
                        {
                            selectedSRV = null;
                        }
                        tmpResource.Dispose();
                    }
                }
            }

            RawColor4 color = new RawColor4(0, 0, 0, 0);

            dxDevCon11.ClearRenderTargetView(sharedRTV, color);
            dxDevCon11.OutputMerger.SetRenderTargets(sharedRTV);
            dxDevCon11.Rasterizer.SetViewport(0f, 0f, ffxivDevice->SwapChain->Width, ffxivDevice->SwapChain->Height, 0f, 1f);
            dxDevCon11.PixelShader.SetSampler(0, pSamplerState);
            if (selectedSRV != null)
                dxDevCon11.PixelShader.SetShaderResource(0, selectedSRV);
            dxDevCon11.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
            if (orthogSquare != null)
                orthogSquare.Render();
        }
        //----
        // If the window is not open and we have connected to the shared texture, disconnect from it
        //----
        else if (outputWindowData->isOutputActive == 0 && outputWindowData->sharedHandle == 0 && sharedTexture != null)
        {
            DestroyTexturesShared();
        }

        DXGIPresentHook!.Original(a, b);
    }
}
