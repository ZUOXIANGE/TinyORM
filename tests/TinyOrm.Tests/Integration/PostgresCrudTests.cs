using TinyOrm.Dialects.Adapters;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using TinyOrm.Tests.Infrastructure;
using Xunit;

namespace TinyOrm.Tests.Integration;

public class PostgresCrudTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;

    public PostgresCrudTests(PostgresFixture fx) { _fx = fx; }

    [Fact]
    public void Postgres_CRUD()
    {
        if (!_fx.IsAvailable) return;
        using (var cmd = _fx.Connection!.CreateCommand())
        {
            cmd.CommandText = "DROP TABLE IF EXISTS persons; CREATE TABLE persons(id SERIAL PRIMARY KEY, first_name TEXT, last_name TEXT, age INT);";
            cmd.ExecuteNonQuery();
        }
        var ctx = new TinyOrmContext(_fx.Connection!, new PostgresDialectAdapter());
        var p = new Person { FirstName = "John", LastName = "Doe", Age = 30 };
        Assert.Equal(1, ctx.Insert(p));
        var list = ctx.Query<Person>().Where(a => a.LastName == "Doe").ToList();
        var e = list.First(); e.Age = 31; Assert.Equal(1, ctx.Update(e));
        Assert.Equal(1, ctx.Delete(e));
    }
}
