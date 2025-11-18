using Microsoft.Data.Sqlite;
using TinyOrm.Runtime.Context;

#pragma warning disable CS8603, CS8602

namespace TinyOrm.Examples;

public static class Program
{
    public static void Main(string[] args)
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        CreateTables(conn);

        var ctx = new TinyOrmContext(conn, new TinyOrm.Dialects.Adapters.SqliteDialectAdapter());
        var (northId, southId) = SeedProvinces(ctx);
        var (john, firstId) = SeedPersons(ctx, northId, southId);
        UpdateFirstPersonAndPrintRows(ctx, firstId);
        JoinDemo(ctx, john, northId, southId);
        DtoDemo(ctx);
        AggregateAndGroupDemo(ctx);
        TransactionRollbackDemo(ctx, northId);
        TransactionCommitDemo(ctx, john);
        var johnReloaded = ctx.Query<Person>().Where(a => a.Id == john.Id).ToList().First();
        System.Console.WriteLine($"john age={johnReloaded.Age}");
        var deleted = ctx.Delete(new Person{ Id = firstId });
    }

    private static void CreateTables(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER, province_id INTEGER);";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE TABLE provinces(id INTEGER PRIMARY KEY, name TEXT);";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE TABLE cities(id INTEGER PRIMARY KEY, name TEXT, person_id INTEGER, province_id INTEGER);";
        cmd.ExecuteNonQuery();
    }

    private static (int northId, int southId) SeedProvinces(TinyOrmContext ctx)
    {
        ctx.BulkInsert(new[]{ new Province{ Name = "North" }, new Province{ Name = "South" } });
        var provs = ctx.Query<Province>().OrderBy(a => a.Id).ToList();
        var northId = provs.First(a => a.Name == "North").Id;
        var southId = provs.First(a => a.Name == "South").Id;
        return (northId, southId);
    }

    private static (Person john, int firstId) SeedPersons(TinyOrmContext ctx, int northId, int southId)
    {
        var inserted = ctx.Insert(new Person{ FirstName="John", LastName="Doe", Age=30, ProvinceId = northId });
        ctx.BulkInsert(new[]{
            new Person{ FirstName="Jane", LastName="Doe", Age=25, ProvinceId = southId },
            new Person{ FirstName="Jim", LastName="Beam", Age=20, ProvinceId = northId }
        });
        var q = ctx.Query<Person>()
            .Where(a => a.FirstName == "John")
            .OrderBy(a => a.LastName)
            .ThenByDesc(a => a.Id)
            .Take(10);
        var list = q.ToList();
        foreach (var p in list) System.Console.WriteLine($"{p.Id}:{p.FirstName} {p.LastName} {p.Age}");
        var john = ctx.Query<Person>().Where(a => a.FirstName == "John").ToList().First();
        return (john, list.First().Id);
    }

    private static void UpdateFirstPersonAndPrintRows(TinyOrmContext ctx, int firstId)
    {
        var first = ctx.Query<Person>().Where(a => a.Id == firstId).ToList().First();
        var updated = ctx.UpdateFields(new Person{ Id=firstId, Age=31 }, a => a.Age);
        var rows = ctx.Query<Person>().Select("id","first_name","age").ToRows();
        foreach (var r in rows) System.Console.WriteLine($"row id={r["id"]} age={r["age"]}");
    }

    private static void JoinDemo(TinyOrmContext ctx, Person john, int northId, int southId)
    {
        ctx.BulkInsert(new[]{
            new City{ Name = "Springfield", PersonId = john.Id, ProvinceId = northId },
            new City{ Name = "Shelbyville", PersonId = john.Id, ProvinceId = southId }
        });
        var joined = ctx.Query<Person>()
            .InnerJoin<City>((p, c) => p.Id == c.PersonId)
            .LeftJoin<Province>((p, pr) => p.ProvinceId == pr.Id)
            .Select("persons.id AS PersonId", "persons.first_name AS FirstName", "cities.name AS City", "provinces.name AS Province")
            .OrderBy(a => a.Id)
            .ToRows()
            .ToArray();
        foreach (var r in joined) System.Console.WriteLine($"join id={r["PersonId"]}, {r["FirstName"]}, city={r["City"]}, prov={r["Province"]}");
    }

    private static void DtoDemo(TinyOrmContext ctx)
    {
        var dto = ctx.Query<Person>().OrderBy(a => a.Id).SelectDto<PersonView>(a => new PersonView{ Id = a.Id, Name = a.FirstName }).ToList();
        foreach (var v in dto) System.Console.WriteLine($"view {v.Id}:{v.Name}");
    }

    private static void AggregateAndGroupDemo(TinyOrmContext ctx)
    {
        var countDoe = ctx.Query<Person>().Where(a => a.LastName == "Doe").Count();
        var sumDoe = ctx.Query<Person>().Where(a => a.LastName == "Doe").Sum(a => a.Age);
        var avgDoe = ctx.Query<Person>().Where(a => a.LastName == "Doe").Avg(a => a.Age);
        var maxAge = ctx.Query<Person>().Max<int>(a => a.Age);
        System.Console.WriteLine($"stats count={countDoe} sum={sumDoe} avg={avgDoe} max={maxAge}");
        var groups = ctx.Query<Person>().GroupBy(a => a.LastName).Having(a => a.Age >= 20).ToRows();
        System.Console.WriteLine($"groups {groups.Count()}");
    }

    private static void TransactionRollbackDemo(TinyOrmContext ctx, int northId)
    {
        using (var tx = ctx.BeginTransaction())
        {
            ctx.Insert(new Person{ FirstName = "Tx", LastName = "User", Age = 99, ProvinceId = northId }, cmd => cmd.Transaction = tx);
            tx.Rollback();
        }
        var afterRollback = ctx.Query<Person>().Where(a => a.FirstName == "Tx").Count();
        System.Console.WriteLine($"after rollback count={afterRollback}");
    }

    private static void TransactionCommitDemo(TinyOrmContext ctx, Person john)
    {
        using (var tx2 = ctx.BeginTransaction())
        {
            john.Age = 32;
            ctx.Update(john, cmd => cmd.Transaction = tx2);
            tx2.Commit();
        }
    }
}
