using Npgsql;
using RinhaDeBackEnd;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNpgsqlDataSource(
    Environment.GetEnvironmentVariable(
        "DB_CONNECTION_STRING") ??
        "ERRO de connection string!!!");

var redisConnectionString = Environment.GetEnvironmentVariable(
        "CACHE_CONNECTION_STRING") ??
        "localhost";

ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnectionString);

builder.Services.AddSingleton(_ => new ConcurrentDictionary<string, Pessoa>());
builder.Services.AddSingleton( _ => new ConcurrentQueue<Pessoa>());
builder.Services.AddSingleton<IConnectionMultiplexer>( _ => ConnectionMultiplexer.Connect(redisConnectionString));

builder.Services.AddTransient(_ =>
{
    return redis.GetDatabase();
});

builder.Services.AddHostedService<InsercaoRegistrosPessoas>();
builder.Services.AddHostedService<SincronizacaoBuscaPessoas>();

var app = builder.Build();

app.MapPost("/pessoas", async (HttpContext http,
                               IConnectionMultiplexer multiplexer,
                               ConcurrentQueue<Pessoa> processingQueue,
                               Pessoa pessoa) =>
{
    
    var cache = multiplexer.GetDatabase();
    
    if (!Pessoa.BasicamenteValida(pessoa))
    {
        http.Response.StatusCode = 422;
        return new ResponseCriacao { Erro = "afe..." };
    }

    var apelidoUsado = await cache.StringGetAsync(pessoa.Apelido);

    if (apelidoUsado.HasValue)
    {
        http.Response.StatusCode = 422;
        return new ResponseCriacao { Erro = "esse apelido já existe" };
    }

    pessoa.Id = Guid.NewGuid();
    await cache.StringSetAsync(pessoa.Id.ToString(), JsonSerializer.Serialize(pessoa));
    await cache.StringSetAsync(pessoa.Apelido, ".");
    processingQueue.Enqueue(pessoa);

    http.Response.Headers.Location = $"/pessoas/{pessoa.Id}";
    http.Response.StatusCode = 201;
    return new ResponseCriacao { Pessoa = pessoa };

}).Produces<ResponseCriacao>();

app.MapGet("/pessoas/{id}", async (HttpContext http, IDatabase cache, Guid id) =>
{
    var cachedPessoa = await cache.StringGetAsync(id.ToString());

    if (!cachedPessoa.IsNull)
        return new ResponseConsulta
        {
            Pessoa = JsonSerializer.Deserialize<Pessoa>(cachedPessoa)
        };

    http.Response.StatusCode = 404;
    return new ResponseConsulta { Erro = "Oops" };

}).Produces<ResponseConsulta>();

app.MapGet("/pessoas", (HttpContext http, NpgsqlConnection conn, ConcurrentDictionary<string, Pessoa> buscaMap, string? t) =>
{
    if (string.IsNullOrEmpty(t))
    {
        http.Response.StatusCode = 400;
        return new ResponseBusca { Erro = "'t' não informado" };
    }

    var pessoas = buscaMap.Where(p => p.Key.Contains(t)).Take(50).Select(p => p.Value).ToList();
    return new ResponseBusca { Resultados = pessoas };
}).Produces<ResponseBusca>();

app.MapGet("/contagem-pessoas", async (NpgsqlConnection conn) =>
{
    await using (conn)
    {
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select count(1) from pessoas";
        var count = await cmd.ExecuteScalarAsync();
        return count;
    }
});

app.Run();
