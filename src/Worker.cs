using Npgsql;

namespace RinhaDeBackEnd;

public class Worker
    : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly NpgsqlConnection _conn;

    public Worker(ILogger<Worker> logger, NpgsqlConnection conn)
    {
        _logger = logger;
        _conn = conn;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // let's do nothing for now
        return;
        await _conn.OpenAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, stoppingToken);
                
                await using var refreshMaterializedViewCmd = _conn.CreateCommand();
                refreshMaterializedViewCmd.CommandText = "refresh materialized view concurrently public.pessoas_busca";
                await refreshMaterializedViewCmd.ExecuteNonQueryAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Erro no worker");
            }
        }

        await _conn.CloseAsync();
        await _conn.DisposeAsync();
    }
}