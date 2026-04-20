using Dapper;
using Npgsql;

namespace paa_tm.Helpers;

public class SqlDbHelper
{
    private readonly string _connStr;

    public SqlDbHelper(IConfiguration config)
    {
        _connStr = config.GetConnectionString("WebApiDatabase")
            ?? throw new InvalidOperationException("Connection string tidak ditemukan di appsettings.json");
    }

    private NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connStr);

    public async Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync(sql, param);
    }

    public async Task<dynamic?> QueryFirstOrDefaultAsync(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(sql, param);
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(sql, param);
    }

    public async Task<T> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<T>(sql, param);
    }

    public async Task<IEnumerable<dynamic>> QueryDynamicAsync(string sql, DynamicParameters param)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync(sql, param);
    }

    public async Task<int> ExecuteScalarDynamicAsync(string sql, DynamicParameters param)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(sql, param);
    }

    public async Task ExecuteTransactionAsync(Func<NpgsqlConnection, NpgsqlTransaction, Task> actions)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            await actions(conn, tx);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}