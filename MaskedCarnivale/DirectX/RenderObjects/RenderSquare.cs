using System.Collections.Generic;

using Device11 = SharpDX.Direct3D11.Device5;
using DeviceContext11 = SharpDX.Direct3D11.DeviceContext4;

namespace MaskedCarnivale.DirectX.RenderObjects;
public class RenderSquare : RenderObject
{ 
    public RenderSquare(Device11 tdev, DeviceContext11 tdevcon) : base(tdev, tdevcon)
    {
        List<float> vertices = new List<float>()
        {
         -1, -1, 0,     0, 1,
         -1,  1, 0,     0, 0,
          1,  1, 0,     1, 0,
          1, -1, 0,     1, 1,
        };
        SetVertexBuffer(vertices, 5);

        List<short> indices = new List<short>()
        {
        0, 1, 2,
        2, 3, 0
        };
        SetIndexBuffer(indices);
    }
}
