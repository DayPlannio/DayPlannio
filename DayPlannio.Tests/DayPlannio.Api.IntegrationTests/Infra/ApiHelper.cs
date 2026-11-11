using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Infra;

public sealed record Prestador(HttpClient Client, Guid Id, string Email, string Senha, string Nome);
public sealed record ClienteCriado(Guid Id, string Email, string SenhaProvisoria);

public static class ApiHelper
{
    public const string SenhaPadrao = "Senha@123";

    public static HttpClient NovoClient(this ApiFactory f) =>
        f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public static HttpClient ClientAdmin(this ApiFactory f)
    {
        var c = f.NovoClient();
        c.DefaultRequestHeaders.Add("X-Api-Key", ApiFactory.ApiKey);
        return c;
    }

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage r) =>
        await r.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<string> MensagemAsync(this HttpResponseMessage r)
    {
        var corpo = await r.Content.ReadAsStringAsync();
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("message", out var m))
                return m.GetString() ?? string.Empty;
        }
        catch (JsonException) { }
        return corpo;
    }

    public static async Task AssertStatusAsync(this HttpResponseMessage r, HttpStatusCode esperado)
    {
        if (r.StatusCode != esperado)
        {
            var corpo = await r.Content.ReadAsStringAsync();
            Assert.Fail($"Esperado {(int)esperado} {esperado}, recebido {(int)r.StatusCode} {r.StatusCode}. Corpo: {corpo}");
        }
    }

    public static void AssertNaoAutenticado(this HttpResponseMessage r) =>
        Assert.True(r.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Esperado 401/302, recebido {(int)r.StatusCode}.");

    public static void AssertProibido(this HttpResponseMessage r) =>
        Assert.True(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Esperado 403/302, recebido {(int)r.StatusCode}.");

    public static DateTime Futuro(int dias, int hora = 9) =>
        DateTime.UtcNow.Date.AddDays(dias).AddHours(hora);

    public static async Task<Prestador> CriarPrestadorAsync(
        this ApiFactory f, bool testeGratuito = true, string? plano = null)
    {
        var id = Guid.NewGuid().ToString("N");
        var email = $"prest_{id}@teste.local";
        var nome = $"Prestador Teste {id[..8]}";
        var client = f.NovoClient();

        var resp = await client.PostAsJsonAsync("api/Users/create", new
        {
            nomeCompleto = nome,
            email,
            senha = SenhaPadrao,
            confirmeSenha = SenhaPadrao,
            aceitouPolitica = true,
            usarTesteGratuito = testeGratuito,
            plano
        });
        await resp.AssertStatusAsync(HttpStatusCode.OK);

        var json = await resp.JsonAsync();
        return new Prestador(client, json.GetProperty("id").GetGuid(), email, SenhaPadrao, nome);
    }

    public static async Task<Guid> CriarClienteAsync(this Prestador p, string? nome = null)
    {
        var resp = await p.Client.PostAsJsonAsync("api/Cliente/create", new
        {
            usuarioId = p.Id,
            nome = nome ?? $"Cliente {Guid.NewGuid():N}"[..16],
            telefone = "14999990000",
            endereco = "Rua dos Testes, 100 - Jaú/SP"
        });
        await resp.AssertStatusAsync(HttpStatusCode.OK);
        return (await resp.JsonAsync()).GetProperty("id").GetGuid();
    }

    public static async Task<Guid> CriarServicoAsync(this Prestador p, string tipo = "Limpeza", int minutos = 60)
    {
        var resp = await p.Client.PostAsJsonAsync("api/TiposServico/create", new
        {
            usuarioId = p.Id,
            tipo,
            descricao = "Serviço criado pelo teste",
            tempoEstimado = minutos
        });
        await resp.AssertStatusAsync(HttpStatusCode.OK);
        return (await resp.JsonAsync()).GetProperty("id").GetGuid();
    }

    public static async Task<HttpResponseMessage> CriarAgendamentoRawAsync(
        this Prestador p, Guid clienteId, Guid servicoId, DateTime dataHoraUtc, decimal valor = 150m) =>
        await p.Client.PostAsJsonAsync("api/Agendamentos/create", new
        {
            usuarioId = p.Id,
            clienteId,
            tipoServicoId = servicoId,
            dataHora = dataHoraUtc,
            valorCobrado = valor,
            custoMaterial = 20m
        });

    public static async Task<Guid> CriarAgendamentoAsync(
        this Prestador p, Guid clienteId, Guid servicoId, DateTime dataHoraUtc, decimal valor = 150m)
    {
        var resp = await p.CriarAgendamentoRawAsync(clienteId, servicoId, dataHoraUtc, valor);
        await resp.AssertStatusAsync(HttpStatusCode.OK);
        return (await resp.JsonAsync()).GetProperty("id").GetGuid();
    }

    public static async Task<ClienteCriado> GerarAcessoClienteAsync(this Prestador p, Guid clienteId)
    {
        var resp = await p.Client.PostAsync($"api/Cliente/gerar-credenciais/{clienteId}", null);
        await resp.AssertStatusAsync(HttpStatusCode.OK);
        var json = await resp.JsonAsync();
        return new ClienteCriado(
            clienteId,
            json.GetProperty("email").GetString()!,
            json.GetProperty("senhaProvisoria").GetString()!);
    }
}
