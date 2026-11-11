using System.Net;
using System.Net.Http.Json;
using DayPlannio.Api.Models;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Infra;

public static class AdminHelper
{
    public static async Task<HttpClient> ClienteAdminLogadoAsync(this ApiFactory f)
    {
        var client = f.NovoClient();
        var r = await client.PostAsJsonAsync("api/Admin/login",
            new { email = ApiFactory.AdminEmail, senha = ApiFactory.AdminSenha });
        await r.AssertStatusAsync(HttpStatusCode.OK);
        return client;
    }

    public static async Task<Guid> AdminIdAsync(this ApiFactory f)
    {
        var r = await f.NovoClient().PostAsJsonAsync("api/Admin/login",
            new { email = ApiFactory.AdminEmail, senha = ApiFactory.AdminSenha });
        await r.AssertStatusAsync(HttpStatusCode.OK);
        return (await r.JsonAsync()).GetProperty("userId").GetGuid();
    }

    public static async Task<Foto> InserirFotoAsync(
        this Prestador p, Guid agendamentoId, bool publica = true, bool aprovada = false, string? descricao = "Foto de teste")
    {
        var foto = new Foto
        {
            Id = Guid.NewGuid(),
            AgendamentoId = agendamentoId,
            PrestadorId = p.Id,
            Url = $"https://contateste.blob.core.windows.net/fotos/2026/11/05/{Guid.NewGuid():N}_foto.png",
            Descricao = descricao,
            Publica = publica,
            Aprovado = aprovada,
            DataUpload = DateTime.UtcNow
        };
        await new ContextMongodb().Foto.InsertOneAsync(foto);
        return foto;
    }

    public static async Task<Prestador> CriarPrestadorComPlanoAsync(this ApiFactory f, string plano)
    {
        var p = await f.CriarPrestadorAsync(testeGratuito: false, plano: plano);
        var r = await f.ClientAdmin().PostAsync($"api/Admin/planos/{p.Id}/aprovar", null);
        await r.AssertStatusAsync(HttpStatusCode.OK);
        return p;
    }

    public static async Task EsperarAsync(Func<bool> condicao, int tentativas = 50)
    {
        for (var i = 0; i < tentativas && !condicao(); i++)
            await Task.Delay(100);
        Assert.True(condicao(), "A condição esperada não aconteceu a tempo.");
    }
}
