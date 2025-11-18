using System.Data;
using System.Data.Common;
using System.Text;
using System.Linq;
using System.Linq.Expressions;

using TinyOrm.Dialects;
using TinyOrm.Runtime.Mapping;
using TinyOrm.Abstractions.Core;

namespace TinyOrm.Runtime.Context;

/// <summary>
/// 提供数据库访问与查询执行的上下文。统一通过该上下文进行增删改查、
/// 原始 SQL 执行以及日志记录，屏蔽不同数据库方言的差异。
/// </summary>
public sealed class TinyOrmContext
{
    /// <summary>数据库连接对象。</summary>
    public DbConnection Connection { get; }

    /// <summary>数据库方言适配器。</summary>
    public IDialectAdapter Dialect { get; }

    /// <summary>是否启用 SQL 日志记录。</summary>
    public bool EnableSqlLogging { get; set; }

    /// <summary>自定义 SQL 日志输出委托。</summary>
    public Action<string>? SqlLogger { get; set; }

    /// <summary>
    /// 创建上下文实例。
    /// </summary>
    /// <param name="connection">数据库连接。</param>
    /// <param name="dialect">数据库方言适配器。</param>
    public TinyOrmContext(DbConnection connection, IDialectAdapter dialect)
    {
        Connection = connection;
        Dialect = dialect;
        EnableSqlLogging = true;
        SqlLogger = s => System.Console.WriteLine(s);
    }

    /// <summary>开始一个事务。</summary>
    /// <param name="isolationLevel">事务隔离级别。</param>
    public DbTransaction BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        => Connection.BeginTransaction(isolationLevel);

    /// <summary>创建针对实体的强类型查询。</summary>
    public TinyOrm.Runtime.Query.TinyQueryable<TEntity> Query<TEntity>() where TEntity : class, new()
    {
        var entry = MappingRegistry.Get<TEntity>();
        var q = new SqlKata.Query(entry.Table);
        var matObj = entry.MaterializerFactory();
        var materializer = (TinyOrm.Abstractions.Core.ITinyRowMaterializer<TEntity>)matObj;
        return new TinyOrm.Runtime.Query.TinyQueryable<TEntity>(this, q, materializer, entry.Table);
    }

    /// <summary>插入单个实体。</summary>
    /// <param name="entity">要插入的实体实例。</param>
    /// <param name="configure">可选的命令配置（如事务）。</param>
    /// <returns>受影响的行数。</returns>
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

    /// <summary>批量插入实体集合。</summary>
    /// <param name="entities">要插入的实体集合。</param>
    /// <param name="configure">可选的命令配置（如事务）。</param>
    /// <returns>受影响的总行数。</returns>
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

    /// <summary>按实体主键更新整行数据。</summary>
    /// <param name="entity">要更新的实体实例（需包含主键）。</param>
    /// <param name="configure">可选的命令配置（如事务）。</param>
    /// <returns>受影响的行数。</returns>
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

    /// <summary>仅更新指定字段，使用主键进行定位。</summary>
    /// <param name="entity">包含主键与待更新字段值的实体。</param>
    /// <param name="fields">要更新的字段选择器。</param>
    /// <returns>受影响的行数。</returns>
    public int UpdateFields<TEntity>(TEntity entity, params Expression<Func<TEntity, object?>>[] fields) where TEntity : class, new()
    {
        var entry = MappingRegistry.Get<TEntity>();
        var props = new List<string>();
        foreach (var f in fields)
        {
            var name = GetPropertyNameFromSelector(f.Body);
            if (!string.IsNullOrEmpty(name)) props.Add(name);
        }
        var setCols = new List<string>();
        foreach (var p in props.Distinct(StringComparer.Ordinal))
        {
            var col = entry.GetColumn(p);
            var meta = entry.Columns.FirstOrDefault(c => c.Prop == p);
            if (meta is null) continue;
            if (meta.IsComputed || meta.IsIdentity || meta.IsKey) continue;
            setCols.Add(col);
        }
        if (setCols.Count == 0) return Update(entity, null);
        var shapeKey = typeof(TEntity).FullName + "|" + Dialect.Name + "|update_fields|" + string.Join(",", setCols.OrderBy(s => s, StringComparer.Ordinal));
        var sql = TinyOrm.Runtime.Execution.CompiledQueryCache.GetOrAdd(shapeKey, () =>
        {
            var table = Dialect.QuoteTable(entry.Table, entry.Schema);
            var setList = string.Join(", ", setCols.Select(c => Dialect.QuoteIdentifier(c) + " = " + Dialect.Parameter("p_" + c)));
            var keyCols = entry.Columns.Where(c => c.IsKey).Select(c => c.Col).ToArray();
            var whereList = string.Join(" AND ", keyCols.Select(c => Dialect.QuoteIdentifier(c) + " = " + Dialect.Parameter("p_" + c)));
            return "UPDATE " + table + " SET " + setList + " WHERE " + whereList;
        });
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        entry.BindUpdate(cmd, entity!, Dialect);
        if (EnableSqlLogging && SqlLogger is not null)
        {
            var list = new List<KeyValuePair<string, object?>>();
            foreach (DbParameter p in cmd.Parameters) list.Add(new KeyValuePair<string, object?>(p.ParameterName, p.Value));
            LogSql(sql, list);
        }
        
        if (Connection.State != ConnectionState.Open)
            Connection.Open();
        return cmd.ExecuteNonQuery();
    }

