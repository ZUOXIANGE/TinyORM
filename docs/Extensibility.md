# 扩展：自定义方言适配器

TinyOrm 通过实现 `IDialectAdapter` 来扩展新的数据库方言，控制 SQL 编译器、标识符与表名引用、参数占位符与类型映射，以及是否支持多值 INSERT。

## 适配器接口

- 接口定义位置：`src/TinyOrm.Dialects/IDialectAdapter.cs:9-30`
- 需实现成员：
  - `Compiler Compiler`
  - `string Name`
  - `DbType MapClrType(Type type)`
  - `string QuoteIdentifier(string name)`
  - `string QuoteTable(string table, string? schema = null)`
  - `string Parameter(string name)`
  - `bool SupportsMultiValuesInsert`

## 最小实现示例

```csharp
using SqlKata.Compilers;
using System.Data;
using TinyOrm.Dialects;

public sealed class CustomDialectAdapter : IDialectAdapter
{
    public Compiler Compiler { get; } = new SqlServerCompiler();
    public string Name => "Custom";

    public DbType MapClrType(Type type)
    {
        if (type == typeof(int)) return DbType.Int32;
        if (type == typeof(long)) return DbType.Int64;
        if (type == typeof(string)) return DbType.String;
        if (type == typeof(DateTime)) return DbType.DateTime2;
        return DbType.Object;
    }

    public string QuoteIdentifier(string name) => "[" + name + "]";

    public string QuoteTable(string table, string? schema = null)
        => string.IsNullOrEmpty(schema) ? QuoteIdentifier(table) : QuoteIdentifier(schema) + "." + QuoteIdentifier(table);

    public string Parameter(string name) => "@" + name;

    public bool SupportsMultiValuesInsert => true;
}
```

## 在上下文中使用

```csharp
var ctx = new TinyOrm.Runtime.Context.TinyOrmContext(connection, new CustomDialectAdapter());
```

## 注意事项

- 选择合适的 `Compiler`（`SqlServerCompiler/MySqlCompiler/PostgresCompiler/SqliteCompiler` 或自定义）
- `MapClrType` 与数据库驱动参数类型对齐
- `QuoteIdentifier/QuoteTable` 遵循目标数据库的引用规则
- `Parameter` 返回正确的占位符前缀（如 `@name` / `:name` / `$name`）
- 根据数据库是否支持多值 INSERT 设置 `SupportsMultiValuesInsert`