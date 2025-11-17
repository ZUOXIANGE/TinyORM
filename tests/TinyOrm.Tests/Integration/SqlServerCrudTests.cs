using TinyOrm.Dialects.Adapters;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using TinyOrm.Tests.Infrastructure;
using Xunit;

namespace TinyOrm.Tests.Integration;

public class SqlServerCrudTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fx;

    public SqlServerCrudTests(SqlServerFixture fx) { _fx = fx; }

    [Fact]
    public void SqlServer_CRUD()
    {
        if (!_fx.IsAvailable) return;
        using (var cmd = _fx.Connection!.CreateCommand())
        {
            cmd.CommandText = "IF OBJECT_ID('dbo.persons','U') IS NOT NULL DROP TABLE dbo.persons; CREATE TABLE dbo.persons(id INT IDENTITY(1,1) PRIMARY KEY, first_name NVARCHAR(100), last_name NVARCHAR(100), age INT);";
            cmd.ExecuteNonQuery();
        }
        var ctx = new TinyOrmContext(_fx.Connection!, new SqlServerDialectAdapter());
        var p = new Person { FirstName = "John", LastName = "Doe", Age = 30 };
        Assert.Equal(1, ctx.Insert(p));
        var list = ctx.Query<Person>().Where(a => a.LastName == "Doe").ToList();
        var e = list.First(); e.Age = 31; Assert.Equal(1, ctx.Update(e));
        Assert.Equal(1, ctx.Delete(e));
    }
}
