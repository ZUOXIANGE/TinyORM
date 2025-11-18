using System.Data.Common;

namespace TinyOrm.Abstractions.Core;

/// <summary>
/// 将数据读取器中的行零反射地实体化为对象实例。
/// 由 Source Generator 为实体生成具体实现，也可自定义实现。
/// </summary>
/// <typeparam name="TEntity">实体类型。</typeparam>
public interface ITinyRowMaterializer<TEntity>
{
    /// <summary>初始化读取器中的列序号。</summary>
    void Initialize(DbDataReader reader);

    /// <summary>读取当前行并返回实体实例。</summary>
    TEntity Read(DbDataReader reader);
}