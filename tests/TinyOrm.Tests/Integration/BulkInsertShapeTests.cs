using Microsoft.Data.Sqlite;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using Xunit;

namespace TinyOrm.Tests.Integration;

public class BulkInsertShapeTests
{
    [Fact]
    public void BulkInsert_Generates_MultiValues_SQL_When_Supported()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var ctx = new TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter())
        {
            EnableSqlLogging = true
        };
        var logs = new System.Collections.Generic.List<string>();
        ctx.SqlLogger = s => logs.Add(s);

        ctx.ExecuteRaw("CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER);", System.Array.Empty<System.Collections.Generic.KeyValuePair<string,object?>>());

        var affected = ctx.BulkInsert(new[]{
            new Person{ FirstName="John", LastName="Doe", Age=30 },
            new Person{ FirstName="Jane", LastName="Doe", Age=25 },
            new Person{ FirstName="Jim",  LastName="Beam", Age=20 }
        });

        Assert.Equal(3, affected);
        Assert.Contains(logs, x => x.Contains("INSERT INTO") && x.Contains("VALUES") && x.Contains("),"));
    }
}