namespace TinyOrm.Diagnostics;

/// <summary>
/// 生成代码（映射）检查工具，用于快速查看实体映射的表与列信息。
/// </summary>
public static class GeneratedCodeInspector
{
    /// <summary>描述实体的映射信息（表名与列集合）。</summary>
    public static string DescribeEntityMap<TEntity>() where TEntity : class
    {
        var mapType = typeof(TEntity).Assembly.GetType(typeof(TEntity).FullName + "Map")!;
        var tableField = mapType.GetField("Table")!;
        var schemaField = mapType.GetField("Schema");
        var columnsField = mapType.GetField("Columns")!;
        var table = (string)tableField.GetValue(null)!;
        var schema = (string?)schemaField?.GetValue(null);
        var cols = (System.ValueTuple<string,string,bool,bool,bool>[])columnsField.GetValue(null)!;
        var colStr = string.Join(", ", cols.Select(c => c.Item2 + (c.Item3 ? "[PK]" : "")));
        return (schema is null ? table : schema + "." + table) + ": " + colStr;
    }
}
