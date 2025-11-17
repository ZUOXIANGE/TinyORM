using Microsoft.Data.Sqlite;
using TinyOrm.Dialects.Adapters;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using Xunit;

namespace TinyOrm.Tests.Integration;

public class SqliteCrudTests
{
    [Fact]
    public void Sqlite_CRUD()
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
        var inserted = ctx.Insert(p);
        Assert.Equal(1, inserted);

        var list = ctx.Query<Person>().Where(a => a.FirstName == "John").ToList();
        Assert.True(list.Count() >= 1);

        var toUpdate = list.First();
        toUpdate.Age = 31;
        var updated = ctx.Update(toUpdate);
        Assert.Equal(1, updated);

        var deleted = ctx.Delete(toUpdate);
        Assert.Equal(1, deleted);
    }
}
