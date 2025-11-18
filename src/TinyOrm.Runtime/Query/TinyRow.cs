using System.Data.Common;

namespace TinyOrm.Runtime.Query;

/// <summary>
/// 轻量级结果行封装，按列名大小写不敏感访问值。
/// 适用于选择自定义列或聚合结果时的轻量读取。
/// </summary>
public sealed class TinyRow
{
    private readonly Dictionary<string, object?> _values;

    /// <summary>从数据读取器与列序号构造行字典。</summary>
    public TinyRow(Dictionary<string,int> ordinals, DbDataReader reader)
    {
        _values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in ordinals)
        {
            var ord = kv.Value;
            _values[kv.Key] = reader.IsDBNull(ord) ? null : reader.GetValue(ord);
        }
    }

    /// <summary>按列名获取值，不存在时返回 null。</summary>
    public object? this[string name]
    {
        get => _values.TryGetValue(name, out var v) ? v : null;
    }
}
