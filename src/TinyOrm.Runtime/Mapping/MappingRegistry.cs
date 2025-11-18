using System.Collections.Concurrent;
using System.Data.Common;
using TinyOrm.Dialects;

namespace TinyOrm.Runtime.Mapping;

/// <summary>
/// 列元数据，描述属性与列的映射以及键/标识/计算等特性。
/// </summary>
public sealed class ColumnMeta
{
    public string Prop { get; init; } = string.Empty;
    public string Col { get; init; } = string.Empty;
    public bool IsKey { get; init; }
    public bool IsIdentity { get; init; }
    public bool IsComputed { get; init; }
}

/// <summary>
/// 实体映射项，包含表信息、列集合以及生成/绑定 SQL 的委托。
/// 这些委托由 Source Generator 生成并在运行时注册到此处。
/// </summary>
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

/// <summary>
/// 映射注册表。存储由 Source Generator 生成的实体映射信息。
/// </summary>
public static class MappingRegistry
{
    private static readonly ConcurrentDictionary<Type, EntityMapEntry> _entries = new();

    /// <summary>注册实体映射。</summary>
    public static void Register(Type t, EntityMapEntry entry) => _entries[t] = entry;

    /// <summary>按类型获取映射。</summary>
    public static EntityMapEntry Get(Type t)
        => _entries.TryGetValue(t, out var e) ? e : throw new InvalidOperationException($"No mapping registered for type {t}");

    /// <summary>按泛型实体类型获取映射。</summary>
    public static EntityMapEntry Get<TEntity>() where TEntity : class
        => Get(typeof(TEntity));
}