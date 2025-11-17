using Microsoft.Data.Sqlite;
using TinyOrm.Dialects.Adapters;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using Xunit;

#pragma warning disable CS8603, CS8602

namespace TinyOrm.Tests.Query;

public class HavingTests
{
    [Fact]
    public void GroupBy_Multi_And_Having_Like_Work()
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

        var rows = ctx.Query<Person>()
            .GroupBy(a => a.LastName)
            .GroupBy(a => a.FirstName)
            .Having(a => a.LastName.Contains("Do"))
            .ToRows()
            .ToArray();

        Assert.True(rows.Length >= 1);
    }
}
