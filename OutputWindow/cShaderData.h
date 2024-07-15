#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <D3D11.h>
#include <D3DCompiler.h>
#include <sstream>
#include <vector>

class ShaderData
{
    std::string sName = "";
    std::string sVersion = "";
    std::string sSource = "";
    std::vector<D3D11_INPUT_ELEMENT_DESC> rawLayout = std::vector<D3D11_INPUT_ELEMENT_DESC>();

public:
    ID3D11VertexShader* VS = nullptr;
    ID3D11PixelShader* PS = nullptr;
    ID3D11InputLayout* Layout = nullptr;

    void SetSource(std::string, std::string, std::string);
    void ClearLayout();
    void AddLayout(D3D11_INPUT_ELEMENT_DESC);
    bool CompileShaderFromString(ID3D11Device* dev);
    void Release();
};


class vs_Texture : public ShaderData
{
public:
    vs_Texture();
};

class ps_Texture : public ShaderData
{
public:
    ps_Texture();
};
