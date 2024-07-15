#include "cRenderObject.h"

RenderSquare::RenderSquare(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon) : RenderObject(tdev, tdevcon)
{
    std::vector<float> vertices =
    {
         -1, -1, 0,		0, 1,
         -1,  1, 0,		0, 0,
          1,  1, 0,		1, 0,
          1, -1, 0,		1, 1,
    };
    SetVertexBuffer(vertices, 5);


    std::vector<short> indices =
    {
        0, 1, 2,
        2, 3, 0
    };
    SetIndexBuffer(indices);
}
