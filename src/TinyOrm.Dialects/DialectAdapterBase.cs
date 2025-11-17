using SqlKata.Compilers;
using System.Data;

namespace TinyOrm.Dialects;

/// <summary>
/// Base implementation for dialect adapters.
/// </summary>
public abstract class DialectAdapterBase : IDialectAdapter
{
    /// <inheritdoc />
    public abstract Compiler Compiler { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
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

    public virtual string QuoteIdentifier(string name) => "\"" + name + "\"";

    public virtual string QuoteTable(string table, string? schema = null)
        => string.IsNullOrEmpty(schema) ? QuoteIdentifier(table) : QuoteIdentifier(schema) + "." + QuoteIdentifier(table);

    public virtual string Parameter(string name) => "@" + name;

    public virtual bool SupportsMultiValuesInsert => true;
}