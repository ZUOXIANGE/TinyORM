using System.Collections.Concurrent;

namespace TinyOrm.Runtime.Execution;

/// <summary>
/// Provides a simple cache for compiled SQL query strings keyed by shape.
/// </summary>
public static class CompiledQueryCache
{
    private static readonly ConcurrentDictionary<string, string> _sqlCache = new();

    /// <summary>Gets or adds a compiled SQL for the given shape key.</summary>
    public static string GetOrAdd(string shapeKey, Func<string> factory) => _sqlCache.GetOrAdd(shapeKey, _ => factory());
}