# 方言适配器与数据库支持

TinyOrm 通过 `IDialectAdapter` 适配不同数据库的 SQL 编译、参数占位符、类型映射与标识符引用，同时声明是否支持多值 INSERT。

## 支持的数据库

- SqlServer：`TinyOrm.Dialects.Adapters.SqlServerDialectAdapter`
- MySql：`TinyOrm.Dialects.Adapters.MySqlDialectAdapter`
- PostgreSQL：`TinyOrm.Dialects.Adapters.PostgresDialectAdapter`
- SQLite：`TinyOrm.Dialects.Adapters.SqliteDialectAdapter`

## 连接与上下文示例

```csharp
// SqlServer
using Microsoft.Data.SqlClient;
var conn = new SqlConnection(Environment.GetEnvironmentVariable("TINYORM_SQLSERVER")!);
var ctx = new TinyOrm.Runtime.Context.TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqlServerDialectAdapter());

// MySql
using MySqlConnector;
var conn = new MySqlConnection(Environment.GetEnvironmentVariable("TINYORM_MYSQL")!);
var ctx = new TinyOrm.Runtime.Context.TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.MySqlDialectAdapter());

// PostgreSQL
using Npgsql;
var conn = new NpgsqlConnection(Environment.GetEnvironmentVariable("TINYORM_POSTGRES")!);
var ctx = new TinyOrm.Runtime.Context.TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.PostgresDialectAdapter());

// SQLite
using Microsoft.Data.Sqlite;
var conn = new SqliteConnection("Data Source=:memory:");
var ctx = new TinyOrm.Runtime.Context.TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter());
```

## 参数占位符与标识符引用

- 由适配器提供 `Parameter(name)`、`QuoteIdentifier(name)` 与 `QuoteTable(table, schema)`（`src/TinyOrm.Dialects/IDialectAdapter.cs:17-27`）
- 运行时创建命令时自动使用占位符与类型映射（`src/TinyOrm.Runtime/Context/TinyOrmContext.cs:149-162`）

## 多值 INSERT 支持

- 某些方言支持 `INSERT INTO ... VALUES (...), (...), ...`，可显著提升批量写效率
- 检测开关：`IDialectAdapter.SupportsMultiValuesInsert`（`src/TinyOrm.Dialects/IDialectAdapter.cs:29`）
- 批量写路径：`TinyOrmContext.BulkInsert<T>`（`src/TinyOrm.Runtime/Context/TinyOrmContext.cs:61-119`）