using Microsoft.Data.Sqlite;
using TinyOrm.Dialects.Adapters;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using Xunit;

#pragma warning disable CS8602

namespace TinyOrm.Tests.Query;

public class PredicateTests
{
    [Fact]
    public void Where_String_Contains_StartsWith_EndsWith_And_ListContains()
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

        var containsDo = ctx.Query<Person>().Where(a => a.LastName.Contains("Do")).Count();
        Assert.Equal(2, containsDo);

        var startsWithJ = ctx.Query<Person>().Where(a => a.FirstName.StartsWith("J")).Count();
        Assert.Equal(3, startsWithJ);

        var endsWithOe = ctx.Query<Person>().Where(a => a.LastName.EndsWith("oe")).Count();
        Assert.Equal(2, endsWithOe);

        var inList = ctx.Query<Person>().Where(a => new[]{"Doe","Beam"}.Contains(a.LastName)).Count();
        Assert.Equal(3, inList);

        var andAlso = ctx.Query<Person>().Where(a => a.LastName == "Doe" && a.Age >= 25).Count();
        Assert.Equal(2, andAlso);

        var orElse = ctx.Query<Person>().Where(a => a.LastName == "Doe" || a.Age < 25).Count();
        Assert.Equal(3, orElse);
    }
}
