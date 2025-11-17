using SqlKata.Compilers;
using System.Data;

namespace TinyOrm.Dialects;

/// <summary>
/// Provides database-specific compiler and type mapping services.
/// </summary>
public interface IDialectAdapter
{
    /// <summary>Returns the SQL compiler for the target database.</summary>
    Compiler Compiler { get; }

    /// <summary>Returns the dialect name.</summary>
    string Name { get; }

    /// <summary>Maps a CLR type to a database parameter type.</summary>
    DbType MapClrType(Type type);

    /// <summary>Quotes an identifier (column or table) according to dialect.</summary>
    string QuoteIdentifier(string name);

    /// <summary>Quotes a table name with optional schema.</summary>
    string QuoteTable(string table, string? schema = null);

    /// <summary>Returns a parameter placeholder for the given name.</summary>
    string Parameter(string name);

    bool SupportsMultiValuesInsert { get; }
}