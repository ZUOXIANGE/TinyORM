# SQL 日志记录

TinyOrm 支持在上下文级别开启 SQL 日志记录，用于开发与调试。日志包含编译后的 SQL 与命名绑定参数。

## 启用方式

```csharp
var ctx = new TinyOrm.Runtime.Context.TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter())
{
    EnableSqlLogging = true,
    SqlLogger = s => Console.WriteLine(s)
};
```

## 覆盖范围

- 原生执行与查询：通过 `CreateCommand` 统一记录（`src/TinyOrm.Runtime/Context/TinyOrmContext.cs:149-162`）
- 查询执行：`ToList/ToRows/Count/Scalar` 编译后记录（`src/TinyOrm.Runtime/Query/TinyQueryable.cs:172-196, 265-286, 315-335, 363-388`）
- 写操作：`Insert/Update/Delete/BulkInsert` 在参数绑定完成后记录（`src/TinyOrm.Runtime/Context/TinyOrmContext.cs:47-147`）

## 日志格式

```
SELECT ... WHERE [persons].[last_name] = @p0 | @p0=Doe
INSERT INTO persons (...) VALUES (...) | @p0_first_name=John, @p0_last_name=Doe, @p0_age=30
```

## 敏感信息与脱敏

- 日志仅用于调试；如包含敏感数据，请在 `SqlLogger` 委托内自行做脱敏或过滤
- 可根据参数名做白名单/黑名单处理，或截断过长文本

示例：

```csharp
ctx.SqlLogger = s =>
{
    var redacted = s.Replace("password=", "password=<redacted>")
                    .Replace("card=", "card=<redacted>");
    if (redacted.Length > 2000) redacted = redacted.Substring(0, 2000) + "...";
    Console.WriteLine(redacted);
};
```