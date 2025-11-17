using Microsoft.Data.Sqlite;
using TinyOrm.Dialects.Adapters;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using Xunit;

namespace TinyOrm.Tests.Query;

public class SelectColumnsTests
{
    [Fact]
    public void Select_With_Alias_ToRows_Returns_Alias_Column()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER);";
            cmd.ExecuteNonQuery();
        }
        var ctx = new TinyOrmContext(conn, new SqliteDialectAdapter());
        ctx.Insert(new Person{ FirstName="John", LastName="Doe", Age=30 });

        var rows = ctx.Query<Person>()
            .Select("first_name as fn")
            .ToRows()
            .ToArray();

        Assert.True(rows.Length >= 1);
        Assert.Equal("John", rows[0]["fn"]);
    }
}