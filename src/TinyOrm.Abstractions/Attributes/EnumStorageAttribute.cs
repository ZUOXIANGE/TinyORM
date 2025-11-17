namespace TinyOrm.Abstractions.Attributes;

public enum EnumStorageKind
{
    Numeric = 0,
    String = 1
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class EnumStorageAttribute : Attribute
{
    public EnumStorageKind Kind { get; }
    public EnumStorageAttribute(EnumStorageKind kind)
    {
        Kind = kind;
    }
}
