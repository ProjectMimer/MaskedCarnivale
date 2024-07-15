using System;
using System.Collections.Generic;

using SharpDX.D3DCompiler;
using Device11 = SharpDX.Direct3D11.Device5;
using InputElement = SharpDX.Direct3D11.InputElement;
using VertexShader = SharpDX.Direct3D11.VertexShader;
using PixelShader = SharpDX.Direct3D11.PixelShader;
using InputLayout = SharpDX.Direct3D11.InputLayout;

namespace MaskedCarnivale.DirectX.ShaderData;
public class ShaderData
{
    string sName = "";
    string sVersion = "";
    string sSource = "";
    List<InputElement> rawLayout = new List<InputElement>();

    public VertexShader? VS { get; private set; } = null;
    public PixelShader? PS { get; private set; } = null;
    public InputLayout? Layout { get; private set; } = null;

    public void SetSource(string name, string version, string source)
    {
        sName = name;
        sVersion = version;
        sSource = source;
    }

    public void ClearLayout()
    {
        rawLayout.Clear();
    }

    public void AddLayout(InputElement element)
    {
        rawLayout.Add(element);
    }

    public bool CompileShaderFromString(Device11 dev)
    {
        ShaderFlags flags = ShaderFlags.PackMatrixColumnMajor | ShaderFlags.EnableStrictness | ShaderFlags.WarningsAreErrors;
#if DEBUG
        flags |= ShaderFlags.SkipOptimization | ShaderFlags.Debug;
#else
        flags |= ShaderFlags.OptimizationLevel3;
#endif
        ShaderBytecode shaderByteCode;
        try
        {
            shaderByteCode = ShaderBytecode.Compile(sSource, sName, sVersion, flags);
        }
        catch (Exception ex)
        {
            Plugin.Log!.Error($"Error compiling shader '{sName}' : {ex}");
            return false;
        }

        if (sVersion.Substring(0, 3) == "vs_")
        {
            try
            {
                Layout = new InputLayout(dev, shaderByteCode, rawLayout.ToArray());
            }
            catch (Exception ex)
            {
                Plugin.Log!.Error($"Error creating layout for shader '{sName}' : {ex}");
                return false;
            }

            VS = new VertexShader(dev, shaderByteCode);
        }
        else if (sVersion.Substring(0, 3) == "ps_")
            PS = new PixelShader(dev, shaderByteCode);

        shaderByteCode?.Dispose();
        return true;
    }

    public void Release()
    {
        VS?.Dispose();
        PS?.Dispose();
        Layout?.Dispose();
    }
}
