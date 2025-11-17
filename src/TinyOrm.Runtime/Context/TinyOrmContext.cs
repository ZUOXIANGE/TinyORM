using System.Data;
using System.Data.Common;
using System.Text;

using TinyOrm.Dialects;
using TinyOrm.Runtime.Mapping;
using TinyOrm.Abstractions.Core;

namespace TinyOrm.Runtime.Context;

/// <summary>
/// Provides database access and query execution services.
/// </summary>
public sealed class TinyOrmContext
{
    /// <summary>Database connection.</summary>
    public DbConnection Connection { get; }

    /// <summary>Dialect adapter.</summary>
    public IDialectAdapter Dialect { get; }

    public bool EnableSqlLogging { get; set; }

    public Action<string>? SqlLogger { get; set; }

    /// <summary>
    /// Creates a new context instance.
    /// </summary>
    /// <param name="connection">Database connection.</param>
    /// <param name="dialect">Dialect adapter.</param>
    public TinyOrmContext(DbConnection connection, IDialectAdapter dialect)
    {
        Connection = connection;
        Dialect = dialect;
        EnableSqlLogging = true;
        SqlLogger = s => System.Console.WriteLine(s);
    }

    /// <summary>Begins a transaction.</summary>
    public DbTransaction BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        => Connection.BeginTransaction(isolationLevel);

    public TinyOrm.Runtime.Query.TinyQueryable<TEntity> Query<TEntity>() where TEntity : class, new()
    {
        var entry = MappingRegistry.Get<TEntity>();
        var q = new SqlKata.Query(entry.Table);
        var matObj = entry.MaterializerFactory();
        var materializer = (TinyOrm.Abstractions.Core.ITinyRowMaterializer<TEntity>)matObj;
        return new TinyOrm.Runtime.Query.TinyQueryable<TEntity>(this, q, materializer, entry.Table);
    }

    public int Insert<TEntity>(TEntity entity, Action<DbCommand>? configure = null) where TEntity : class, new()
    {
        var entry = MappingRegistry.Get<TEntity>();
        var shapeKey = typeof(TEntity).FullName + "|" + Dialect.Name + "|insert";
        var insertSql = TinyOrm.Runtime.Execution.CompiledQueryCache.GetOrAdd(shapeKey, () => entry.BuildInsert(Dialect));
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = insertSql;
        entry.BindInsert(cmd, entity!, Dialect);
        if (EnableSqlLogging && SqlLogger is not null)
        {
            var list = new List<KeyValuePair<string, object?>>();
            foreach (DbParameter p in cmd.Parameters) list.Add(new KeyValuePair<string, object?>(p.ParameterName, p.Value));
            LogSql(insertSql, list);
            foreach (DbParameter p in cmd.Parameters)
                System.Console.WriteLine("TYPE " + p.ParameterName + ": " + p.DbType + ", CLR=" + (p.Value?.GetType().FullName ?? "<null>"));
        }
        configure?.Invoke(cmd);
        if (Connection.State != ConnectionState.Open)
            Connection.Open();
        try
        {
            return cmd.ExecuteNonQuery();
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine("EXECUTE ERROR: " + ex.Message);
            System.Console.WriteLine(insertSql);
            foreach (DbParameter p in cmd.Parameters)
                System.Console.WriteLine("PARAM " + p.ParameterName + ": DbType=" + p.DbType + ", CLR=" + (p.Value?.GetType().FullName ?? "<null>") + ", Value=" + (p.Value ?? "<null>"));
            throw;
        }
    }

