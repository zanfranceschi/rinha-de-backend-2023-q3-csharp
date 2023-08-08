using System.Collections.Concurrent;
using Npgsql;
using NpgsqlTypes;
using RinhaDeBackEnd;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNpgsqlDataSource(
    Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ??
    "ERRO de connection string!!!");

//builder.Services.AddSingleton(sp => new ConcurrentQueue<Pessoa>());
//builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapGet("/", async (HttpContext http, NpgsqlConnection conn) =>
{
    await using (conn)
    {
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select count(1) from pessoas";
        var count = await cmd.ExecuteScalarAsync();
        return $"contagem: {count} / {Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")}";
    }
});

app.MapPost("/pessoas", async (HttpContext http, NpgsqlConnection conn, Pessoa pessoa) =>
{
    if (!pessoa.Nascimento.HasValue ||
        string.IsNullOrEmpty(pessoa.Nome) ||
        pessoa.Nome.Length > 100 ||
        string.IsNullOrEmpty(pessoa.Apelido) ||
        pessoa.Apelido.Length > 32)
    {
        http.Response.StatusCode = 422;
        return "unprocessable entity - primeira verificação";
    }

    foreach (var item in pessoa.Stack ?? Enumerable.Empty<string>())
        if (item.Length > 32 || item.Length == 0)
            return "unprocessable entity - stack zuada";

    await using (conn)
    {
        await conn.OpenAsync();

        pessoa.Id = Guid.NewGuid();

        try
        {
            await using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = "insert into pessoas (id, apelido, nome, nascimento, stack, busca) values ($1, $2, $3, $4, $5, $6)";
            insertCmd.Parameters.AddWithValue(pessoa.Id);
            insertCmd.Parameters.AddWithValue(pessoa.Apelido);
            insertCmd.Parameters.AddWithValue(pessoa.Nome);
            insertCmd.Parameters.AddWithValue(pessoa.Nascimento.Value);

            if (pessoa.Stack is not null)
                insertCmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb, pessoa.Stack);
            else
                insertCmd.Parameters.AddWithValue(DBNull.Value);

            var busca = $"{pessoa.Apelido}{pessoa.Nome}{string.Join("", pessoa.Stack?.Select(i => i) ?? new List<string>())}";

            insertCmd.Parameters.AddWithValue(busca);

            await insertCmd.ExecuteNonQueryAsync();
        }
        catch (Npgsql.PostgresException ex)
        {
            if (ex?.ConstraintName == "pessoas_apelido_key")
            {
                http.Response.StatusCode = 422;
                return "unprocessable entity - apelido já usado";
            }
        }
    }

    http.Response.Headers.Location = $"/pessoas/{pessoa.Id}";
    http.Response.StatusCode = 201;
    return "created";
});

app.MapGet("/pessoas/{id}", async (HttpContext http, NpgsqlConnection conn, Guid id) =>
{
    await using (conn)
    {
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select apelido, nome, nascimento, stack from pessoas where id = $1 limit 1";
        cmd.Parameters.AddWithValue(id);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            http.Response.StatusCode = 200;
            var (apelido, nome, nascimento, stack) = (reader["apelido"], reader["nome"], reader["nascimento"], reader["stack"]);
            return $"id: {id}, apelido: {apelido}, nome: {nome}, nascimento: {nascimento}, stack: {stack}";
        }
    }
    http.Response.StatusCode = 404;
    return "not found";
});

app.MapGet("/pessoas", async (HttpContext http, NpgsqlConnection conn, string? t) =>
{
    if (string.IsNullOrEmpty(t))
    {
        http.Response.StatusCode = 400;
        return new { pessoas = new List<Pessoa>(), erro = "t não informado" };
    }

    var resultado = new List<Pessoa>();

    await using (conn)
    {
        await conn.OpenAsync();
        await using var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "select id, apelido, nome, nascimento, stack from pessoas where busca like $1 limit 50";
        selectCmd.Parameters.AddWithValue($"%{t}%");

        var reader = await selectCmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var (id, apelido, nome, nascimento, stack) = (reader.GetGuid(0),
                                                          reader.GetString(1),
                                                          reader.GetString(2),
                                                          reader.GetDateTime(3),
                                                          reader["stack"] != DBNull.Value ? reader.GetFieldValue<IEnumerable<string>>(4) : null
                                                          );
            resultado.Add(new Pessoa { Id = id, Apelido = apelido, Nome = nome, Nascimento = DateOnly.FromDateTime(nascimento), Stack = stack });
        }
    }
    return new { pessoas = resultado, erro = "null" };
});

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
