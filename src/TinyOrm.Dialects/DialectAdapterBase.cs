using SqlKata.Compilers;
using System.Data;

namespace TinyOrm.Dialects;

/// <summary>
/// 方言适配器的基类，提供通用的类型映射与标识符处理。
/// 派生类可覆盖以适配特定数据库。
/// </summary>
public abstract class DialectAdapterBase : IDialectAdapter
{
    /// <inheritdoc />
    public abstract Compiler Compiler { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    /// <summary>默认的 CLR 到 DbType 映射。</summary>
    public virtual DbType MapClrType(Type type)
    {
        if (type == typeof(string)) return DbType.String;
        if (type == typeof(int)) return DbType.Int32;
        if (type == typeof(long)) return DbType.Int64;
        if (type == typeof(short)) return DbType.Int16;
        if (type == typeof(byte)) return DbType.Byte;
        if (type == typeof(bool)) return DbType.Boolean;
        if (type == typeof(DateTime)) return DbType.DateTime2;
        if (type == typeof(DateTimeOffset)) return DbType.DateTimeOffset;
        if (type == typeof(Guid)) return DbType.Guid;
        if (type == typeof(decimal)) return DbType.Decimal;
        if (type == typeof(double)) return DbType.Double;
        if (type == typeof(float)) return DbType.Single;
        if (type == typeof(byte[])) return DbType.Binary;
        return DbType.Object;
    }

    /// <summary>引用标识符（默认使用双引号）。</summary>
    public virtual string QuoteIdentifier(string name) => "\"" + name + "\"";

    /// <summary>引用表名（默认 schema.table）。</summary>
    public virtual string QuoteTable(string table, string? schema = null)
        => string.IsNullOrEmpty(schema) ? QuoteIdentifier(table) : QuoteIdentifier(schema) + "." + QuoteIdentifier(table);

    /// <summary>参数占位符前缀（默认 @）。</summary>
    public virtual string Parameter(string name) => "@" + name;

    /// <summary>是否支持多值插入（默认支持）。</summary>
    public virtual bool SupportsMultiValuesInsert => true;
}