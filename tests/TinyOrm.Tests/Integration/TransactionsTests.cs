using Microsoft.Data.Sqlite;
using TinyOrm.Dialects.Adapters;
using TinyOrm.Runtime.Context;
using TinyOrm.Tests.Entities;
using Xunit;

namespace TinyOrm.Tests.Integration;

public class TransactionsTests
{
    [Fact]
    public void Sqlite_Transaction_Rollback_And_Commit_Works()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var ctx = new TinyOrmContext(conn, new SqliteDialectAdapter());
        ctx.ExecuteRaw("CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER);", System.Array.Empty<System.Collections.Generic.KeyValuePair<string,object?>>());

        using (var tx = ctx.BeginTransaction())
        {
            ctx.Insert(new Person{ FirstName="T1", LastName="X", Age=1 }, cmd => cmd.Transaction = tx);
            ctx.Insert(new Person{ FirstName="T2", LastName="X", Age=2 }, cmd => cmd.Transaction = tx);
            tx.Rollback();
        }
        var afterRollback = ctx.Query<Person>().Where(a => a.LastName == "X").Count();
        Assert.Equal(0, afterRollback);

        using (var tx2 = ctx.BeginTransaction())
        {
            ctx.Insert(new Person{ FirstName="T1", LastName="X", Age=1 }, cmd => cmd.Transaction = tx2);
            ctx.Insert(new Person{ FirstName="T2", LastName="X", Age=2 }, cmd => cmd.Transaction = tx2);
            tx2.Commit();
        }
        var afterCommit = ctx.Query<Person>().Where(a => a.LastName == "X").Count();
        Assert.Equal(2, afterCommit);
    }

    [Fact]
    public void Sqlite_NestedTransactions_Savepoint_Rollback_Works()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var ctx = new TinyOrmContext(conn, new SqliteDialectAdapter());
        ctx.ExecuteRaw("CREATE TABLE persons(id INTEGER PRIMARY KEY, first_name TEXT, last_name TEXT, age INTEGER);", System.Array.Empty<System.Collections.Generic.KeyValuePair<string,object?>>());

        using var tx = ctx.BeginTransaction();
        ctx.ExecuteRaw("SAVEPOINT sp1", System.Array.Empty<System.Collections.Generic.KeyValuePair<string,object?>>(), tx);
        ctx.Insert(new Person{ FirstName="A", LastName="Y", Age=10 }, cmd => cmd.Transaction = tx);
        ctx.ExecuteRaw("SAVEPOINT sp2", System.Array.Empty<System.Collections.Generic.KeyValuePair<string,object?>>(), tx);
        ctx.Insert(new Person{ FirstName="B", LastName="Y", Age=20 }, cmd => cmd.Transaction = tx);
        ctx.ExecuteRaw("ROLLBACK TO sp2", System.Array.Empty<System.Collections.Generic.KeyValuePair<string,object?>>(), tx);
        ctx.ExecuteRaw("RELEASE sp1", System.Array.Empty<System.Collections.Generic.KeyValuePair<string,object?>>(), tx);
        tx.Commit();

        var countY = ctx.Query<Person>().Where(a => a.LastName == "Y").Count();
        Assert.Equal(1, countY);
    }
}