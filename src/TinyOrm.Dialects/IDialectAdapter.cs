using SqlKata.Compilers;
using System.Data;

namespace TinyOrm.Dialects;

/// <summary>
/// 提供数据库方言相关的编译器与类型映射服务。
/// 用于生成不同数据库的 SQL 以及参数/标识符处理。
/// </summary>
public interface IDialectAdapter
{
    /// <summary>获取目标数据库的 SqlKata 编译器。</summary>
    Compiler Compiler { get; }

    /// <summary>获取方言名称。</summary>
    string Name { get; }

    /// <summary>将 CLR 类型映射到数据库参数类型。</summary>
    DbType MapClrType(Type type);

    /// <summary>按方言规则引用标识符（列或表）。</summary>
    string QuoteIdentifier(string name);

    /// <summary>按方言规则引用表名，可包含模式名。</summary>
    string QuoteTable(string table, string? schema = null);

    /// <summary>返回参数占位符（带方言前缀）。</summary>
    string Parameter(string name);

    /// <summary>是否支持多值插入（批量 VALUES）。</summary>
    bool SupportsMultiValuesInsert { get; }
}