    public int BulkInsert<TEntity>(IEnumerable<TEntity> entities, Action<DbCommand>? configure = null) where TEntity : class, new()
    {
        var entry = MappingRegistry.Get<TEntity>();
        if (Dialect.SupportsMultiValuesInsert)
        {
            var cols = entry.Columns.Where(c => !c.IsComputed && !c.IsIdentity).Select(c => c.Col).ToArray();
            var table = Dialect.QuoteTable(entry.Table, entry.Schema);
            var colList = string.Join(", ", cols.Select(Dialect.QuoteIdentifier));
            var listEntities = entities.ToList();
            var valuesRows = new StringBuilder();
            for (int i = 0; i < listEntities.Count; i++)
            {
                var prefix = "p" + i + "_";
                var row = string.Join(", ", cols.Select(c => Dialect.Parameter(prefix + c)));
                valuesRows.Append("(").Append(row).Append(")");
                if (i < listEntities.Count - 1) valuesRows.Append(", ");
            }
            var sql = $"INSERT INTO {table} ({colList}) VALUES {valuesRows}";
            using var tx = Connection.BeginTransaction();
            using var cmd = Connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            for (int i = 0; i < listEntities.Count; i++)
            {
                var e = listEntities[i]!;
                var vals = entry.ExtractInsertValues(e!);
                foreach (var v in vals)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = Dialect.Parameter("p" + i + "_" + v.Col);
                    p.Value = v.Val ?? DBNull.Value;
                    p.DbType = Dialect.MapClrType(v.Type);
                    cmd.Parameters.Add(p);
                }
            }
            if (EnableSqlLogging && SqlLogger is not null)
            {
                var list = new List<KeyValuePair<string, object?>>();
                foreach (DbParameter p in cmd.Parameters) list.Add(new KeyValuePair<string, object?>(p.ParameterName, p.Value));
                LogSql(sql, list);
            }
            configure?.Invoke(cmd);
            var res = cmd.ExecuteNonQuery();
            tx.Commit();
            return res;
        }
        else
        {
            var shapeKey = typeof(TEntity).FullName + "|" + Dialect.Name + "|insert";
            var insertSql = TinyOrm.Runtime.Execution.CompiledQueryCache.GetOrAdd(shapeKey, () => entry.BuildInsert(Dialect));
            using var tx = Connection.BeginTransaction();
            int affected = 0;
            foreach (var e in entities)
            {
                using var cmd = Connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = insertSql;
                entry.BindInsert(cmd, e!, Dialect);
                if (EnableSqlLogging && SqlLogger is not null)
                {
                    var list = new List<KeyValuePair<string, object?>>();
                    foreach (DbParameter p in cmd.Parameters) list.Add(new KeyValuePair<string, object?>(p.ParameterName, p.Value));
                    LogSql(insertSql, list);
                }
                configure?.Invoke(cmd);
                affected += cmd.ExecuteNonQuery();
            }
            tx.Commit();
            return affected;
        }
    }

    public int Update<TEntity>(TEntity entity, Action<DbCommand>? configure = null) where TEntity : class, new()
    {
        var entry = MappingRegistry.Get<TEntity>();
        var shapeKey = typeof(TEntity).FullName + "|" + Dialect.Name + "|update";
        var sql = TinyOrm.Runtime.Execution.CompiledQueryCache.GetOrAdd(shapeKey, () => entry.BuildUpdate(Dialect));
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        entry.BindUpdate(cmd, entity!, Dialect);
        if (EnableSqlLogging && SqlLogger is not null)
        {
            var list = new List<KeyValuePair<string, object?>>();
            foreach (DbParameter p in cmd.Parameters) list.Add(new KeyValuePair<string, object?>(p.ParameterName, p.Value));
            LogSql(sql, list);
        }
        configure?.Invoke(cmd);
        if (Connection.State != ConnectionState.Open)
            Connection.Open();
        return cmd.ExecuteNonQuery();
    }

    public int Delete<TEntity>(TEntity entity, Action<DbCommand>? configure = null) where TEntity : class, new()
    {
        var entry = MappingRegistry.Get<TEntity>();
        var shapeKey = typeof(TEntity).FullName + "|" + Dialect.Name + "|delete";
        var sql = TinyOrm.Runtime.Execution.CompiledQueryCache.GetOrAdd(shapeKey, () => entry.BuildDelete(Dialect));
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        entry.BindDelete(cmd, entity!, Dialect);
        if (EnableSqlLogging && SqlLogger is not null)
        {
            var list = new List<KeyValuePair<string, object?>>();
            foreach (DbParameter p in cmd.Parameters) list.Add(new KeyValuePair<string, object?>(p.ParameterName, p.Value));
            LogSql(sql, list);
        }
        configure?.Invoke(cmd);
        if (Connection.State != ConnectionState.Open)
            Connection.Open();
        return cmd.ExecuteNonQuery();
    }

    internal DbCommand CreateCommand(string sql, IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var kv in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = Dialect.Parameter(kv.Key.TrimStart('@'));
            p.Value = kv.Value ?? DBNull.Value;
            p.DbType = Dialect.MapClrType(kv.Value?.GetType() ?? typeof(object));
            cmd.Parameters.Add(p);
        }
        LogSql(sql, parameters);
        return cmd;
    }

    public int ExecuteRaw(string sql, IEnumerable<KeyValuePair<string, object?>> parameters, DbTransaction? tx = null)
    {
        using var cmd = CreateCommand(sql, parameters);
        if (tx is not null) cmd.Transaction = tx;
        if (Connection.State != ConnectionState.Open) Connection.Open();
        return cmd.ExecuteNonQuery();
    }

    public IEnumerable<TEntity> QueryRaw<TEntity>(string sql, IEnumerable<KeyValuePair<string, object?>> parameters, ITinyRowMaterializer<TEntity> materializer)
        where TEntity : class, new()
    {
        using var cmd = CreateCommand(sql, parameters);
        if (Connection.State != ConnectionState.Open) Connection.Open();
        using var reader = cmd.ExecuteReader();
        materializer.Initialize(reader);
        var list = new List<TEntity>();
        while (reader.Read()) list.Add(materializer.Read(reader));
        return list;
    }

    internal void LogSql(string sql, IEnumerable<KeyValuePair<string, object?>>? bindings)
    {
        if (!EnableSqlLogging || SqlLogger is null) return;
        if (bindings is null)
        {
            SqlLogger(sql);
            return;
        }
        var sb = new StringBuilder();
        sb.Append(sql);
        sb.Append(" | ");
        bool first = true;
        foreach (var kv in bindings)
        {
            if (!first) sb.Append(", "); first = false;
            sb.Append(kv.Key).Append("=");
            sb.Append(kv.Value is null ? "<null>" : kv.Value);
        }
        SqlLogger(sb.ToString());
    }
}
