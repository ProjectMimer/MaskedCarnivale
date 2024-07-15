using System;

namespace MaskedCarnivale.Structures;


[AttributeUsage(AttributeTargets.Method)]
public sealed class StaticAddressAttribute(string signature, ushort[] relativeFollowOffsets, bool isPointer = false) : Attribute
{
    public StaticAddressAttribute(string signature, ushort relativeFollowOffset, bool isPointer = false) : this(signature, [relativeFollowOffset], isPointer) { }
    public string Signature { get; } = signature;
    public ushort[] RelativeFollowOffsets { get; } = relativeFollowOffsets;
    public bool IsPointer { get; } = isPointer;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class VirtualFunctionAttribute(uint index) : Attribute
{
    public uint Index { get; } = index;
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class FixedSizeArrayAttribute(bool isString = false) : Attribute
{
    public bool IsString { get; } = isString;
}
