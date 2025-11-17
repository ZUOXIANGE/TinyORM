using SqlKata.Compilers;

namespace TinyOrm.Dialects.Adapters;

/// <summary>
/// SQLite dialect adapter.
/// </summary>
public sealed class SqliteDialectAdapter : DialectAdapterBase
{
    private static readonly SqliteCompiler _compiler = new();

    /// <inheritdoc />
    public override Compiler Compiler => _compiler;

    /// <inheritdoc />
    public override string Name => "Sqlite";

    public override string QuoteIdentifier(string name) => "\"" + name + "\"";

    public override string QuoteTable(string table, string? schema = null) => QuoteIdentifier(table);
}
