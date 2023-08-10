using Npgsql;
using RinhaDeBackEnd;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNpgsqlDataSource(
    Environment.GetEnvironmentVariable(
        "DB_CONNECTION_STRING") ??
        "ERRO de connection string!!!");

ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(
    Environment.GetEnvironmentVariable(
        "CACHE_CONNECTION_STRING") ??
        "localhost");

builder.Services.AddTransient(_ =>
{
    return redis.GetDatabase();
});

builder.Services.AddSingleton(_ =>
{
    return new ConcurrentQueue<Pessoa>();
});

builder.Services.AddHostedService<InsercaoRegistrosPessoas>();

var app = builder.Build();

app.MapPost("/pessoas", async (HttpContext http,
                               IDatabase cache,
                               ConcurrentQueue<Pessoa> processingQueue,
                               Pessoa pessoa) =>
{
    if (!Pessoa.BasicamenteValida(pessoa))
    {
        http.Response.StatusCode = 422;
        return new ResponseCriacao { Erro = "afe..." };
    }

    var apelidoUsado = await cache.StringGetAsync($"pessoa-apelido:{pessoa.Apelido}");

    if (apelidoUsado.HasValue)
    {
        http.Response.StatusCode = 422;
        return new ResponseCriacao { Erro = "esse apelido já existe" };
    }

    // Daqui pra baixo é só baixaria pra ficar bem na rinha kkkk
    // Nunca confie numa fila na memória pra coisas importantes, nunca!
    pessoa.Id = Guid.NewGuid();
    var json = cache.JSON();
    await json.SetAsync($"pessoa:{pessoa.Id}", "$", pessoa);
    await cache.StringSetAsync($"pessoa-apelido:{pessoa.Apelido}", ".");
    processingQueue.Enqueue(pessoa); // kkkk

    http.Response.Headers.Location = $"/pessoas/{pessoa.Id}";
    http.Response.StatusCode = 201;
    return new ResponseCriacao { Pessoa = pessoa };

}).Produces<ResponseCriacao>();

app.MapGet("/pessoas/{id}", async (HttpContext http, IDatabase cache, Guid id) =>
{
    var cachedPessoa = await cache.JSON().GetAsync<Pessoa>($"pessoa:{id}");

    if (cachedPessoa != null)
        return new ResponseConsulta { Pessoa = cachedPessoa };

    http.Response.StatusCode = 404;
    return new ResponseConsulta { Erro = "Oops" };

}).Produces<ResponseConsulta>();

app.MapGet("/pessoas", async (HttpContext http, NpgsqlConnection conn, string? t) =>
{
    if (string.IsNullOrEmpty(t))
    {
        http.Response.StatusCode = 400;
        return new ResponseBusca { Erro = "'t' não informado" };
    }

    var pessoas = new List<Pessoa>();

    await using (conn)
    {
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select * from pessoas where busca like $1 limit 50;";
        cmd.Parameters.AddWithValue($"%{t}%");
        var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var (id, apelido, nome, nascimento, stack) = (reader.GetGuid(0),
                                                          reader.GetString(1),
                                                          reader.GetString(2),
                                                          reader.GetDateTime(3),
                                                          reader["stack"] != DBNull.Value
                                                            ? reader.GetFieldValue<IEnumerable<string>>(4)
                                                            : null
                                                          );
            pessoas.Add(new Pessoa
            {
                Id = id,
                Apelido = apelido,
                Nome = nome,
                Nascimento = DateOnly.FromDateTime(nascimento),
                Stack = stack
            });
        }
    }

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
