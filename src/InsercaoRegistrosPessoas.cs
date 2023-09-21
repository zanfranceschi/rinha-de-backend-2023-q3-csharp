using System.Collections.Concurrent;
using Npgsql;
using StackExchange.Redis;
using System.Text.Json;

namespace RinhaDeBackEnd;
public class InsercaoRegistrosPessoas
    : BackgroundService
{
    private readonly ILogger<InsercaoRegistrosPessoas> _logger;
    private readonly ConcurrentQueue<Pessoa> _queue;
    private readonly ConcurrentDictionary<string, Pessoa> _pessasMap;
    private readonly IDatabase _cache;
    private readonly NpgsqlConnection _conn;

    public InsercaoRegistrosPessoas(
        ILogger<InsercaoRegistrosPessoas> logger,
        ConcurrentQueue<Pessoa> queue,
        ConcurrentDictionary<string, Pessoa> pessasMap,
        IDatabase cache,
        NpgsqlConnection conn)
    {
        _logger = logger;
        _queue = queue;
        _pessasMap = pessasMap;
        _cache = cache;
        _conn = conn;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool connected = false;

        while (!connected)
        {
            try
            {
                await _conn.OpenAsync();
                connected = true;
                _logger.LogInformation("connected to postgres!!! yey");
            }
            catch (NpgsqlException)
            {
                _logger.LogWarning("retrying connection to postgres");
                await Task.Delay(5_000);
            }
        }

     while (!stoppingToken.IsCancellationRequested)
     {
        await Task.Delay(2_000);
    
        var pessoas = new List<Pessoa>(_queue.Count);
    
        while (_queue.TryDequeue(out var pessoa))
        {
            pessoas.Add(pessoa);
        }
    
        if (pessoas.Count == 0)
        {
            continue;
        }
    
        try
        {
            using (var batch = _conn.CreateBatch())
            {
                var batchCommands = new List<NpgsqlBatchCommand>();
                var batchCmd = new NpgsqlBatchCommand(@"
                    insert into pessoas
                    (id, apelido, nome, nascimento, stack)
                    values ($1, $2, $3, $4, $5)
                    on conflict do nothing;
                ");
    
                batch.BatchCommands.Add(batchCmd);
    
                foreach (var p in pessoas)
                {
                    batchCmd.Parameters.AddWithValue(p.Id);
                    batchCmd.Parameters.AddWithValue(p.Apelido);
                    batchCmd.Parameters.AddWithValue(p.Nome);
                    batchCmd.Parameters.AddWithValue(p.Nascimento.Value);
                    batchCmd.Parameters.AddWithValue(p.Stack == null ? DBNull.Value : p.Stack.Select(s => s.ToString()).ToArray());
    
                    var buscaStackValue = p.Stack == null ? "" : string.Join("", p.Stack.Select(s => s.ToString()));
                    var buscaValue = $"{p.Apelido}{p.Nome}{buscaStackValue}" ?? "";
                    await _cache.PublishAsync("busca", JsonSerializer.Serialize<Pessoa>(p), CommandFlags.FireAndForget);
                }
    
                await batch.ExecuteNonQueryAsync();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "erro no worker :)");
        }
    }
    
    await _conn.CloseAsync();
    await _conn.DisposeAsync();
    }
}
