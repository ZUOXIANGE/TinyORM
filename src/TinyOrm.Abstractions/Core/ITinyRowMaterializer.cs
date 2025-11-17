using System.Data.Common;

namespace TinyOrm.Abstractions.Core;

/// <summary>
/// Materializes entity instances from a data reader with zero reflection.
/// </summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
public interface ITinyRowMaterializer<TEntity>
{
    /// <summary>Initializes column ordinals for the provided reader.</summary>
    void Initialize(DbDataReader reader);

    /// <summary>Reads the current row and returns an entity instance.</summary>
    TEntity Read(DbDataReader reader);
}