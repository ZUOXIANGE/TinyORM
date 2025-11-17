using TinyOrm.Dialects.Adapters;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using TinyOrm.Tests.Infrastructure;
using Xunit;

namespace TinyOrm.Tests.Integration;

public class MySqlCrudTests : IClassFixture<MySqlFixture>
{
    private readonly MySqlFixture _fx;

    public MySqlCrudTests(MySqlFixture fx) { _fx = fx; }

    [Fact]
    public void MySql_CRUD()
    {
        if (!_fx.IsAvailable) return;
        using (var cmd = _fx.Connection!.CreateCommand())
        {
            cmd.CommandText = "DROP TABLE IF EXISTS persons; CREATE TABLE persons(id INT AUTO_INCREMENT PRIMARY KEY, first_name VARCHAR(100), last_name VARCHAR(100), age INT);";
            cmd.ExecuteNonQuery();
        }
        var ctx = new TinyOrmContext(_fx.Connection!, new MySqlDialectAdapter());
        var p = new Person { FirstName = "John", LastName = "Doe", Age = 30 };
        Assert.Equal(1, ctx.Insert(p));
        var list = ctx.Query<Person>().Where(a => a.LastName == "Doe").ToList();
        var e = list.First(); e.Age = 31; Assert.Equal(1, ctx.Update(e));
        Assert.Equal(1, ctx.Delete(e));
    }
}
