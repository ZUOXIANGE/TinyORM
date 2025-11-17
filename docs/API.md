# TinyOrm API 参考

## TinyOrmContext

- 属性
  - `DbConnection Connection`
  - `IDialectAdapter Dialect`
  - `bool EnableSqlLogging`
  - `Action<string>? SqlLogger`
- 方法
  - `DbTransaction BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)`（src/TinyOrm.Runtime/Context/TinyOrmContext.cs:34-36）
  - `TinyQueryable<TEntity> Query<TEntity>()`（src/TinyOrm.Runtime/Context/TinyOrmContext.cs:38-45）
  - `int Insert<TEntity>(TEntity entity, Action<DbCommand>? configure = null)`（src/TinyOrm.Runtime/Context/TinyOrmContext.cs:47-59）
  - `int BulkInsert<TEntity>(IEnumerable<TEntity> entities, Action<DbCommand>? configure = null)`（src/TinyOrm.Runtime/Context/TinyOrmContext.cs:61-119）
  - `int Update<TEntity>(TEntity entity, Action<DbCommand>? configure = null)`（src/TinyOrm.Runtime/Context/TinyOrmContext.cs:121-133）
  - `int Delete<TEntity>(TEntity entity, Action<DbCommand>? configure = null)`（src/TinyOrm.Runtime/Context/TinyOrmContext.cs:135-147）
  - `int ExecuteRaw(string sql, IEnumerable<KeyValuePair<string, object?>> parameters, DbTransaction? tx = null)`（src/TinyOrm.Runtime/Context/TinyOrmContext.cs:164-170）
  - `IEnumerable<TEntity> QueryRaw<TEntity>(string sql, IEnumerable<KeyValuePair<string, object?>> parameters, ITinyRowMaterializer<TEntity> materializer)`（src/TinyOrm.Runtime/Context/TinyOrmContext.cs:172-181）

## TinyQueryable<TEntity>

- 过滤与排序
  - `Where(Field<TEntity,TProperty> field, TinyOperator op, TProperty value)`（src/TinyOrm.Runtime/Query/TinyQueryable.cs:32-63）
  - `Where(Expression<Func<TEntity,bool>> predicate)`（src/TinyOrm.Runtime/Query/TinyQueryable.cs:79-83）
  - `OrderBy/OrderByDesc/ThenBy/ThenByDesc`（src/TinyOrm.Runtime/Query/TinyQueryable.cs:85-123）
  - `Skip/Take`（src/TinyOrm.Runtime/Query/TinyQueryable.cs:141-143, 125-133）
- 分组与 Having
  - `GroupBy` 支持单列与多列（src/TinyOrm.Runtime/Query/TinyQueryable.cs:149-170）
  - `Having(Expression<Func<TEntity,bool>> predicate)`（src/TinyOrm.Runtime/Query/TinyQueryable.cs:309-313）
- 联接
  - `Join/InnerJoin/LeftJoin/RightJoin/Include`（src/TinyOrm.Runtime/Query/TinyQueryable.cs:198-264）
- 聚合与标量
  - `Count()`（src/TinyOrm.Runtime/Query/TinyQueryable.cs:315-335）
  - `Max/Min/Sum/Avg`（src/TinyOrm.Runtime/Query/TinyQueryable.cs:337-361）
- 投影与执行
  - `Select(params string[] columns)`（src/TinyOrm.Runtime/Query/TinyQueryable.cs:143-147）
  - `SelectDto<TDto>(Expression<Func<TEntity,TDto>> projection)`（src/TinyOrm.Runtime/Query/TinyQueryable.cs:288-307）
  - `ToList()`（src/TinyOrm.Runtime/Query/TinyQueryable.cs:172-196）
  - `ToRows()`（src/TinyOrm.Runtime/Query/TinyQueryable.cs:265-286）

## TinyRow

- 用于 `ToRows()` 的轻量结果类型，按列名索引取值（src/TinyOrm.Runtime/Query/TinyRow.cs:10-25）

## IDialectAdapter

- 职责与方法（src/TinyOrm.Dialects/IDialectAdapter.cs:9-30）
  - `Compiler Compiler`
  - `string Name`
  - `DbType MapClrType(Type type)`
  - `string QuoteIdentifier(string name)`
  - `string QuoteTable(string table, string? schema = null)`
  - `string Parameter(string name)`
  - `bool SupportsMultiValuesInsert`

## MappingRegistry 与 EntityMapEntry

- 注册与获取映射（src/TinyOrm.Runtime/Mapping/MappingRegistry.cs:31-42）
- `EntityMapEntry` 字段（src/TinyOrm.Runtime/Mapping/MappingRegistry.cs:16-29）
  - `Table`、`Schema`、`Columns`
  - `BuildInsert/BindInsert`
  - `BuildUpdate/BindUpdate`
  - `BuildDelete/BindDelete`
  - `MaterializerFactory`
  - `ExtractInsertValues`

## CompiledQueryCache

- 形状键缓存编译 SQL（src/TinyOrm.Runtime/Execution/CompiledQueryCache.cs:12-13）