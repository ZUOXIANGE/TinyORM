using Microsoft.Data.Sqlite;
using TinyOrm.Dialects.Adapters;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using Xunit;

namespace TinyOrm.Tests.Query;

public class PagingTests
{
    [Fact]
    public void Order_Skip_Take_Works()
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
            new Person{ FirstName="A", LastName="L", Age=10 },
            new Person{ FirstName="B", LastName="K", Age=20 },
            new Person{ FirstName="C", LastName="J", Age=30 },
            new Person{ FirstName="D", LastName="I", Age=40 },
            new Person{ FirstName="E", LastName="H", Age=50 }
        });

        var page = ctx.Query<Person>()
            .OrderBy(a => a.Age)
            .ThenByDesc(a => a.Id)
            .Skip(1)
            .Take(2)
            .ToList()
            .ToArray();

        Assert.Equal(2, page.Count());
        Assert.True(page[0].Age >= 20);
        Assert.True(page[1].Age >= page[0].Age);
    }
}
