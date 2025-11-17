using Microsoft.Data.Sqlite;
using TinyOrm.Dialects.Adapters;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using Xunit;

namespace TinyOrm.Tests.Query;

public class JoinTests
{
    [Fact]
    public void InnerJoin_Expression_And_Typed_Join_Work()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER);
CREATE TABLE cities(id INTEGER PRIMARY KEY, name TEXT, person_id INTEGER);";
            cmd.ExecuteNonQuery();
        }
        var ctx = new TinyOrmContext(conn, new SqliteDialectAdapter());
        var p = new Person{ FirstName="John", LastName="Doe", Age=30 };
        ctx.Insert(p);
        var c = new City{ Name="NY", PersonId=1 };
        ctx.Insert(c);

        var rowsExpr = ctx.Query<Person>()
            .InnerJoin<City>((pp, cc) => pp.Id == cc.PersonId)
            .Select("persons.id as pid", "cities.name as cname")
            .ToRows()
            .ToArray();
        Assert.True(rowsExpr.Length >= 1);
        Assert.Equal("NY", rowsExpr[0]["cname"]);

        var rowsTyped = ctx.Query<Person>()
            .Join<City, int>(PersonMap.Id, CityMap.PersonId)
            .Select("persons.id as pid", "cities.name as cname")
            .ToRows()
            .ToArray();
        Assert.True(rowsTyped.Length >= 1);
        Assert.Equal("NY", rowsTyped[0]["cname"]);
    }
}