    /// <summary>仅更新指定字段，允许自定义命令配置（如设置事务）。</summary>
    /// <param name="entity">包含主键与待更新字段值的实体。</param>
    /// <param name="configure">命令配置委托。</param>
    /// <param name="fields">要更新的字段选择器。</param>
    /// <returns>受影响的行数。</returns>
    public int UpdateFieldsWith<TEntity>(TEntity entity, Action<DbCommand> configure, params Expression<Func<TEntity, object?>>[] fields) where TEntity : class, new()
    {
        var entry = MappingRegistry.Get<TEntity>();
        var props = new List<string>();
        foreach (var f in fields)
        {
            var name = GetPropertyNameFromSelector(f.Body);
            if (!string.IsNullOrEmpty(name)) props.Add(name);
        }
        var setCols = new List<string>();
        foreach (var p in props.Distinct(StringComparer.Ordinal))
        {
            var col = entry.GetColumn(p);
            var meta = entry.Columns.FirstOrDefault(c => c.Prop == p);
            if (meta is null) continue;
            if (meta.IsComputed || meta.IsIdentity || meta.IsKey) continue;
            setCols.Add(col);
        }
        if (setCols.Count == 0) return Update(entity, configure);
        var shapeKey = typeof(TEntity).FullName + "|" + Dialect.Name + "|update_fields|" + string.Join(",", setCols.OrderBy(s => s, StringComparer.Ordinal));
        var sql = TinyOrm.Runtime.Execution.CompiledQueryCache.GetOrAdd(shapeKey, () =>
        {
            var table = Dialect.QuoteTable(entry.Table, entry.Schema);
            var setList = string.Join(", ", setCols.Select(c => Dialect.QuoteIdentifier(c) + " = " + Dialect.Parameter("p_" + c)));
            var keyCols = entry.Columns.Where(c => c.IsKey).Select(c => c.Col).ToArray();
            var whereList = string.Join(" AND ", keyCols.Select(c => Dialect.QuoteIdentifier(c) + " = " + Dialect.Parameter("p_" + c)));
            return "UPDATE " + table + " SET " + setList + " WHERE " + whereList;
        });
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

    /// <summary>按条件更新指定字段。</summary>
    /// <param name="entity">携带待更新字段值的实体（无需主键）。</param>
    /// <param name="where">更新条件表达式。</param>
    /// <param name="fields">要更新的字段选择器。</param>
    /// <returns>受影响的行数。</returns>
    public int UpdateSet<TEntity>(TEntity entity, Expression<Func<TEntity, bool>> where, params Expression<Func<TEntity, object?>>[] fields) where TEntity : class, new()
        => UpdateSetWith(entity, where, null, fields);

    /// <summary>按条件更新指定字段，允许自定义命令配置（如设置事务）。</summary>
    /// <param name="entity">携带待更新字段值的实体（无需主键）。</param>
    /// <param name="where">更新条件表达式。</param>
    /// <param name="configure">命令配置委托。</param>
    /// <param name="fields">要更新的字段选择器。</param>
    /// <returns>受影响的行数。</returns>
    public int UpdateSetWith<TEntity>(TEntity entity, Expression<Func<TEntity, bool>> where, Action<DbCommand>? configure, params Expression<Func<TEntity, object?>>[] fields) where TEntity : class, new()
    {
        var entry = MappingRegistry.Get<TEntity>();
        var props = new List<string>();
        foreach (var f in fields)
        {
            var name = GetPropertyNameFromSelector(f.Body);
            if (!string.IsNullOrEmpty(name)) props.Add(name);
        }
        var setCols = new List<string>();
        foreach (var p in props.Distinct(StringComparer.Ordinal))
        {
            var col = entry.GetColumn(p);
            var meta = entry.Columns.FirstOrDefault(c => c.Prop == p);
            if (meta is null) continue;
            if (meta.IsComputed || meta.IsIdentity || meta.IsKey) continue;
            setCols.Add(col);
        }
        var table = Dialect.QuoteTable(entry.Table, entry.Schema);
        var setList = string.Join(", ", setCols.Select(c => Dialect.QuoteIdentifier(c) + " = " + Dialect.Parameter("p_" + c)));
        var whereParams = new List<(string Name, object? Val, Type Type)>();
        var whereClause = BuildWhereClause<TEntity>(where.Body, whereParams);
        var sql = "UPDATE " + table + " SET " + setList + " WHERE " + whereClause;
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        entry.BindUpdate(cmd, entity!, Dialect);
        for (int i = 0; i < whereParams.Count; i++)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = Dialect.Parameter(whereParams[i].Name);
            p.Value = whereParams[i].Val ?? DBNull.Value;
            p.DbType = Dialect.MapClrType(whereParams[i].Type);
            cmd.Parameters.Add(p);
        }
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

    /// <summary>构建 WHERE 子句并收集参数。</summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="expr">条件表达式。</param>
    /// <param name="parameters">输出的参数集合。</param>
    /// <returns>WHERE 子句字符串。</returns>
    private string BuildWhereClause<TEntity>(Expression expr, List<(string Name, object? Val, Type Type)> parameters) where TEntity : class, new()
    {
        if (expr is BinaryExpression be)
        {
            if (be.NodeType == ExpressionType.AndAlso)
            {
                var l = BuildWhereClause<TEntity>(be.Left, parameters);
                var r = BuildWhereClause<TEntity>(be.Right, parameters);
                return "(" + l + ") AND (" + r + ")";
            }
            if (be.NodeType == ExpressionType.OrElse)
            {
                var l = BuildWhereClause<TEntity>(be.Left, parameters);
                var r = BuildWhereClause<TEntity>(be.Right, parameters);
                return "(" + l + ") OR (" + r + ")";
            }
            if (be.NodeType is ExpressionType.Equal or ExpressionType.NotEqual or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or ExpressionType.LessThan or ExpressionType.LessThanOrEqual)
            {
                MemberExpression? me = null;
                Expression? other = null;
                if (be.Left is MemberExpression lm && lm.Expression is ParameterExpression) { me = lm; other = be.Right; }
                else if (be.Right is MemberExpression rm && rm.Expression is ParameterExpression) { me = rm; other = be.Left; }
                if (me is not null)
                {
                    var prop = me.Member.Name;
                    var col = MappingRegistry.Get<TEntity>().GetColumn(prop);
                    object? val;
                    Type vt;
                    if (other is ConstantExpression c) { val = c.Value; vt = c.Value?.GetType() ?? typeof(object); }
                    else if (other is UnaryExpression ue && ue.NodeType == ExpressionType.Convert && ue.Operand is ConstantExpression uc) { val = uc.Value; vt = uc.Value?.GetType() ?? typeof(object); }
                    else throw new NotSupportedException("Unsupported where expression");
                    var name = "cond" + parameters.Count;
                    parameters.Add((name, val, vt));
                    var op = be.NodeType switch
                    {
                        ExpressionType.Equal => " = ",
                        ExpressionType.NotEqual => " <> ",
                        ExpressionType.GreaterThan => " > ",
                        ExpressionType.GreaterThanOrEqual => " >= ",
                        ExpressionType.LessThan => " < ",
                        ExpressionType.LessThanOrEqual => " <= ",
                        _ => throw new NotSupportedException("Unsupported where expression")
                    };
                    return Dialect.QuoteIdentifier(col) + op + Dialect.Parameter(name);
                }
            }
        }
        if (expr is MethodCallExpression mc)
        {
            if (mc.Object is MemberExpression mm && mm.Expression is ParameterExpression)
            {
                var prop = mm.Member.Name;
                var col = MappingRegistry.Get<TEntity>().GetColumn(prop);
                if (mc.Method.Name == "Contains" && mc.Arguments.Count == 1)
                {
                    if (mc.Arguments[0] is ConstantExpression c)
                    {
                        var name = "cond" + parameters.Count;
                        var v = c.Value?.ToString() ?? string.Empty;
                        parameters.Add((name, "%" + v + "%", typeof(string)));
                        return Dialect.QuoteIdentifier(col) + " LIKE " + Dialect.Parameter(name);
                    }
                }
                if (mc.Method.Name == "StartsWith" && mc.Arguments.Count == 1)
                {
                    if (mc.Arguments[0] is ConstantExpression c)
                    {
                        var name = "cond" + parameters.Count;
                        var v = c.Value?.ToString() ?? string.Empty;
                        parameters.Add((name, v + "%", typeof(string)));
                        return Dialect.QuoteIdentifier(col) + " LIKE " + Dialect.Parameter(name);
                    }
                }
                if (mc.Method.Name == "EndsWith" && mc.Arguments.Count == 1)
                {
                    if (mc.Arguments[0] is ConstantExpression c)
                    {
                        var name = "cond" + parameters.Count;
                        var v = c.Value?.ToString() ?? string.Empty;
                        parameters.Add((name, "%" + v, typeof(string)));
                        return Dialect.QuoteIdentifier(col) + " LIKE " + Dialect.Parameter(name);
                    }
                }
            }
            if (mc.Object is null && mc.Method.Name == "Contains" && mc.Arguments.Count == 2)
            {
                if (mc.Arguments[0] is ConstantExpression set && mc.Arguments[1] is MemberExpression rm && rm.Expression is ParameterExpression)
                {
                    var prop = rm.Member.Name;
                    var col = MappingRegistry.Get<TEntity>().GetColumn(prop);
                    var vals = (set.Value as System.Collections.IEnumerable)?.Cast<object?>().ToArray() ?? Array.Empty<object?>();
                    var parts = new List<string>();
                    for (int i = 0; i < vals.Length; i++)
                    {
                        var name = "cond" + parameters.Count + "_" + i;
                        parameters.Add((name, vals[i], vals[i]?.GetType() ?? typeof(object)));
                        parts.Add(Dialect.Parameter(name));
                    }
                    var inList = string.Join(", ", parts);
                    return Dialect.QuoteIdentifier(col) + " IN (" + inList + ")";
                }
            }
        }
        throw new NotSupportedException("Unsupported where expression");
    }

    /// <summary>从选择器表达式中提取属性名称。</summary>
    private static string GetPropertyNameFromSelector(Expression expr)
    {
        if (expr is UnaryExpression ue && ue.NodeType == ExpressionType.Convert) return GetPropertyNameFromSelector(ue.Operand);
        if (expr is MemberExpression me && me.Expression is ParameterExpression) return me.Member.Name;
        throw new NotSupportedException("Unsupported key selector expression");
    }

    /// <summary>按主键删除实体。</summary>
    /// <param name="entity">包含主键的实体。</param>
    /// <param name="configure">可选的命令配置（如事务）。</param>
    /// <returns>受影响的行数。</returns>
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

    /// <summary>创建并填充命令参数的辅助方法。</summary>
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

    /// <summary>执行原始 SQL（非查询）。</summary>
    public int ExecuteRaw(string sql, IEnumerable<KeyValuePair<string, object?>> parameters, DbTransaction? tx = null)
    {
        using var cmd = CreateCommand(sql, parameters);
        if (tx is not null) cmd.Transaction = tx;
        if (Connection.State != ConnectionState.Open) Connection.Open();
        return cmd.ExecuteNonQuery();
    }

    /// <summary>执行原始查询并使用物化器将行转换为实体。</summary>
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

    /// <summary>记录 SQL 及绑定参数。</summary>
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
