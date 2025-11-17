using Microsoft.Data.Sqlite;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using Xunit;

namespace TinyOrm.Tests.Integration;

public class SqlLoggingTests
{
    [Fact]
    public void Sql_Logging_Can_Be_Enabled_And_Records_Statements()
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

        ctx.Insert(new Person{ FirstName = "John", LastName = "Doe", Age = 30 });
        ctx.BulkInsert(new[]{
            new Person{ FirstName = "Jane", LastName = "Doe", Age = 25 },
            new Person{ FirstName = "Jim", LastName = "Beam", Age = 20 }
        });

        var list = ctx.Query<Person>().Where(a => a.LastName == "Doe").OrderBy(a => a.Id).ToList();
        var e = list.First(); e.Age = 31; ctx.Update(e);
        ctx.Delete(e);

        var rows = ctx.QueryRaw<Person>("SELECT id,first_name,last_name,age FROM persons WHERE last_name=@last", new[]{ new System.Collections.Generic.KeyValuePair<string,object?>("last","Doe") }, new PersonMap.Materializer());

        Assert.Contains(logs, x => x.Contains("CREATE TABLE"));
        Assert.Contains(logs, x => x.Contains("INSERT"));
        Assert.Contains(logs, x => x.Contains("SELECT"));
        Assert.Contains(logs, x => x.Contains("UPDATE"));
        Assert.Contains(logs, x => x.Contains("DELETE"));
    }
}