using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TinyOrm.Tests.Infrastructure;

public sealed class SqlServerFixture : IAsyncLifetime
{
    public SqlConnection? Connection { get; private set; }
    public bool IsAvailable { get; private set; }
    private MsSqlContainer? _container;

    public async Task InitializeAsync()
    {
        var cs = Environment.GetEnvironmentVariable("TINYORM_SQLSERVER");
        if (!string.IsNullOrEmpty(cs))
        {
            try
            {
                Connection = new SqlConnection(cs);
                await Connection.OpenAsync();
                IsAvailable = true;
                return;
            }
            catch { }
        }

        try
        {
            _container = new MsSqlBuilder().Build();
            await _container.StartAsync();
            Connection = new SqlConnection(_container.GetConnectionString());
            await Connection.OpenAsync();
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (Connection is not null)
        {
            await Connection.DisposeAsync();
        }
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

public sealed class MySqlFixture : IAsyncLifetime
{
    public MySqlConnection? Connection { get; private set; }
    public bool IsAvailable { get; private set; }
    private MySqlContainer? _container;

    public async Task InitializeAsync()
    {
        var cs = Environment.GetEnvironmentVariable("TINYORM_MYSQL");
        if (!string.IsNullOrEmpty(cs))
        {
            try
            {
                Connection = new MySqlConnection(cs);
                await Connection.OpenAsync();
                IsAvailable = true;
                return;
            }
            catch { }
        }

        try
        {
            _container = new MySqlBuilder().Build();
            await _container.StartAsync();
            Connection = new MySqlConnection(_container.GetConnectionString());
            await Connection.OpenAsync();
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (Connection is not null)
        {
            await Connection.DisposeAsync();
        }
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

public sealed class PostgresFixture : IAsyncLifetime
{
    public NpgsqlConnection? Connection { get; private set; }
    public bool IsAvailable { get; private set; }
    private PostgreSqlContainer? _container;

    public async Task InitializeAsync()
    {
        var cs = Environment.GetEnvironmentVariable("TINYORM_POSTGRES");
        if (!string.IsNullOrEmpty(cs))
        {
            try
            {
                Connection = new NpgsqlConnection(cs);
                await Connection.OpenAsync();
                IsAvailable = true;
                return;
            }
            catch { }
        }

        try
        {
            _container = new PostgreSqlBuilder().Build();
            await _container.StartAsync();
            Connection = new NpgsqlConnection(_container.GetConnectionString());
            await Connection.OpenAsync();
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (Connection is not null)
        {
            await Connection.DisposeAsync();
        }
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
