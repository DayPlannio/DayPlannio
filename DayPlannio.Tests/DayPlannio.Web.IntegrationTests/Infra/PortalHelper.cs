using System.Net;
using System.Net.Http.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Web.IntegrationTests.Infra;

public sealed record ClienteLogado(
    HttpClient Nav, Prestador Prestador, ClienteCriado Acesso, string Senha, string EmailRecuperacao);

public static class PortalHelper
{
    public const string NovaSenha = "MinhaNova@123";

    public static async Task<string> HtmlAsync(this HttpResponseMessage r) =>
        WebHelper.Texto(await r.Content.ReadAsStringAsync());

    public static async Task<ClienteLogado> ClienteLogadoAsync(
        this PortalFixture fx, Prestador? prestador = null, string nome = "Cliente do Portal")
    {
        var p = prestador ?? await fx.Api.CriarPrestadorAsync();
        var id = await p.CriarClienteAsync(nome);
        var acesso = await p.GerarAcessoClienteAsync(id);
        var nav = fx.Web.NovoNavegador();

        var login = await nav.EnviarFormAsync("/Account/Login",
            new() { ["Email"] = acesso.Email, ["Senha"] = acesso.SenhaProvisoria });
        Assert.Contains("TrocarSenha", login.Destino());

        var recuperacao = $"rec_{Guid.NewGuid():N}@teste.local";
        var troca = await nav.EnviarFormAsync("/Account/TrocarSenha", new()
        {
            ["SenhaAtual"] = acesso.SenhaProvisoria,
            ["SenhaNova"] = NovaSenha,
            ["SenhaNovaConfirmacao"] = NovaSenha,
            ["EmailSecundario"] = recuperacao
        });
        Assert.Contains("PoliticaPrivacidade", troca.Destino());

        var politica = await nav.EnviarFormAsync("/Account/PoliticaPrivacidade", new() { ["Escolha"] = "concordar" });
        Assert.Contains("Metricas", politica.Destino());

        return new ClienteLogado(nav, p, acesso, NovaSenha, recuperacao);
    }

    public static async Task<HttpClient> AdminLogadoAsync(this PortalFixture fx)
    {
        var nav = fx.Web.NovoNavegador();
        var r = await nav.EnviarFormAsync("/Account/Login",
            new() { ["Email"] = ApiFactory.AdminEmail, ["Senha"] = ApiFactory.AdminSenha });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("Painel", r.Destino());
        return nav;
    }

    public static Task<HttpResponseMessage> PostSimplesAsync(this HttpClient nav, string url, Dictionary<string, string> campos) =>
        nav.PostAsync(url, new FormUrlEncodedContent(campos));
}
