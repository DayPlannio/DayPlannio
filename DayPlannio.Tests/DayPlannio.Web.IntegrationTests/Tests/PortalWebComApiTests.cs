using System.Net;
using DayPlannio.Api.IntegrationTests.Infra;
using DayPlannio.Web.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Web.IntegrationTests.Tests;

[Collection(PortalCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class PortalWebComApiTests
{
    private readonly PortalFixture _fx;
    public PortalWebComApiTests(PortalFixture fx) => _fx = fx;

    private async Task<(Prestador prestador, ClienteCriado acesso)> NovoClienteComAcessoAsync()
    {
        var p = await _fx.Api.CriarPrestadorAsync();
        var id = await p.CriarClienteAsync("Cliente do Portal");
        return (p, await p.GerarAcessoClienteAsync(id));
    }

    [Fact(DisplayName = "WI-01 A tela de login do portal é entregue")]
    public async Task Login_Get_RetornaPagina()
    {
        var html = WebHelper.Texto(await _fx.Web.NovoNavegador().GetStringAsync("/Account/Login"));

        Assert.Contains("Acesse sua conta", html);
        Assert.Contains("Entrar", html);
    }

    [Fact(DisplayName = "WI-02 Login com campos vazios mostra a mensagem de validação (sem chamar a API)")]
    public async Task Login_CamposVazios_MostraErro()
    {
        var r = await _fx.Web.NovoNavegador().EnviarFormAsync("/Account/Login",
            new() { ["Email"] = "", ["Senha"] = "" });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Informe e-mail e senha.", WebHelper.Texto(await r.Content.ReadAsStringAsync()));
    }

    [Fact(DisplayName = "WI-03 Login com credenciais inválidas: Web consulta a API (cliente e admin) e mostra erro")]
    public async Task Login_Invalido_MostraCredenciaisInvalidas()
    {
        var r = await _fx.Web.NovoNavegador().EnviarFormAsync("/Account/Login",
            new() { ["Email"] = "ninguem@teste.local", ["Senha"] = "Errada@123" });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Credenciais inválidas.", WebHelper.Texto(await r.Content.ReadAsStringAsync()));
    }

    [Fact(DisplayName = "WI-04 Cliente com senha provisória é levado à troca de senha (primeiro acesso)")]
    public async Task Login_Cliente_PrimeiroAcesso_RedirecionaParaTrocarSenha()
    {
        var (_, acesso) = await NovoClienteComAcessoAsync();

        var r = await _fx.Web.NovoNavegador().EnviarFormAsync("/Account/Login",
            new() { ["Email"] = acesso.Email, ["Senha"] = acesso.SenhaProvisoria });

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Account/TrocarSenha", r.Destino());
    }

    [Fact(DisplayName = "WI-05 Rota do dashboard sem login redireciona para o login")]
    public async Task Dashboard_SemLogin_RedirecionaParaLogin()
    {
        var r = await _fx.Web.NovoNavegador().GetAsync("/Metricas");

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Account/Login", r.Destino());
    }

    [Fact(DisplayName = "WI-06 Admin semeado na API entra no portal e é levado ao painel")]
    public async Task Login_Admin_RedirecionaParaPainel()
    {
        var r = await _fx.Web.NovoNavegador().EnviarFormAsync("/Account/Login",
            new() { ["Email"] = ApiFactory.AdminEmail, ["Senha"] = ApiFactory.AdminSenha });

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Admin/Painel", r.Destino());
    }

    [Fact(DisplayName = "WI-07 Cliente não acessa o painel de Admin")]
    public async Task Cliente_NaoAcessaPainelAdmin()
    {
        var (_, acesso) = await NovoClienteComAcessoAsync();
        var nav = _fx.Web.NovoNavegador();
        await nav.EnviarFormAsync("/Account/Login", new() { ["Email"] = acesso.Email, ["Senha"] = acesso.SenhaProvisoria });

        var r = await nav.GetAsync("/Admin/Painel");

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Account/Login", r.Destino());
    }

    [Fact(DisplayName = "WI-08 Fluxo do cliente: login → trocar senha → aceitar política → métricas (Web ↔ API ↔ Mongo)")]
    public async Task FluxoCompletoDoCliente_AteAsMetricas()
    {
        var (prestador, acesso) = await NovoClienteComAcessoAsync();
        var servico = await prestador.CriarServicoAsync("Pintura", 120);
        var ag = await prestador.CriarAgendamentoAsync(acesso.Id, servico, ApiHelper.Futuro(2), valor: 300m);
        (await prestador.Client.PutAsync($"api/Agendamentos/concluir/{ag}", null)).EnsureSuccessStatusCode();

        var nav = _fx.Web.NovoNavegador();
        const string novaSenha = "MinhaNova@123";

        var login = await nav.EnviarFormAsync("/Account/Login",
            new() { ["Email"] = acesso.Email, ["Senha"] = acesso.SenhaProvisoria });
        Assert.Contains("/Account/TrocarSenha", login.Destino());

        var troca = await nav.EnviarFormAsync("/Account/TrocarSenha", new()
        {
            ["SenhaAtual"] = acesso.SenhaProvisoria,
            ["SenhaNova"] = novaSenha,
            ["SenhaNovaConfirmacao"] = novaSenha,
            ["EmailSecundario"] = "recuperacao@teste.local"
        });
        Assert.Equal(HttpStatusCode.Redirect, troca.StatusCode);
        Assert.Contains("PoliticaPrivacidade", troca.Destino());

        var politica = await nav.EnviarFormAsync("/Account/PoliticaPrivacidade", new() { ["Escolha"] = "concordar" });
        Assert.Equal(HttpStatusCode.Redirect, politica.StatusCode);
        Assert.Contains("/Metricas", politica.Destino());

        var metricas = await nav.GetAsync("/Metricas");
        Assert.Equal(HttpStatusCode.OK, metricas.StatusCode);
        Assert.Contains("Meus Indicadores", WebHelper.Texto(await metricas.Content.ReadAsStringAsync()));

        var antiga = await _fx.Web.NovoNavegador().EnviarFormAsync("/Account/Login",
            new() { ["Email"] = acesso.Email, ["Senha"] = acesso.SenhaProvisoria });
        Assert.Equal(HttpStatusCode.OK, antiga.StatusCode);

        var nova = await _fx.Web.NovoNavegador().EnviarFormAsync("/Account/Login",
            new() { ["Email"] = acesso.Email, ["Senha"] = novaSenha });
        Assert.Contains("/Metricas", nova.Destino());
    }

    [Fact(DisplayName = "WI-09 Troca de senha no portal recusa senha fraca e mostra a mensagem da API/validação")]
    public async Task TrocarSenha_Fraca_MostraErro()
    {
        var (_, acesso) = await NovoClienteComAcessoAsync();
        var nav = _fx.Web.NovoNavegador();
        await nav.EnviarFormAsync("/Account/Login", new() { ["Email"] = acesso.Email, ["Senha"] = acesso.SenhaProvisoria });

        var r = await nav.EnviarFormAsync("/Account/TrocarSenha", new()
        {
            ["SenhaAtual"] = acesso.SenhaProvisoria,
            ["SenhaNova"] = "abc",
            ["SenhaNovaConfirmacao"] = "abc",
            ["EmailSecundario"] = "x@teste.local"
        });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("não atende aos requisitos", WebHelper.Texto(await r.Content.ReadAsStringAsync()));
    }
}
