# 构建与测试

## 构建

```bash
dotnet build -warnaserror
```

- 无警告构建：核心工程与示例、测试均可在 `-warnaserror` 下构建通过

## 测试

```bash
dotnet test -v minimal
```

- 过滤单个测试类

```bash
dotnet test tests/TinyOrm.Tests/TinyOrm.Tests.csproj --filter FullyQualifiedName~TinyOrm.Tests.Integration.SqliteCrudTests -v minimal
```

- 数据库连接环境变量
  - `TINYORM_SQLSERVER`
  - `TINYORM_MYSQL`
  - `TINYORM_POSTGRES`

无连接字符串或连接失败时，相关测试将安全跳过，避免环境干扰。

## 日志测试

- `SqlLoggingTests` 验证日志覆盖建表、插入、批量插入、查询、更新、删除、原生 SQL（`tests/TinyOrm.Tests/Integration/SqlLoggingTests.cs`）