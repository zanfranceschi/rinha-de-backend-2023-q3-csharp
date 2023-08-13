using System.Collections.Concurrent;
using StackExchange.Redis;
using System.Text.Json;

namespace RinhaDeBackEnd;
public class BuscaPessoas
    : BackgroundService
{
    private readonly ConcurrentDictionary<string, Pessoa> _pessasMap;
    private readonly IConnectionMultiplexer _multiplexer;

    public BuscaPessoas(
        ConcurrentDictionary<string, Pessoa> pessasMap,
        IConnectionMultiplexer multiplexer)
    {
        _pessasMap = pessasMap;
        _multiplexer = multiplexer;

        var sub = _multiplexer.GetSubscriber();

        sub.Subscribe("busca", async (channel, message) =>
        {
            var pessoa = JsonSerializer.Deserialize<Pessoa>(message);
            var buscaStackValue = pessoa.Stack == null ? "" : string.Join("", pessoa.Stack.Select(s => s.ToString()));
            var buscaValue = $"{pessoa.Apelido}{pessoa.Nome}{buscaStackValue}" ?? "";
            _pessasMap.TryAdd(buscaValue, pessoa);
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // i know, i'm sorry to disappoint you
        return;
    }
}