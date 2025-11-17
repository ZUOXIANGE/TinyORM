using System.Linq.Expressions;
using KataQuery = SqlKata.Query;
using TinyOrm.Abstractions.Core;
using TinyOrm.Runtime.Context;

namespace TinyOrm.Runtime.Query;

/// <summary>
/// Strongly typed query builder for an entity, backed by SqlKata.
/// </summary>
public sealed class TinyQueryable<TEntity> where TEntity : class, new()
{
    private readonly TinyOrmContext _ctx;
    private readonly KataQuery _query;
    private readonly ITinyRowMaterializer<TEntity> _materializer;
    private readonly string _table;

    /// <summary>
    /// Creates a new queryable instance.
    /// </summary>
    public TinyQueryable(TinyOrmContext ctx, KataQuery query, ITinyRowMaterializer<TEntity> materializer, string table)
    {
        _ctx = ctx;
        _query = query;
        _materializer = materializer;
        _table = table;
    }

    /// <summary>Adds a filter predicate using a typed field.</summary>
    public TinyQueryable<TEntity> Where<TProperty>(Field<TEntity, TProperty> field, TinyOperator op, TProperty value)
    {
        switch (op)
        {
            case TinyOperator.Eq:
                _query.Where(field.ColumnName, value);
                break;
            case TinyOperator.NotEq:
                _query.Where(field.ColumnName, "!=", value);
                break;
            case TinyOperator.Gt:
                _query.Where(field.ColumnName, ">", value);
                break;
            case TinyOperator.Ge:
                _query.Where(field.ColumnName, ">=", value);
                break;
            case TinyOperator.Lt:
                _query.Where(field.ColumnName, "<", value);
                break;
            case TinyOperator.Le:
                _query.Where(field.ColumnName, "<=", value);
                break;
            case TinyOperator.In:
                _query.WhereIn(field.ColumnName, (IEnumerable<TProperty>) (object) (value!));
                break;
            case TinyOperator.Like:
                _query.Where(field.ColumnName, "like", value!);
                break;
        }
        return this;
    }

    /// <summary>Orders by a typed field ascending.</summary>
    public TinyQueryable<TEntity> OrderBy<TProperty>(Field<TEntity, TProperty> field)
    {
        _query.OrderBy(field.ColumnName);
        return this;
    }

    /// <summary>Orders by a typed field descending.</summary>
    public TinyQueryable<TEntity> OrderByDesc<TProperty>(Field<TEntity, TProperty> field)
    {
        _query.OrderByDesc(field.ColumnName);
        return this;
    }

