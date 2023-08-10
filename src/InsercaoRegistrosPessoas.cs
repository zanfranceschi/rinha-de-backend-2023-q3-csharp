using System.Collections.Concurrent;
using Npgsql;
using NpgsqlTypes;

namespace RinhaDeBackEnd;
public class InsercaoRegistrosPessoas
    : BackgroundService
{
    private readonly ILogger<InsercaoRegistrosPessoas> _logger;
    private readonly ConcurrentQueue<Pessoa> _queue;
    private readonly NpgsqlConnection _conn;

    public InsercaoRegistrosPessoas(
        ILogger<InsercaoRegistrosPessoas> logger,
        ConcurrentQueue<Pessoa> queue,
        NpgsqlConnection conn)
    {
        _logger = logger;
        _queue = queue;
        _conn = conn;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5_000); // I know, sorry :´)

        await _conn.OpenAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(5_000);

            var pessoas = new List<Pessoa>();

            Pessoa pessoa;
            while (_queue.TryDequeue(out pessoa))
                pessoas.Add(pessoa);

            if (pessoas.Count == 0)
                continue;

            try
            {
                var batch = _conn.CreateBatch();
                var batchCommands = new List<NpgsqlBatchCommand>();

                foreach (var p in pessoas)
                {
                    var batchCmd = new NpgsqlBatchCommand("""
                        insert into pessoas
                        (id, apelido, nome, nascimento, stack, busca)
                        values ($1, $2, $3, $4, $5, $6);
                    """);
                    batchCmd.Parameters.AddWithValue(p.Id);
                    batchCmd.Parameters.AddWithValue(p.Apelido);
                    batchCmd.Parameters.AddWithValue(p.Nome);
                    batchCmd.Parameters.AddWithValue(p.Nascimento.Value);

                    if (p.Stack is not null)
                        batchCmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb, p.Stack);
                    else
                        batchCmd.Parameters.AddWithValue(DBNull.Value);

                    var busca = $"{p.Apelido}{p.Nome}{string.Join("", p.Stack?.Select(i => i) ?? new List<string>())}";
                    batchCmd.Parameters.AddWithValue(busca);

                    batch.BatchCommands.Add(batchCmd);
                }

                await batch.ExecuteNonQueryAsync();
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