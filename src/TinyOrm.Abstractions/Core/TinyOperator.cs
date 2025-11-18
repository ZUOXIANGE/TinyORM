namespace TinyOrm.Abstractions.Core;

/// <summary>
/// 强类型查询支持的比较/集合/匹配运算符。
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