    public TinyQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        ApplyPredicate(predicate.Body, false);
        return this;
    }

    public TinyQueryable<TEntity> OrderBy(Expression<Func<TEntity, object>> keySelector)
    {
        var col = GetColumnFromSelector(keySelector.Body);
        _query.OrderBy(col);
        return this;
    }

    public TinyQueryable<TEntity> OrderByDesc(Expression<Func<TEntity, object>> keySelector)
    {
        var col = GetColumnFromSelector(keySelector.Body);
        _query.OrderByDesc(col);
        return this;
    }

    public TinyQueryable<TEntity> ThenBy<TProperty>(Field<TEntity, TProperty> field)
    {
        _query.OrderBy(field.ColumnName);
        return this;
    }

    public TinyQueryable<TEntity> ThenByDesc<TProperty>(Field<TEntity, TProperty> field)
    {
        _query.OrderByDesc(field.ColumnName);
        return this;
    }

    public TinyQueryable<TEntity> ThenBy(Expression<Func<TEntity, object>> keySelector)
    {
        var col = GetColumnFromSelector(keySelector.Body);
        _query.OrderBy(col);
        return this;
    }

    public TinyQueryable<TEntity> ThenByDesc(Expression<Func<TEntity, object>> keySelector)
    {
        var col = GetColumnFromSelector(keySelector.Body);
        _query.OrderByDesc(col);
        return this;
    }

    /// <summary>Limits the number of returned rows.</summary>
    public TinyQueryable<TEntity> Limit(int count)
    {
        _query.Limit(count);
        return this;
    }

    public TinyQueryable<TEntity> Take(int count) => Limit(count);

    /// <summary>Offsets the returned rows.</summary>
    public TinyQueryable<TEntity> Offset(int count)
    {
        _query.Offset(count);
        return this;
    }

    public TinyQueryable<TEntity> Skip(int count) => Offset(count);

    public TinyQueryable<TEntity> Select(params string[] columnNames)
    {
        _query.Select(columnNames);
        return this;
    }

    public TinyQueryable<TEntity> GroupBy<TProperty>(Field<TEntity, TProperty> field)
    {
        _query.GroupBy(field.ColumnName);
        return this;
    }

    public TinyQueryable<TEntity> GroupBy(Expression<Func<TEntity, object>> keySelector)
    {
        var col = GetColumnFromSelector(keySelector.Body);
        _query.GroupBy(col);
        return this;
    }

    public TinyQueryable<TEntity> GroupBy(params Expression<Func<TEntity, object>>[] keySelectors)
    {
        foreach (var ks in keySelectors)
        {
            var col = GetColumnFromSelector(ks.Body);
            _query.GroupBy(col);
        }
        return this;
    }

    /// <summary>Executes the query and returns entities.</summary>
    public IEnumerable<TEntity> ToList()
    {
        var result = _ctx.Dialect.Compiler.Compile(_query);
        _ctx.LogSql(result.Sql, result.NamedBindings);
        using var cmd = _ctx.Connection.CreateCommand();
        cmd.CommandText = result.Sql;
        foreach (var (name, val) in result.NamedBindings)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = val ?? DBNull.Value;
            p.DbType = _ctx.Dialect.MapClrType(val?.GetType() ?? typeof(object));
            cmd.Parameters.Add(p);
        }
        if (_ctx.Connection.State != System.Data.ConnectionState.Open)
            _ctx.Connection.Open();
        using var reader = cmd.ExecuteReader();
        _materializer.Initialize(reader);
        var list = new List<TEntity>();
        while (reader.Read())
        {
            list.Add(_materializer.Read(reader));
        }
        return list;
    }

    public TinyQueryable<TEntity> Join<TOther, TKey>(Field<TEntity, TKey> selfKey, TinyOrm.Abstractions.Core.Field<TOther, TKey> otherKey, string? alias = null)
        where TOther : class, new()
    {
        var otherTable = TinyOrm.Runtime.Mapping.MappingRegistry.Get<TOther>().Table;
        var otherCol = otherKey.ColumnName;
        var selfCol = selfKey.ColumnName;
        var left = (_table + "." + selfCol);
        var right = ((alias ?? otherTable) + "." + otherCol);
        _query.Join(otherTable, right, left);
        return this;
    }

    public TinyQueryable<TEntity> InnerJoin<TOther>(Expression<Func<TEntity, TOther, bool>> on, string? alias = null)
        where TOther : class, new()
    {
        var otherTable = TinyOrm.Runtime.Mapping.MappingRegistry.Get<TOther>().Table;
        var (leftCol, rightCol) = GetJoinColumns<TOther>(on.Body, alias ?? otherTable);
        _query.Join(otherTable, rightCol, leftCol);
        return this;
    }

    public TinyQueryable<TEntity> LeftJoin<TOther>(Expression<Func<TEntity, TOther, bool>> on, string? alias = null)
        where TOther : class, new()
    {
        var otherTable = TinyOrm.Runtime.Mapping.MappingRegistry.Get<TOther>().Table;
        var (leftCol, rightCol) = GetJoinColumns<TOther>(on.Body, alias ?? otherTable);
        _query.LeftJoin(otherTable, rightCol, leftCol);
        return this;
    }

    public TinyQueryable<TEntity> RightJoin<TOther>(Expression<Func<TEntity, TOther, bool>> on, string? alias = null)
        where TOther : class, new()
    {
        var otherTable = TinyOrm.Runtime.Mapping.MappingRegistry.Get<TOther>().Table;
        var (leftCol, rightCol) = GetJoinColumns<TOther>(on.Body, alias ?? otherTable);
        _query.RightJoin(otherTable, rightCol, leftCol);
        return this;
    }

    public TinyQueryable<TEntity> LeftJoin<TOther, TKey>(Field<TEntity, TKey> selfKey, TinyOrm.Abstractions.Core.Field<TOther, TKey> otherKey, string? alias = null)
        where TOther : class, new()
    {
        var otherTable = TinyOrm.Runtime.Mapping.MappingRegistry.Get<TOther>().Table;
        var otherCol = otherKey.ColumnName;
        var selfCol = selfKey.ColumnName;
        var left = (_table + "." + selfCol);
        var right = ((alias ?? otherTable) + "." + otherCol);
        _query.LeftJoin(otherTable, right, left);
        return this;
    }

    public TinyQueryable<TEntity> RightJoin<TOther, TKey>(Field<TEntity, TKey> selfKey, TinyOrm.Abstractions.Core.Field<TOther, TKey> otherKey, string? alias = null)
        where TOther : class, new()
    {
        var otherTable = TinyOrm.Runtime.Mapping.MappingRegistry.Get<TOther>().Table;
        var otherCol = otherKey.ColumnName;
        var selfCol = selfKey.ColumnName;
        var left = (_table + "." + selfCol);
        var right = ((alias ?? otherTable) + "." + otherCol);
        _query.RightJoin(otherTable, right, left);
        return this;
    }

    public TinyQueryable<TEntity> Include<TOther, TKey>(Field<TEntity, TKey> selfKey, TinyOrm.Abstractions.Core.Field<TOther, TKey> otherKey, string? alias = null)
        where TOther : class, new()
        => Join(selfKey, otherKey, alias);

    public IEnumerable<TinyRow> ToRows()
    {
        var result = _ctx.Dialect.Compiler.Compile(_query);
        _ctx.LogSql(result.Sql, result.NamedBindings);
        using var cmd = _ctx.Connection.CreateCommand();
        cmd.CommandText = result.Sql;
        foreach (var (name, val) in result.NamedBindings)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = val ?? DBNull.Value;
            p.DbType = _ctx.Dialect.MapClrType(val?.GetType() ?? typeof(object));
            cmd.Parameters.Add(p);
        }
        if (_ctx.Connection.State != System.Data.ConnectionState.Open)
            _ctx.Connection.Open();
        using var reader = cmd.ExecuteReader();
        var ordinals = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        for (int i=0;i<reader.FieldCount;i++) ordinals[reader.GetName(i)] = i;
        var list = new List<TinyRow>();
        while (reader.Read()) list.Add(new TinyRow(ordinals, reader));
        return list;
    }
    private sealed class DefaultDtoMaterializer<TDto> : TinyOrm.Abstractions.Core.ITinyRowMaterializer<TDto> where TDto : class, new()
    {
        private System.Collections.Generic.Dictionary<string,int> _ord = new System.Collections.Generic.Dictionary<string,int>(System.StringComparer.OrdinalIgnoreCase);
        private System.Reflection.PropertyInfo[] _props = System.Array.Empty<System.Reflection.PropertyInfo>();
        public void Initialize(System.Data.Common.DbDataReader reader)
        {
            _props = typeof(TDto).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var p in _props)
            {
                int o;
                try { o = reader.GetOrdinal(p.Name); }
                catch { continue; }
                _ord[p.Name] = o;
            }
        }
        public TDto Read(System.Data.Common.DbDataReader reader)
        {
            var obj = new TDto();
            foreach (var p in _props)
            {
                if (!_ord.TryGetValue(p.Name, out var o)) continue;
                object? v = reader.IsDBNull(o) ? null : reader.GetValue(o);
                var t = p.PropertyType;
                if (v is null)
                {
                    p.SetValue(obj, null);
                }
                else if (t.IsEnum)
                {
                    if (v is string s) p.SetValue(obj, System.Enum.Parse(t, s));
                    else p.SetValue(obj, System.Enum.ToObject(t, System.Convert.ChangeType(v, System.Enum.GetUnderlyingType(t))!));
                }
                else
                {
                    var tt = System.Nullable.GetUnderlyingType(t) ?? t;
                    p.SetValue(obj, System.Convert.ChangeType(v, tt));
                }
            }
            return obj;
        }
    }

    public TinyQueryable<TDto> SelectDto<TDto>(Expression<Func<TEntity, TDto>> projection)
        where TDto : class, new()
    {
        if (projection.Body is MemberInitExpression mi)
        {
            foreach (var b in mi.Bindings.OfType<MemberAssignment>())
            {
                if (b.Expression is MemberExpression me && me.Expression is ParameterExpression)
                {
                    var col = GetColumnFromMember(me);
                    var alias = b.Member.Name;
                    _query.Select(col + " as " + alias);
                }
            }
        }
        TinyOrm.Abstractions.Core.ITinyRowMaterializer<TDto> mat;
        try
        {
            var matEntry = TinyOrm.Runtime.Mapping.MappingRegistry.Get<TDto>();
            var matObj = matEntry.MaterializerFactory();
            mat = (TinyOrm.Abstractions.Core.ITinyRowMaterializer<TDto>)matObj;
        }
        catch (System.InvalidOperationException)
        {
            mat = new DefaultDtoMaterializer<TDto>();
        }
        return new TinyOrm.Runtime.Query.TinyQueryable<TDto>(_ctx, _query, mat, _table);
    }

    public TinyQueryable<TEntity> Having(Expression<Func<TEntity, bool>> predicate)
    {
        ApplyHavingPredicate(predicate.Body);
        return this;
    }

    public long Count()
    {
        var q = _query.Clone();
        q.ClearComponent("select");
        q.SelectRaw("COUNT(*) AS c");
        var result = _ctx.Dialect.Compiler.Compile(q);
        _ctx.LogSql(result.Sql, result.NamedBindings);
        using var cmd = _ctx.Connection.CreateCommand();
        cmd.CommandText = result.Sql;
        foreach (var (name, val) in result.NamedBindings)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = val ?? DBNull.Value;
            p.DbType = _ctx.Dialect.MapClrType(val?.GetType() ?? typeof(object));
            cmd.Parameters.Add(p);
        }
        if (_ctx.Connection.State != System.Data.ConnectionState.Open)
            _ctx.Connection.Open();
        var obj = cmd.ExecuteScalar();
        return obj is long l ? l : Convert.ToInt64(obj ?? 0);
    }

    public T? Max<T>(Expression<Func<TEntity, object>> keySelector)
    {
        var col = GetColumnFromSelector(keySelector.Body);
        return Scalar<T>("MAX(" + col + ")");
    }

    public T? Min<T>(Expression<Func<TEntity, object>> keySelector)
    {
        var col = GetColumnFromSelector(keySelector.Body);
        return Scalar<T>("MIN(" + col + ")");
    }

    public decimal Sum(Expression<Func<TEntity, object>> keySelector)
    {
        var col = GetColumnFromSelector(keySelector.Body);
        var v = Scalar<object>("SUM(" + col + ")");
        return v is null ? 0m : Convert.ToDecimal(v);
    }

    public double Avg(Expression<Func<TEntity, object>> keySelector)
    {
        var col = GetColumnFromSelector(keySelector.Body);
        var v = Scalar<object>("AVG(" + col + ")");
        return v is null ? 0d : Convert.ToDouble(v);
    }

    private T? Scalar<T>(string select)
    {
        var q = _query.Clone();
        q.ClearComponent("select");
        q.SelectRaw(select + " AS v");
        var result = _ctx.Dialect.Compiler.Compile(q);
        _ctx.LogSql(result.Sql, result.NamedBindings);
        using var cmd = _ctx.Connection.CreateCommand();
        cmd.CommandText = result.Sql;
        foreach (var (name, val) in result.NamedBindings)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = val ?? DBNull.Value;
            p.DbType = _ctx.Dialect.MapClrType(val?.GetType() ?? typeof(object));
            cmd.Parameters.Add(p);
        }
        if (_ctx.Connection.State != System.Data.ConnectionState.Open)
            _ctx.Connection.Open();
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var v = reader.GetValue(0);
            return v is DBNull ? default : (T?)Convert.ChangeType(v, typeof(T));
        }
        return default;
    }

    private void ApplyHavingPredicate(Expression expr)
    {
        if (expr is BinaryExpression be)
        {
            var op = be.NodeType switch
            {
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "!=",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                _ => null
            };
            if (op is null) return;
            if (be.Left is MemberExpression lm && lm.Expression is ParameterExpression && be.Right is ConstantExpression rc)
            {
                var col = GetColumnFromSelector(lm);
                _query.Having(col, op, rc.Value);
                return;
            }
        }
        if (expr is MethodCallExpression mc && mc.Method.Name == "Contains" && mc.Object is MemberExpression me && me.Expression is ParameterExpression && mc.Arguments.Count == 1 && mc.Arguments[0] is ConstantExpression ce)
        {
            var col = GetColumnFromSelector(me);
            var like = "%" + ce.Value + "%";
            _query.Having(col, "like", like);
            return;
        }
    }

    private void ApplyPredicate(Expression expr, bool or)
    {
        if (expr is BinaryExpression be)
        {
            if (be.NodeType == ExpressionType.AndAlso)
            {
                ApplyPredicate(be.Left, false);
                ApplyPredicate(be.Right, false);
                return;
            }
            if (be.NodeType == ExpressionType.OrElse)
            {
                if (TryGetCondition(be.Left, out var lcol, out var lop, out var lval) &&
                    TryGetCondition(be.Right, out var rcol, out var rop, out var rval))
                {
                    System.Console.WriteLine("OR group: " + lcol + " " + lop + " _ OR _ " + rcol + " " + rop);
                    _query.WhereRaw("(" + lcol + " " + lop + " ? OR " + rcol + " " + rop + " ?)", new object?[]{ lval, rval });
                    return;
                }
                System.Console.WriteLine("OR fallback");
                ApplyPredicate(be.Left, false);
                ApplyPredicate(be.Right, true);
                return;
            }
            var op = be.NodeType switch
            {
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "!=",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                _ => null
            };
            if (op is null) return;
            if (be.Left is MemberExpression lm && lm.Expression is ParameterExpression)
            {
                var col = GetColumnFromMember(lm);
                if (be.Right is ConstantExpression rc)
                {
                    System.Console.WriteLine("COND " + (or ? "OR" : "AND") + ": " + col + " " + op + " " + rc.Value);
                    if (or) _query.OrWhere(col, op, rc.Value); else _query.Where(col, op, rc.Value);
                    return;
                }
                if (be.Right is UnaryExpression ur && ur.NodeType == ExpressionType.Convert && ur.Operand is ConstantExpression rconv)
                {
                    System.Console.WriteLine("COND " + (or ? "OR" : "AND") + ": " + col + " " + op + " " + rconv.Value + " (conv)");
                    if (or) _query.OrWhere(col, op, rconv.Value); else _query.Where(col, op, rconv.Value);
                    return;
                }
                System.Console.WriteLine("RIGHT NOT CONST for " + col);
            }
            if (be.Right is MemberExpression rm && rm.Expression is ParameterExpression)
            {
                var col = GetColumnFromMember(rm);
                if (be.Left is ConstantExpression lc)
                {
                    var invOp = op switch
                    {
                        ">" => "<",
                        ">=" => "<=",
                        "<" => ">",
                        "<=" => ">=",
                        _ => op
                    };
                    System.Console.WriteLine("COND " + (or ? "OR" : "AND") + ": " + col + " " + invOp + " " + lc.Value);
                    if (or) _query.OrWhere(col, invOp, lc.Value); else _query.Where(col, invOp, lc.Value);
                    return;
                }
                if (be.Left is UnaryExpression ul && ul.NodeType == ExpressionType.Convert && ul.Operand is ConstantExpression lconv)
                {
                    var invOp = op switch
                    {
                        ">" => "<",
                        ">=" => "<=",
                        "<" => ">",
                        "<=" => ">=",
                        _ => op
                    };
                    System.Console.WriteLine("COND " + (or ? "OR" : "AND") + ": " + col + " " + invOp + " " + lconv.Value + " (conv)");
                    if (or) _query.OrWhere(col, invOp, lconv.Value); else _query.Where(col, invOp, lconv.Value);
                    return;
                }
                else System.Console.WriteLine("LEFT NOT CONST for " + col);
            }
            System.Console.WriteLine("BINARY NOT RECOGNIZED: " + be.NodeType);
        }
        if (expr is MethodCallExpression mc)
        {
            if (mc.Method.Name == "Contains" && mc.Object is MemberExpression me && me.Expression is ParameterExpression && mc.Arguments.Count == 1 && mc.Arguments[0] is ConstantExpression ce)
            {
                var col = GetColumnFromMember(me);
                var like = "%" + ce.Value + "%";
                _query.Where(col, "like", like);
                return;
            }
            if (mc.Method.Name == "StartsWith" && mc.Object is MemberExpression me2 && me2.Expression is ParameterExpression && mc.Arguments.Count == 1 && mc.Arguments[0] is ConstantExpression ce2)
            {
                var col = GetColumnFromMember(me2);
                var like = ce2.Value + "%";
                _query.Where(col, "like", like);
                return;
            }
            if (mc.Method.Name == "EndsWith" && mc.Object is MemberExpression me3 && me3.Expression is ParameterExpression && mc.Arguments.Count == 1 && mc.Arguments[0] is ConstantExpression ce3)
            {
                var col = GetColumnFromMember(me3);
                var like = "%" + ce3.Value;
                _query.Where(col, "like", like);
                return;
            }
            if (mc.Method.Name == "Contains" && mc.Object is null && mc.Arguments.Count == 2 && mc.Arguments[0] is ConstantExpression listConst && mc.Arguments[1] is MemberExpression prop && prop.Expression is ParameterExpression)
            {
                var col = GetColumnFromMember(prop);
                if (listConst.Value is System.Collections.IEnumerable seq)
                {
                    var arr = new System.Collections.Generic.List<object?>();
                    foreach (var x in seq) arr.Add(x);
                    _query.WhereIn(col, arr);
                }
                return;
            }
        }
        if (expr is MemberExpression bm && bm.Expression is ParameterExpression)
        {
            var col = GetColumnFromMember(bm);
            _query.Where(col, true);
        }
    }

    private string GetColumnFromSelector(Expression expr)
    {
        if (expr is UnaryExpression ue && ue.NodeType == ExpressionType.Convert) return GetColumnFromSelector(ue.Operand);
        if (expr is MemberExpression me && me.Expression is ParameterExpression) return GetColumnFromMember(me);
        throw new System.NotSupportedException("Unsupported key selector expression");
    }

    private string GetColumnFromMember(MemberExpression me)
    {
        var prop = me.Member.Name;
        var entry = TinyOrm.Runtime.Mapping.MappingRegistry.Get<TEntity>();
        var col = entry.GetColumn(prop);
        return _table + "." + col;
    }

    private bool TryGetCondition(Expression expr, out string col, out string op, out object? val)
    {
        col = op = string.Empty; val = null;
        if (expr is BinaryExpression be)
        {
            var bop = be.NodeType switch
            {
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "!=",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                _ => null
            };
            if (bop is null) return false;
            if (be.Left is MemberExpression lm && lm.Expression is ParameterExpression && be.Right is ConstantExpression rc)
            {
                col = GetColumnFromMember(lm); op = bop; val = rc.Value; return true;
            }
            if (be.Right is MemberExpression rm && rm.Expression is ParameterExpression && be.Left is ConstantExpression lc)
            {
                var inv = bop switch { ">" => "<", ">=" => "<=", "<" => ">", "<=" => ">=", _ => bop };
                col = GetColumnFromMember(rm); op = inv!; val = lc.Value; return true;
            }
            return false;
        }
        if (expr is MethodCallExpression mc)
        {
            if (mc.Method.Name == "Contains" && mc.Object is MemberExpression me && me.Expression is ParameterExpression && mc.Arguments.Count == 1 && mc.Arguments[0] is ConstantExpression ce)
            { col = GetColumnFromMember(me); op = "like"; val = "%" + ce.Value + "%"; return true; }
            if (mc.Method.Name == "StartsWith" && mc.Object is MemberExpression me2 && me2.Expression is ParameterExpression && mc.Arguments.Count == 1 && mc.Arguments[0] is ConstantExpression ce2)
            { col = GetColumnFromMember(me2); op = "like"; val = ce2.Value + "%"; return true; }
            if (mc.Method.Name == "EndsWith" && mc.Object is MemberExpression me3 && me3.Expression is ParameterExpression && mc.Arguments.Count == 1 && mc.Arguments[0] is ConstantExpression ce3)
            { col = GetColumnFromMember(me3); op = "like"; val = "%" + ce3.Value; return true; }
        }
        return false;
    }

    private (string left, string right) GetJoinColumns<TOther>(Expression expr, string alias) where TOther : class, new()
    {
        if (expr is BinaryExpression be && be.NodeType == ExpressionType.Equal)
        {
            if (be.Left is MemberExpression lm && lm.Expression is ParameterExpression)
            {
                if (be.Right is MemberExpression rm && rm.Expression is ParameterExpression)
                {
                    var lcol = GetColumnFromMember(lm);
                    var rprop = rm.Member.Name;
                    var otherEntry = TinyOrm.Runtime.Mapping.MappingRegistry.Get<TOther>();
                    var rcol = otherEntry.GetColumn(rprop);
                    return (lcol, (alias + "." + rcol));
                }
            }
        }
        throw new System.NotSupportedException("Unsupported join expression");
    }
}
