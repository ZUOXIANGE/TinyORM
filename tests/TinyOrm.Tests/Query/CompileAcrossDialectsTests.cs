using TinyOrm.Dialects.Adapters;
using TinyOrm.Tests.Entities;

using Xunit;

namespace TinyOrm.Tests.Query;

public class CompileAcrossDialectsTests
{
    [Fact]
    public void SqlServer_Compile_Select_With_Where()
    {
        var adapter = new SqlServerDialectAdapter();
        var q = new SqlKata.Query(PersonMap.Table).Where(PersonMap.FirstName.ColumnName, "John");
        var sql = adapter.Compiler.Compile(q).Sql;
        Assert.Contains("FROM [persons]", sql);
    }

    [Fact]
    public void MySql_Compile_Select_With_Where()
    {
        var adapter = new MySqlDialectAdapter();
        var q = new SqlKata.Query(PersonMap.Table).Where(PersonMap.LastName.ColumnName, "Doe");
        var sql = adapter.Compiler.Compile(q).Sql;
        Assert.Contains("FROM `persons`", sql);
    }

    [Fact]
    public void Postgres_Compile_Select_With_Where()
    {
        var adapter = new PostgresDialectAdapter();
        var q = new SqlKata.Query(PersonMap.Table).Where(PersonMap.Age.ColumnName, 42);
        var sql = adapter.Compiler.Compile(q).Sql;
        Assert.Contains("FROM \"persons\"", sql);
    }

    [Fact]
    public void Sqlite_Compile_Select_With_Where()
    {
        var adapter = new SqliteDialectAdapter();
        var q = new SqlKata.Query(PersonMap.Table).Where(PersonMap.Id.ColumnName, 1);
        var sql = adapter.Compiler.Compile(q).Sql;
        Assert.Contains("FROM \"persons\"", sql);
    }
}
