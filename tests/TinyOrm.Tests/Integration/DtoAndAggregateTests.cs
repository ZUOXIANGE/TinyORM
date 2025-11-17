using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
using TinyOrm.Abstractions.Attributes;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using Xunit;
#pragma warning disable CS8603, CS8602

namespace TinyOrm.Tests.Integration;

[ComplexType]
public sealed class PersonView
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public class DtoAndAggregateTests
{
    [Fact]
    public void SelectDto_And_Aggregates_Work()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var ctx = new TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter());
        ctx.ExecuteRaw("CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER);", Array.Empty<KeyValuePair<string,object?>>());
        var persons = new[]{
            new Person{ FirstName="John", LastName="Doe", Age=30 },
            new Person{ FirstName="Jane", LastName="Doe", Age=25 },
            new Person{ FirstName="Jim", LastName="Beam", Age=20 },
        };
        var bulk = ctx.BulkInsert(persons);
        Assert.Equal(3, bulk);

        var dto = ctx.Query<Person>()
            .OrderBy(a => a.Id)
            .SelectDto<PersonView>(a => new PersonView{ Id = a.Id, Name = a.FirstName })
            .ToList();
        Assert.True(dto.Count() >= 3);
        Assert.Contains(dto, x => x.Name == "John");

        var countDoe = ctx.Query<Person>().Where(a => a.LastName == "Doe").Count();
        Assert.Equal(2, countDoe);

        var sumAgesDoe = ctx.Query<Person>().Where(a => a.LastName == "Doe").Sum(a => a.Age);
        Assert.Equal(55m, sumAgesDoe);

        var avgAgesDoe = ctx.Query<Person>().Where(a => a.LastName == "Doe").Avg(a => a.Age);
        Assert.True(avgAgesDoe > 0);

        var maxAge = ctx.Query<Person>().Max<int>(a => a.Age);
        Assert.Equal(30, maxAge);

        var groups = ctx.Query<Person>().GroupBy(a => a.LastName).Having(a => a.Age >= 20).ToRows();
        Assert.True(groups.Any());
    }
}

public enum OrderStatus
{
    Active = 1,
    Inactive = 2
}

[Table("orders_i")]
public sealed class OrderInt
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")] public int Id { get; set; }
    [Column("status")] public OrderStatus Status { get; set; }
    [Column("name")] public string? Name { get; set; }
}

[Table("orders_s")]
public sealed class OrderString
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")] public int Id { get; set; }
    [Column("status")]
    [EnumStorage(EnumStorageKind.String)]
    public OrderStatus Status { get; set; }
    [Column("name")] public string? Name { get; set; }
}

[Table("orders_n")]
public sealed class OrderNullable
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")] public int Id { get; set; }
    [Column("status")] public OrderStatus? Status { get; set; }
    [Column("name")] public string? Name { get; set; }
}

public class EnumMaterializerTests
{
    [Fact]
    public void Enum_Reads_From_Integer_Column()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var ctx = new TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter());
        ctx.ExecuteRaw("CREATE TABLE orders_i(id INTEGER PRIMARY KEY, status INTEGER, name TEXT);", Array.Empty<KeyValuePair<string,object?>>());
        ctx.ExecuteRaw("INSERT INTO orders_i(status,name) VALUES(@s,@n)", new[]{ new KeyValuePair<string,object?>("s",1), new KeyValuePair<string,object?>("n","A") });
        ctx.ExecuteRaw("INSERT INTO orders_i(status,name) VALUES(@s,@n)", new[]{ new KeyValuePair<string,object?>("s",2), new KeyValuePair<string,object?>("n","B") });

        var list = ctx.Query<OrderInt>().OrderBy(a => a.Id).ToList().ToArray();
        Assert.True(list.Length >= 2);
        Assert.Equal(OrderStatus.Active, list[0].Status);
        Assert.Equal(OrderStatus.Inactive, list[1].Status);
    }

    [Fact]
    public void Enum_Reads_From_String_Column()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var ctx = new TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter());
        ctx.ExecuteRaw("CREATE TABLE orders_s(id INTEGER PRIMARY KEY, status TEXT, name TEXT);", Array.Empty<KeyValuePair<string,object?>>());
        ctx.ExecuteRaw("INSERT INTO orders_s(status,name) VALUES(@s,@n)", new[]{ new KeyValuePair<string,object?>("s","Active"), new KeyValuePair<string,object?>("n","A") });
        ctx.ExecuteRaw("INSERT INTO orders_s(status,name) VALUES(@s,@n)", new[]{ new KeyValuePair<string,object?>("s","Inactive"), new KeyValuePair<string,object?>("n","B") });

        var list = ctx.Query<OrderString>().OrderBy(a => a.Id).ToList().ToArray();
        Assert.True(list.Length >= 2);
        Assert.Equal(OrderStatus.Active, list[0].Status);
        Assert.Equal(OrderStatus.Inactive, list[1].Status);
    }

    [Fact]
    public void Nullable_Enum_Reads_Null_And_Value()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var ctx = new TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter());
        ctx.ExecuteRaw("CREATE TABLE orders_n(id INTEGER PRIMARY KEY, status INTEGER, name TEXT);", Array.Empty<KeyValuePair<string,object?>>());
        ctx.ExecuteRaw("INSERT INTO orders_n(status,name) VALUES(@s,@n)", new[]{ new KeyValuePair<string,object?>("s",null), new KeyValuePair<string,object?>("n","A") });
        ctx.ExecuteRaw("INSERT INTO orders_n(status,name) VALUES(@s,@n)", new[]{ new KeyValuePair<string,object?>("s",2), new KeyValuePair<string,object?>("n","B") });

        var list = ctx.Query<OrderNullable>().OrderBy(a => a.Id).ToList().ToArray();
        Assert.True(list.Length >= 2);
        Assert.Null(list[0].Status);
        Assert.Equal(OrderStatus.Inactive, list[1].Status);
    }

    [Fact]
    public void Enum_Insert_String_Column_Stores_Name()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var ctx = new TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter());
        ctx.ExecuteRaw("CREATE TABLE orders_s(id INTEGER PRIMARY KEY, status TEXT, name TEXT);", Array.Empty<KeyValuePair<string,object?>>());
        var o = new OrderString{ Status = OrderStatus.Inactive, Name = "Z" };
        Assert.Equal(1, ctx.Insert(o));
        var raw = ctx.Query<OrderString>().Select("status").ToRows().ToArray();
        Assert.Single(raw);
        Assert.Equal("Inactive", raw[0]["status"]);
    }

}