namespace TinyOrm.Abstractions.Core;

/// <summary>
/// Supported filter operators for strongly typed queries.
/// </summary>
public enum TinyOperator
{
    Eq,
    NotEq,
    Gt,
    Ge,
    Lt,
    Le,
    In,
    Like
}