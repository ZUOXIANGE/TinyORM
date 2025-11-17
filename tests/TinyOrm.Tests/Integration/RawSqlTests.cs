using Microsoft.Data.Sqlite;

using TinyOrm.Tests.Entities;
using Xunit;

namespace TinyOrm.Tests.Integration;

public class RawSqlTests
{
    [Fact]
    public void ExecuteRaw_And_QueryRaw_Works()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var ctx = new TinyOrm.Runtime.Context.TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter());
        ctx.ExecuteRaw("CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER);", Array.Empty<KeyValuePair<string,object?>>());
        var affected = ctx.ExecuteRaw("INSERT INTO persons(first_name,last_name,age) VALUES(@first,@last,@age)", new[]{ new KeyValuePair<string,object?>("first","John"), new KeyValuePair<string,object?>("last","Doe"), new KeyValuePair<string,object?>("age",30) });
        Assert.Equal(1, affected);
        var rows = ctx.QueryRaw<Person>("SELECT id,first_name,last_name,age FROM persons WHERE last_name=@last", new[]{ new KeyValuePair<string,object?>("last","Doe") }, new PersonMap.Materializer());
        Assert.True(rows.Any());
    }
}
