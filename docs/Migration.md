# 仓储到上下文 API 迁移

TinyOrm 统一采用上下文式 API，`Repository<TEntity>` 与 `TinyOrmContext.Set<TEntity>()` 已移除。迁移映射如下：

## 写操作迁移

- `ctx.Set<TEntity>().Insert(table, entity, schema)` → `ctx.Insert(entity)`
- `repo.BulkInsert(entities)` → `ctx.BulkInsert(entities)`
- `repo.Update(entity)` → `ctx.Update(entity)`
- `repo.Delete(entity)` → `ctx.Delete(entity)`

## 查询迁移

- `repo.Query(table, materializer)` → `ctx.Query<TEntity>()`
- 其余查询语法保持不变（Where/OrderBy/GroupBy/Having/SelectDto/ToList/ToRows 等）

## 兼容性与优化

- 原有零反射、预编译 SQL 缓存与批量写优化全部保留
- 方言兼容与参数占位符、标识符引用保持一致（`IDialectAdapter`）

## 参考实现位置

- 上下文写操作：`src/TinyOrm.Runtime/Context/TinyOrmContext.cs:47-147`
- 查询入口：`src/TinyOrm.Runtime/Context/TinyOrmContext.cs:38-45`
- 查询接口与聚合：`src/TinyOrm.Runtime/Query/TinyQueryable.cs`