namespace TinyOrm.Abstractions.Core;

/// <summary>
/// 实体属性的强类型字段引用。
/// 通过该类型可在查询/排序/分组中安全引用列名。
/// </summary>
/// <typeparam name="TEntity">实体类型。</typeparam>
/// <typeparam name="TProperty">属性类型。</typeparam>
public readonly record struct Field<TEntity, TProperty>(string PropertyName, string ColumnName);