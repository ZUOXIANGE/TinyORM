using Microsoft.Data.Sqlite;
using TinyOrm.Dialects.Adapters;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using Xunit;

namespace TinyOrm.Tests.Integration;

public class UpdateFieldsTests
{
    [Fact]
    public void Sqlite_UpdateFields_Only_Target_Columns()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER);";
            cmd.ExecuteNonQuery();
        }
        var ctx = new TinyOrmContext(conn, new SqliteDialectAdapter());
        var p = new Person { FirstName = "John", LastName = "Doe", Age = 30 };
        Assert.Equal(1, ctx.Insert(p));
        var first = ctx.Query<Person>().Where(a => a.FirstName == "John").ToList().First();
        var affected = ctx.UpdateFields(new Person { Id = first.Id, Age = 31 }, e => e.Age);
        Assert.Equal(1, affected);
        var after = ctx.Query<Person>().Where(a => a.Id == first.Id).ToList().First();
        Assert.Equal("John", after.FirstName);
        Assert.Equal("Doe", after.LastName);
        Assert.Equal(31, after.Age);
    }

    [Fact]
    public void Sqlite_UpdateSet_By_Where()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER);";
            cmd.ExecuteNonQuery();
        }
        var ctx = new TinyOrmContext(conn, new SqliteDialectAdapter());
        ctx.BulkInsert(new[]{
            new Person{ FirstName="John", LastName="Doe", Age=30 },
            new Person{ FirstName="Jane", LastName="Doe", Age=25 },
            new Person{ FirstName="Jim",  LastName="Beam", Age=20 }
        });
        var changed = ctx.UpdateSet(new Person { Age = 40 }, e => e.LastName == "Doe", e => e.Age);
        Assert.True(changed >= 2);
        var list = ctx.Query<Person>().OrderBy(a => a.Id).ToList().ToArray();
        Assert.Equal(40, list[0].Age);
        Assert.Equal(40, list[1].Age);
        Assert.Equal(20, list[2].Age);
    }
}