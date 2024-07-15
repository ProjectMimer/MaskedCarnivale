#include "cRenderObject.h"


RenderObject::RenderObject() : dev(nullptr), devcon(nullptr)
{
}

RenderObject::RenderObject(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon) : dev(tdev), devcon(tdevcon)
{
}

bool RenderObject::SetVertexBuffer(std::vector<float> vertices, int itmStride, D3D11_USAGE usage)
{
    vertexList = vertices;
    stride = itmStride;
    byteStride = stride * sizeof(float);
    vertexCount = (int)vertices.size() / stride;
    int byteWidth = vertexCount * byteStride;

    vertexSet = false;
    if (vertices.size() > 0)
    {
        D3D11_BUFFER_DESC vertexBufferDesc = D3D11_BUFFER_DESC();
        vertexBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
        vertexBufferDesc.ByteWidth = byteWidth;
        vertexBufferDesc.BindFlags = D3D11_BIND_VERTEX_BUFFER;
        vertexBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
        vertexBufferDesc.MiscFlags = 0;
        vertexBufferDesc.StructureByteStride = 0;

        D3D11_SUBRESOURCE_DATA initData = D3D11_SUBRESOURCE_DATA();
        initData.pSysMem = &vertices[0];

        HRESULT result = dev->CreateBuffer(&vertexBufferDesc, &initData, &vertexBuffer);
        if (FAILED(result)) {
            return false;
        }

        vertexSet = true;
    }
    return vertexSet;
}

int RenderObject::GetVertexCount()
{
    return vertexCount;
}

bool RenderObject::SetIndexBuffer(std::vector<short> indices, D3D11_USAGE usage)
{
    indexList = indices;
    indexCount = (int)indices.size();
    int byteWidth = indexCount * sizeof(short);

    indexSet = false;
    if (indices.size() > 0)
    {
        D3D11_BUFFER_DESC indexBufferDesc = D3D11_BUFFER_DESC();
        indexBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
        indexBufferDesc.ByteWidth = byteWidth;
        indexBufferDesc.BindFlags = D3D11_BIND_INDEX_BUFFER;
        indexBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
        indexBufferDesc.MiscFlags = 0;
        indexBufferDesc.StructureByteStride = 0;

        D3D11_SUBRESOURCE_DATA initData = D3D11_SUBRESOURCE_DATA();
        initData.pSysMem = &indices[0];

        HRESULT result = dev->CreateBuffer(&indexBufferDesc, &initData, &indexBuffer);
        if (FAILED(result)) {
            return false;
        }

        indexSet = true;
    }
    return indexSet;
}

void RenderObject::SetShadersLayout(ID3D11InputLayout* layout, ID3D11VertexShader* vertex, ID3D11PixelShader* pixel)
{
    structLayout = layout;
    vertexShader = vertex;
    pixelShader = pixel;
    layoutSet = true;
}

void RenderObject::MapResource(void* data, int size)
{
    D3D11_MAPPED_SUBRESOURCE mappedResource = D3D11_MAPPED_SUBRESOURCE();
    devcon->Map(vertexBuffer, 0, D3D11_MAP_WRITE_DISCARD, 0, &mappedResource);
    memcpy(mappedResource.pData, data, size);
    devcon->Unmap(vertexBuffer, 0);
}


bool RenderObject::RayIntersection(XMVECTOR origin, XMVECTOR direction, XMVECTOR* intersection, float* distance, std::stringstream* logError)
{
    bool intersected = false;
    //XMMATRIX objMatrixI = XMMatrixInverse(0, objMatrix);
    for (int i = 0; i < indexCount; i += 3)
    {
        int i0 = indexList[i + 0];
        int i1 = indexList[i + 1];
        int i2 = indexList[i + 2];

        XMVECTOR v0 = { vertexList[i0 * stride + 0], vertexList[i0 * stride + 1], vertexList[i0 * stride + 2] };
        XMVECTOR v1 = { vertexList[i1 * stride + 0], vertexList[i1 * stride + 1], vertexList[i1 * stride + 2] };
        XMVECTOR v2 = { vertexList[i2 * stride + 0], vertexList[i2 * stride + 1], vertexList[i2 * stride + 2] };

        XMVECTOR v0uv = { vertexList[i0 * stride + 3], vertexList[i0 * stride + 4], 1.0f };
        XMVECTOR v1uv = { vertexList[i1 * stride + 3], vertexList[i1 * stride + 4], 1.0f };
        XMVECTOR v2uv = { vertexList[i2 * stride + 3], vertexList[i2 * stride + 4], 1.0f };

        v0 = XMVector3Transform(v0, objMatrix);
        v1 = XMVector3Transform(v1, objMatrix);
        v2 = XMVector3Transform(v2, objMatrix);

        float pickU = 0.0f;
        float pickV = 0.0f;
        float pickW = 0.0f;

        bool rayHit = RayTest(origin, direction, v0, v1, v2, &pickU, &pickV, &pickW, distance, logError);
        if (rayHit)
        {
            intersected = true;
            *intersection = pickU * v1uv + pickV * v2uv + pickW * v0uv;
            //(*logError) << pickU << " : " << pickV << " : " << pickW << " : " << (*distance) << " -- " << intersection->m128_f32[0] << ", " <<intersection->m128_f32[1] << ", " << intersection->m128_f32[2] << std::endl;
        }
    }
    return intersected;
}

