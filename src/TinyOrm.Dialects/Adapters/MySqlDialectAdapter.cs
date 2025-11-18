using SqlKata.Compilers;

namespace TinyOrm.Dialects.Adapters;

/// <summary>
/// MySQL 方言适配器。
/// </summary>
public sealed class MySqlDialectAdapter : DialectAdapterBase
{
    private static readonly MySqlCompiler _compiler = new();

    /// <inheritdoc />
    public override Compiler Compiler => _compiler;

    /// <inheritdoc />
    public override string Name => "MySql";

    public override string QuoteIdentifier(string name) => "`" + name + "`";

    public override string QuoteTable(string table, string? schema = null)
        => string.IsNullOrEmpty(schema) ? QuoteIdentifier(table) : QuoteIdentifier(schema) + "." + QuoteIdentifier(table);
}
