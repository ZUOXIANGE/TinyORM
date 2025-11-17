using System.Data.Common;

namespace TinyOrm.Runtime.Query;

public sealed class TinyRow
{
    private readonly Dictionary<string, object?> _values;

    public TinyRow(Dictionary<string,int> ordinals, DbDataReader reader)
    {
        _values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in ordinals)
        {
            var ord = kv.Value;
            _values[kv.Key] = reader.IsDBNull(ord) ? null : reader.GetValue(ord);
        }
    }

    public object? this[string name]
    {
        get => _values.TryGetValue(name, out var v) ? v : null;
    }
}
