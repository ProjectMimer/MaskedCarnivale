namespace MaskedCarnivale.DirectX.ShaderData;

public class ps_Texture : ShaderData
{
    public ps_Texture()
    {
        SetSource("ps_Texture", "ps_5_0", """
Texture2D shaderTexture;
SamplerState sampleType;
struct VOut
{
	float4 position : SV_POSITION;
	float2 tex : TEXCOORD0;
};

float4 ps_Texture(VOut input) : SV_TARGET
{
	return shaderTexture.Sample(sampleType, input.tex);
}
""");
    }
}
