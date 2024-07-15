using InputElement = SharpDX.Direct3D11.InputElement;
using InputClassification = SharpDX.Direct3D11.InputClassification;
using Format = SharpDX.DXGI.Format;

namespace MaskedCarnivale.DirectX.ShaderData;
public class vs_Texture : ShaderData
{
    public vs_Texture()
    {
        AddLayout(new InputElement("SV_POSITION", 0, Format.R32G32B32_Float, InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0));
        AddLayout(new InputElement("TEXCOORD", 0, Format.R32G32_Float, InputElement.AppendAligned, 0, InputClassification.PerVertexData, 0));
        SetSource("vs_Texture", "vs_5_0", """
struct VOut
{
	float4 position : SV_POSITION;
	float2 tex : TEXCOORD0;
};

VOut vs_Texture(float4 position : SV_POSITION, float2 tex : TEXCOORD0)
{
	VOut output;
	output.position = float4(position.xyz, 1.0f);
	output.tex = tex;
	return output;
}
""");
    }
}
