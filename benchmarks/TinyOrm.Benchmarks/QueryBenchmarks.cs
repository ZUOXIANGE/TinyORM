using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Data.Sqlite;
using SqlKata;

using TinyOrm.Runtime.Context;

namespace TinyOrm.Benchmarks;

public class QueryBenchmarks
{
    private TinyOrmContext _ctx = null!;
    private SqliteConnection _conn = null!;

    [GlobalSetup]
    public void Setup()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER);";
        cmd.ExecuteNonQuery();
        _ctx = new TinyOrmContext(_conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter());
    }

    [Benchmark]
    public string Compile_Select_By_FirstName()
    {
        var q = new Query(PersonMap.Table).Where(PersonMap.FirstName.ColumnName, "John");
        return _ctx.Dialect.Compiler.Compile(q).Sql;
    }

    [Benchmark]
    public void AdoNet_Insert_Raw()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO persons(first_name,last_name,age) VALUES(@p1,@p2,@p3)";
        var p1 = cmd.CreateParameter(); p1.ParameterName = "@p1"; p1.Value = "John"; cmd.Parameters.Add(p1);
        var p2 = cmd.CreateParameter(); p2.ParameterName = "@p2"; p2.Value = "Doe"; cmd.Parameters.Add(p2);
        var p3 = cmd.CreateParameter(); p3.ParameterName = "@p3"; p3.Value = 30; cmd.Parameters.Add(p3);
        cmd.ExecuteNonQuery();
    }

    public static void Main(string[] args) => BenchmarkRunner.Run<QueryBenchmarks>();
}

[System.ComponentModel.DataAnnotations.Schema.Table("persons")]
public sealed class Person
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity)]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")] public int Id { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.Column("first_name")] public string? FirstName { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.Column("last_name")] public string? LastName { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.Column("age")] public int? Age { get; set; }
}