bool RenderObject::RayTest(XMVECTOR origin, XMVECTOR direction, XMVECTOR v0, XMVECTOR v1, XMVECTOR v2, float* barycentricU, float* barycentricV, float* barycentricW, float* distance, std::stringstream* logError)
{
    XMVECTOR v1v0 = v1 - v0;
    XMVECTOR v2v0 = v2 - v0;
    XMVECTOR vOv0 = origin - v0;

    // Begin calculating determinant - also used to calculate barycentricU parameter
    XMVECTOR pvec = XMVector3Cross(direction, v2v0);

    // If determinant is near zero, ray lies in plane of triangle
    float det = 0;
    DirectX::XMStoreFloat(&det, XMVector3Dot(v1v0, pvec));
    if (det < 0.0001f && det > -0.0001f)
        return false;
    float fInvDet = 1.0f / det;

    // Calculate barycentricU parameter and test bounds
    DirectX::XMStoreFloat(barycentricU, XMVector3Dot(vOv0, pvec) * fInvDet);
    if (*barycentricU < 0.0f || *barycentricU > 1.0f)
        return false;

    // Prepare to test barycentricV parameter
    XMVECTOR qvec = XMVector3Cross(vOv0, v1v0);

    // Calculate barycentricV parameter and test bounds
    DirectX::XMStoreFloat(barycentricV, XMVector3Dot(direction, qvec) * fInvDet);
    if (*barycentricV < 0.0f || (*barycentricU + *barycentricV) > 1.0f)
        return false;

    // Calculate pickDistance
    DirectX::XMStoreFloat(distance, XMVector3Dot(v2v0, qvec) * fInvDet);
    if (*distance > 0)
        return false;
    (*barycentricW) = 1.f - (*barycentricU) - (*barycentricV);

    //(*logError) << det << " : " << fInvDet << " : " << (*barycentricU) << " : " << (*barycentricV) << " : " << (*distance) << std::endl;
    return true;

}

void RenderObject::Render()
{
    unsigned int offset = 0;
    if (layoutSet)
    {
        devcon->IASetInputLayout(structLayout);
        devcon->VSSetShader(vertexShader, 0, 0);
        devcon->PSSetShader(pixelShader, 0, 0);

        if (vertexSet)
            devcon->IASetVertexBuffers(0, 1, &vertexBuffer, &byteStride, &offset);

        if (indexSet)
        {
            devcon->IASetIndexBuffer(indexBuffer, DXGI_FORMAT_R16_UINT, 0);
            devcon->DrawIndexed(indexCount, 0, 0);
        }
        else
        {
            devcon->Draw(vertexCount, 0);
        }
    }
}


void RenderObject::SetObjectMatrix(XMMATRIX matrix)
{
    objMatrix = matrix;
}

XMMATRIX RenderObject::GetObjectMatrix(bool inverse, bool transpose)
{
    if (transpose && inverse)
        return XMMatrixTranspose(XMMatrixInverse(0, objMatrix));
    else if (transpose)
        return XMMatrixTranspose(objMatrix);
    else if (inverse)
        return XMMatrixInverse(0, objMatrix);
    else
        return objMatrix;
}

void RenderObject::Release()
{
    if (vertexBuffer) { vertexBuffer->Release(); vertexBuffer = nullptr; }
    if (indexBuffer) { indexBuffer->Release(); indexBuffer = nullptr; }

    if (structLayout) { structLayout = nullptr; }
    if (vertexShader) { vertexShader = nullptr; }
    if (pixelShader) { pixelShader = nullptr; }

    vertexCount = 0;
    indexCount = 0;

    dev = nullptr;
    devcon = nullptr;
}
