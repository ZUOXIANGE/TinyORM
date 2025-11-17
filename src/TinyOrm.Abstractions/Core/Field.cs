namespace TinyOrm.Abstractions.Core;

/// <summary>
/// Represents a strongly typed field reference for an entity property.
/// </summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
/// <typeparam name="TProperty">Property type.</typeparam>
public readonly record struct Field<TEntity, TProperty>(string PropertyName, string ColumnName);