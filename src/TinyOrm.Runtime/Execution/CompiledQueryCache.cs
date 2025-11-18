using System.Collections.Concurrent;

namespace TinyOrm.Runtime.Execution;

/// <summary>
/// 编译后 SQL 的简单缓存，按形状键（shape key）存储。
/// 可避免重复构建 INSERT/UPDATE/DELETE 等语句，提高性能。
/// </summary>
public static class CompiledQueryCache
{
    private static readonly ConcurrentDictionary<string, string> _sqlCache = new();

    /// <summary>获取或添加指定形状键对应的 SQL。</summary>
    public static string GetOrAdd(string shapeKey, Func<string> factory) => _sqlCache.GetOrAdd(shapeKey, _ => factory());
}