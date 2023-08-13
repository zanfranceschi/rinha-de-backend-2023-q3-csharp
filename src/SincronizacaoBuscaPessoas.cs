using System.Collections.Concurrent;
using StackExchange.Redis;
using System.Text.Json;

namespace RinhaDeBackEnd;
public class SincronizacaoBuscaPessoas
    : BackgroundService
{
    private readonly ConcurrentDictionary<string, Pessoa> _pessoasMap;
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ISubscriber _subscriber;

    public SincronizacaoBuscaPessoas(
        ConcurrentDictionary<string, Pessoa> pessoasMap,
        IConnectionMultiplexer multiplexer)
    {
        _pessoasMap = pessoasMap;
        _multiplexer = multiplexer;
        _subscriber = _multiplexer.GetSubscriber();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _subscriber.SubscribeAsync("busca", async (channel, message) =>
        {
            var pessoa = JsonSerializer.Deserialize<Pessoa>(message);
            var buscaStackValue = pessoa.Stack == null ? "" : string.Join("", pessoa.Stack.Select(s => s.ToString()));
            var buscaValue = $"{pessoa.Apelido}{pessoa.Nome}{buscaStackValue}" ?? "";
            _pessoasMap.TryAdd(buscaValue, pessoa);
        });
    }
}