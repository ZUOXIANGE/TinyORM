# TinyOrm 指南

TinyOrm 是一个轻量级、零反射且 AOT 友好的 .NET ORM。查询接口类似 LINQ，支持联接、分组、聚合、DTO 投影与原生 SQL；写操作统一由上下文 `TinyOrmContext` 提供。

## 快速开始

```csharp
using Microsoft.Data.Sqlite;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TinyOrm.Runtime.Context;

[Table("persons")]
public sealed class Person
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")] public int Id { get; set; }
    [Column("first_name")] public string? FirstName { get; set; }
    [Column("last_name")] public string? LastName { get; set; }
    [Column("age")] public int? Age { get; set; }
}

using var conn = new SqliteConnection("Data Source=:memory:");
conn.Open();
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER);";
    cmd.ExecuteNonQuery();
}

var ctx = new TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter());

// 插入
ctx.Insert(new Person { FirstName = "John", LastName = "Doe", Age = 30 });

// 批量插入（方言支持时使用多值 INSERT）
ctx.BulkInsert(new[]{
    new Person { FirstName = "Jane", LastName = "Doe", Age = 25 },
    new Person { FirstName = "Jim",  LastName = "Beam", Age = 20 }
});

// 查询
var list = ctx.Query<Person>()
    .Where(a => a.LastName == "Doe")
    .OrderBy(a => a.Age)
    .ThenByDesc(a => a.Id)
    .Take(10)
    .ToList();

// 更新
var toUpdate = list.First();
toUpdate.Age = 31;
ctx.Update(toUpdate);

// 删除
ctx.Delete(toUpdate);
```

## 上下文 API（写操作）

- 插入：`ctx.Insert(entity)`
- 批量插入：`ctx.BulkInsert(entities)`（优先使用多值 INSERT，回退为事务循环）
- 更新：`ctx.Update(entity)`
- 删除：`ctx.Delete(entity)`
- 原生执行：`ctx.ExecuteRaw(sql, parameters)`
- 原生查询：`ctx.QueryRaw<TEntity>(sql, parameters, materializer)`

## 查询 API（读操作）

- 入口：`ctx.Query<TEntity>()`
- 过滤：`Where(a => a.Age >= 18)`、支持 `Contains/StartsWith/EndsWith` 映射至 `LIKE`
- 排序：`OrderBy(a => a.Age).ThenByDesc(a => a.Id)`
- 分页：`Skip(20).Take(10)`
- 联接：`InnerJoin/LeftJoin/RightJoin` 与 `Join(Field, Field)`
- 投影到行：`ToRows()`（返回 `TinyRow`，适合选择部分列）
- 投影到实体：`ToList()`（强类型物化器）

## DTO 投影

```csharp
using System.ComponentModel.DataAnnotations.Schema;

[ComplexType]
public sealed class PersonView
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

var dto = ctx.Query<Person>()
    .OrderBy(a => a.Id)
    .SelectDto<PersonView>(a => new PersonView { Id = a.Id, Name = a.FirstName })
    .ToList();
```

- DTO 需以 `ComplexType` 标注
- Source Generator 在编译期生成 DTO 物化器，零反射、AOT 友好

## 聚合与分组 / Having

```csharp
var countDoe = ctx.Query<Person>().Where(a => a.LastName == "Doe").Count();
var sumAgesDoe = ctx.Query<Person>().Where(a => a.LastName == "Doe").Sum(a => a.Age);
var avgAgesDoe = ctx.Query<Person>().Where(a => a.LastName == "Doe").Avg(a => a.Age);
var maxAge = ctx.Query<Person>().Max<int>(a => a.Age);

var groups = ctx.Query<Person>()
    .GroupBy(a => a.LastName)
    .Having(a => a.Age >= 20)
    .ToRows();
```

- `Count/Sum/Avg/Max/Min` 支持按列聚合
- `GroupBy` 支持多列；`Having` 支持比较与 `LIKE`

## 联接与选择

```csharp
var q = ctx.Query<Person>()
    .InnerJoin<City>((p, c) => p.CityId == c.Id)
    .LeftJoin<Province>((p, c, pr) => c.ProvinceId == pr.Id)
    .Select((p, c, pr) => new { p.Id, City = c.Name, Province = pr.Name })
    .Where(x => x.Id > 0);
var rows = q.ToRows();
```

## 批量写优化

- 支持多值 INSERT 的方言：生成单条 `INSERT ... VALUES (...),(...),...`，绑定所有参数后一次执行
- 不支持多值 INSERT 的方言：事务内循环单条 INSERT，行为一致

## 原生 SQL

```csharp
ctx.ExecuteRaw(
    "INSERT INTO persons(first_name,last_name,age) VALUES(@first,@last,@age)",
    new[]{
        new KeyValuePair<string,object?>("first","John"),
        new KeyValuePair<string,object?>("last","Doe"),
        new KeyValuePair<string,object?>("age",30)
    }
);

var rows = ctx.QueryRaw<Person>(
    "SELECT id,first_name,last_name,age FROM persons WHERE last_name=@last",
    new[]{ new KeyValuePair<string,object?>("last","Doe") },
    new PersonMap.Materializer()
);
```

## SQL 日志记录

```csharp
var ctx = new TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter())
{
    EnableSqlLogging = true,
    SqlLogger = s => Console.WriteLine(s)
};

var list = ctx.Query<Person>().Where(a => a.LastName == "Doe").ToList();
ctx.Update(new Person{ Id=list.First().Id, Age=31 });
```

- 日志格式示例：`SELECT ... | @p0=Doe`
- 注意：日志仅用于调试，请在委托内做脱敏或过滤

## AOT 与零反射

- 物化器与实体映射由 Source Generator 在编译期生成，不依赖运行时反射，AOT 友好
- SQL 生成与参数绑定依赖已生成的委托与映射元数据（`MappingRegistry` 与各 `EntityMap`）
- 日志记录通过 `Action<string>` 输出，不引入动态代理或反射

## 方言

- 适配器接口职责（编译器、类型映射、标识符与表名引用、参数占位符、多值 INSERT 支持）
- 支持 SqlServer、MySql、PostgreSQL、Sqlite；构造 `TinyOrmContext` 时传入适配器实例

## 迁移（从仓储到上下文）

- `ctx.Set<TEntity>().Insert(...)` → `ctx.Insert(entity)`
- `repo.BulkInsert(...)` → `ctx.BulkInsert(entities)`
- `repo.Update(...)` → `ctx.Update(entity)`
- `repo.Delete(...)` → `ctx.Delete(entity)`
- `repo.Query(table, materializer)` → `ctx.Query<TEntity>()`

## 构建与测试

```bash
dotnet build -warnaserror
dotnet test -v minimal
```