using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using SharpDX.Direct3D11;
using Device11 = SharpDX.Direct3D11.Device5;
using DeviceContext11 = SharpDX.Direct3D11.DeviceContext4;
using VertexShader = SharpDX.Direct3D11.VertexShader;
using PixelShader = SharpDX.Direct3D11.PixelShader;
using InputLayout = SharpDX.Direct3D11.InputLayout;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace MaskedCarnivale.DirectX.RenderObjects;
public class RenderObject
{
    Device11? dev { get; set; } = null;
    DeviceContext11? devcon { get; set; } = null;

    InputLayout? structLayout { get; set; } = null;
    VertexShader? vertexShader { get; set; } = null;
    PixelShader? pixelShader { get; set; } = null;

    Buffer? vertexBuffer { get; set; } = null;
    Buffer? indexBuffer { get; set; } = null;

    List<float> vertexList = new List<float>();
    List<short> indexList = new List<short>();

    Matrix4x4 objMatrix = Matrix4x4.Identity;
    int stride = 0;
    int byteStride = 0;
    int vertexCount = 0;
    int indexCount = 0;

    bool vertexSet = false;
    bool indexSet = false;
    bool layoutSet = false;

    public RenderObject(Device11 tdev, DeviceContext11 tdevcon)
    {
        dev = tdev;
        devcon = tdevcon;
    }

    public bool SetVertexBuffer(List<float> vertices, int itmStride, ResourceUsage usage = ResourceUsage.Dynamic)
    {
        vertexList = vertices;
        stride = itmStride;
        byteStride = stride * sizeof(float);
        vertexCount = vertices.Count() / stride;
        int byteWidth = vertexCount * byteStride;

        vertexSet = false;
        if (vertices.Count() > 0)
        {
            try
            {
                vertexBuffer = Buffer.Create(dev, BindFlags.VertexBuffer, vertices.ToArray(), byteWidth, usage, CpuAccessFlags.Write, ResourceOptionFlags.None, 0);
            }
            catch (Exception ex)
            {
                Plugin.Log!.Error($"Error setting vertex buffer : {ex}");
                return false;
            }
        }
        vertexSet = true;
        return vertexSet;
    }

    public bool SetIndexBuffer(List<short> indices, ResourceUsage usage = ResourceUsage.Dynamic)
    {
        indexList = indices;
        indexCount = indices.Count();
        int byteWidth = indexCount * sizeof(short);

        indexSet = false;
        if (indices.Count() > 0)
        {
            try
            {
                indexBuffer = Buffer.Create(dev, BindFlags.IndexBuffer, indices.ToArray(), byteWidth, usage, CpuAccessFlags.Write, ResourceOptionFlags.None, 0);
            }
            catch (Exception ex)
            {
                Plugin.Log!.Error($"Error setting index buffer : {ex}");
                return false;
            }
            
        }
        indexSet = true;
        return indexSet;
    }

    public void SetShadersLayout(InputLayout layout, VertexShader vertex, PixelShader pixel)
    {
        structLayout = layout;
        vertexShader = vertex;
        pixelShader = pixel;
        layoutSet = true;
    }

    public void Render()
    {
        int offset = 0;

        if (layoutSet)
        {
            devcon!.InputAssembler.InputLayout = structLayout;
            devcon!.VertexShader.Set(vertexShader);
            devcon!.PixelShader.Set(pixelShader);

            if (vertexSet)
                devcon.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, byteStride, offset));

            if (indexSet)
            {
                devcon.InputAssembler.SetIndexBuffer(indexBuffer, SharpDX.DXGI.Format.R16_UInt, 0);
                devcon.DrawIndexed(indexCount, 0, 0);
            }
            else
            {
                devcon.Draw(vertexCount, 0);
            }
        }
    }

    public void SetObjectMatrix(Matrix4x4 matrix)
    {
        objMatrix = matrix;
    }

    public Matrix4x4 GetObjectMatrix(bool inverse, bool transpose)
    {
        Matrix4x4 retMatrix = objMatrix;
        if (inverse)
            Matrix4x4.Invert(retMatrix, out retMatrix);
        if (transpose)
            retMatrix = Matrix4x4.Transpose(retMatrix);
        return retMatrix;
    }

    public void Release()
    {
        vertexBuffer?.Dispose();
        indexBuffer?.Dispose();

        structLayout?.Dispose();
        vertexShader?.Dispose();
        pixelShader?.Dispose();

        vertexCount = 0;
        vertexList.Clear();
        indexCount = 0;
        indexList.Clear();

        dev = null;
        devcon = null;
    }
}
