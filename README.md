# TinyOrm

一个轻量级、零反射、AOT 友好的 ORM。查询能力由 SqlKata 驱动，写操作由编译期 Source Generator 输出，统一通过 `TinyOrmContext` 使用。

**特性**
- 跨方言：SqlServer / MySql / PostgreSql / Sqlite
- 强类型查询：Where / OrderBy / GroupBy / Having / 聚合 / DTO 投影
- 写操作：Insert / BulkInsert / Update / Delete（零反射参数绑定）
- 原生 SQL：ExecuteRaw / QueryRaw
- 日志：可选 SQL 文本与参数日志输出

**快速开始**
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
{ cmd.CommandText = "CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER);"; cmd.ExecuteNonQuery(); }

var ctx = new TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter());
ctx.Insert(new Person { FirstName = "John", LastName = "Doe", Age = 30 });
ctx.BulkInsert(new[]{ new Person { FirstName = "Jane", LastName = "Doe", Age = 25 }, new Person { FirstName = "Jim",  LastName = "Beam", Age = 20 } });

var list = ctx.Query<Person>().Where(a => a.LastName == "Doe").OrderBy(a => a.Age).ThenByDesc(a => a.Id).Take(10).ToList();
var toUpdate = list.First(); toUpdate.Age = 31; ctx.Update(toUpdate);
ctx.Delete(toUpdate);
```

**DTO 与聚合**
```csharp
using System.ComponentModel.DataAnnotations.Schema;

[ComplexType]
public sealed class PersonView { public int Id { get; set; } public string? Name { get; set; } }

var dto = ctx.Query<Person>().OrderBy(a => a.Id).SelectDto<PersonView>(a => new PersonView { Id = a.Id, Name = a.FirstName }).ToList();
var countDoe = ctx.Query<Person>().Where(a => a.LastName == "Doe").Count();
var sumDoe = ctx.Query<Person>().Where(a => a.LastName == "Doe").Sum(a => a.Age);
var avgDoe = ctx.Query<Person>().Where(a => a.LastName == "Doe").Avg(a => a.Age);
var maxAge = ctx.Query<Person>().Max<int>(a => a.Age);
```

**原生 SQL**
```csharp
ctx.ExecuteRaw("INSERT INTO persons(first_name,last_name,age) VALUES(@first,@last,@age)", new[]{ new KeyValuePair<string,object?>("first","John"), new KeyValuePair<string,object?>("last","Doe"), new KeyValuePair<string,object?>("age",30) });
var rows = ctx.QueryRaw<Person>("SELECT id,first_name,last_name,age FROM persons WHERE last_name=@last", new[]{ new KeyValuePair<string,object?>("last","Doe") }, new PersonMap.Materializer());
```

**日志**
```csharp
var ctx = new TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter())
{
    EnableSqlLogging = true,
    SqlLogger = s => Console.WriteLine(s)
};
```
