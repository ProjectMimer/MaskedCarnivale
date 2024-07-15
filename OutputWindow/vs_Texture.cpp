#include "cShaderData.h"

vs_Texture::vs_Texture()
{
    AddLayout({ "SV_POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, D3D11_APPEND_ALIGNED_ELEMENT, D3D11_INPUT_PER_VERTEX_DATA, 0 });
    AddLayout({ "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, D3D11_APPEND_ALIGNED_ELEMENT, D3D11_INPUT_PER_VERTEX_DATA, 0 });
    SetSource("vs_Texture", "vs_5_0", R"""(
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
)""");
}
