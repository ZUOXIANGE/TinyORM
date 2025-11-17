using SqlKata.Compilers;

namespace TinyOrm.Dialects.Adapters;

/// <summary>
/// SQL Server dialect adapter.
/// </summary>
public sealed class SqlServerDialectAdapter : DialectAdapterBase
{
    private static readonly SqlServerCompiler _compiler = new();

    /// <inheritdoc />
    public override Compiler Compiler => _compiler;

    /// <inheritdoc />
    public override string Name => "SqlServer";

    public override string QuoteIdentifier(string name) => "[" + name + "]";

    public override string QuoteTable(string table, string? schema = null)
        => string.IsNullOrEmpty(schema) ? QuoteIdentifier(table) : QuoteIdentifier(schema) + "." + QuoteIdentifier(table);
}
