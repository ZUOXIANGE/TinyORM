using System.Collections.Concurrent;
using System.Data.Common;
using TinyOrm.Dialects;

namespace TinyOrm.Runtime.Mapping;

public sealed class ColumnMeta
{
    public string Prop { get; init; } = string.Empty;
    public string Col { get; init; } = string.Empty;
    public bool IsKey { get; init; }
    public bool IsIdentity { get; init; }
    public bool IsComputed { get; init; }
}

public sealed class EntityMapEntry
{
    public string Table { get; init; } = string.Empty;
    public string? Schema { get; init; }
    public ColumnMeta[] Columns { get; init; } = Array.Empty<ColumnMeta>();
    public Func<IDialectAdapter, string> BuildInsert { get; init; } = _ => string.Empty;
    public Action<DbCommand, object, IDialectAdapter> BindInsert { get; init; } = (_, _, _) => { };
    public Func<IDialectAdapter, string> BuildUpdate { get; init; } = _ => string.Empty;
    public Action<DbCommand, object, IDialectAdapter> BindUpdate { get; init; } = (_, _, _) => { };
    public Func<IDialectAdapter, string> BuildDelete { get; init; } = _ => string.Empty;
    public Action<DbCommand, object, IDialectAdapter> BindDelete { get; init; } = (_, _, _) => { };
    public Func<object> MaterializerFactory { get; init; } = () => new object();
    public Func<object, (string Col, object? Val, Type Type)[]> ExtractInsertValues { get; init; } = _ => Array.Empty<(string, object?, Type)>();
    public Func<string, string> GetColumn { get; init; } = static p => p;
}

public static class MappingRegistry
{
    private static readonly ConcurrentDictionary<Type, EntityMapEntry> _entries = new();

    public static void Register(Type t, EntityMapEntry entry) => _entries[t] = entry;

    public static EntityMapEntry Get(Type t)
        => _entries.TryGetValue(t, out var e) ? e : throw new InvalidOperationException($"No mapping registered for type {t}");

    public static EntityMapEntry Get<TEntity>() where TEntity : class
        => Get(typeof(TEntity));
}