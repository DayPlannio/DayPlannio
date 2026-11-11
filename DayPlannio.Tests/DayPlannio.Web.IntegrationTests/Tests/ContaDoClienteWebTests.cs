using System.Net;
using System.Net.Http.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using DayPlannio.Web.IntegrationTests.Infra;
using Xunit;
using Campos = System.Collections.Generic.Dictionary<string, string>;

namespace DayPlannio.Web.IntegrationTests.Tests;

[Collection(PortalCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class ContaDoClienteWebTests
{
    private readonly PortalFixture _fx;
    public ContaDoClienteWebTests(PortalFixture fx) => _fx = fx;

    [Theory(DisplayName = "WI-10 Rotas antigas e atalhos redirecionam para as telas certas")]
    [InlineData("/", "Login")]
    [InlineData("/Home/Login", "Login")]
    [InlineData("/Home/Metricas", "Metricas")]
    [InlineData("/Admin/Login", "Login")]
    public async Task Atalhos_Redirecionam(string rota, string destino)
    {
        var r = await _fx.Web.NovoNavegador().GetAsync(rota);

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains(destino, r.Destino());
    }

    [Fact(DisplayName = "WI-11 A rota /Login continua entregando a tela de login")]
    public async Task RotaLoginCompativel_EntregaTela()
    {
        var r = await _fx.Web.NovoNavegador().GetAsync("/Login");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Acesse sua conta", await r.HtmlAsync());
    }

    [Fact(DisplayName = "WI-12 Sair do portal encerra a sessão do cliente")]
    public async Task Logout_EncerraSessao()
    {
        var cliente = await _fx.ClienteLogadoAsync();

        var saida = await cliente.Nav.GetAsync("/Account/Logout");
        var depois = await cliente.Nav.GetAsync("/Metricas");

        Assert.Equal(HttpStatusCode.Redirect, saida.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, depois.StatusCode);
        Assert.Contains("/Account/Login", depois.Destino());
    }

    [Fact(DisplayName = "WI-13 Esqueci a senha: e-mail vazio mostra erro e e-mail desconhecido segue sem revelar nada")]
    public async Task EsqueciSenha_Passo1()
    {
        var nav = _fx.Web.NovoNavegador();
        Assert.Equal(HttpStatusCode.OK, (await nav.GetAsync("/Account/EsqueciSenha")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await nav.GetAsync("/Account/EsqueciSenha?editar=1")).StatusCode);

        var vazio = await nav.EnviarFormAsync("/Account/EsqueciSenha", new() { ["Passo"] = "1", ["Email"] = "" });
        var desconhecido = await nav.EnviarFormAsync("/Account/EsqueciSenha",
            new() { ["Passo"] = "1", ["Email"] = $"ninguem_{Guid.NewGuid():N}@teste.local" });

        Assert.Contains("Informe seu e-mail de acesso.", await vazio.HtmlAsync());
        Assert.Equal(HttpStatusCode.OK, desconhecido.StatusCode);
        Assert.Contains("enviamos um código de recuperação", await desconhecido.HtmlAsync());
    }

    [Fact(DisplayName = "WI-14 Esqueci a senha: cliente sem e-mail de recuperação recebe a orientação da API")]
    public async Task EsqueciSenha_SemEmailDeRecuperacao()
    {
        var p = await _fx.Api.CriarPrestadorAsync();
        var acesso = await p.GerarAcessoClienteAsync(await p.CriarClienteAsync());

        var r = await _fx.Web.NovoNavegador().EnviarFormAsync("/Account/EsqueciSenha",
            new() { ["Passo"] = "1", ["Email"] = acesso.Email });

        Assert.Contains("Nenhum e-mail de recuperação cadastrado", await r.HtmlAsync());
    }

    [Fact(DisplayName = "WI-15 Esqueci a senha: código por e-mail → nova senha → login com a nova senha")]
    public async Task EsqueciSenha_FluxoCompleto()
    {
        var p = await _fx.Api.CriarPrestadorAsync();
        var acesso = await p.GerarAcessoClienteAsync(await p.CriarClienteAsync());
        var recuperacao = $"rec_{Guid.NewGuid():N}@teste.local";
        (await _fx.Api.ClientAdmin().PostAsJsonAsync("api/Cliente/definir-email-secundario",
            new { clienteId = acesso.Id, emailSecundario = recuperacao })).EnsureSuccessStatusCode();
        var nav = _fx.Web.NovoNavegador();

        var passo1 = await nav.EnviarFormAsync("/Account/EsqueciSenha", new() { ["Passo"] = "1", ["Email"] = acesso.Email });
        Assert.Contains("enviamos um código de recuperação", await passo1.HtmlAsync());
        var codigo = _fx.Api.Emails.UltimoCodigoPara(recuperacao);
        Assert.NotNull(codigo);

        var reenviar = await nav.EnviarFormAsync("/Account/EsqueciSenha",
            new() { ["Passo"] = "2", ["Email"] = acesso.Email, ["acao"] = "reenviar" });
        Assert.Contains("Reenviamos", await reenviar.HtmlAsync());

        Campos Passo2(string cod, string nova, string conf) => new()
        {
            ["Passo"] = "2", ["Email"] = acesso.Email, ["Code"] = cod, ["SenhaNova"] = nova, ["SenhaNovaConfirmacao"] = conf
        };

        var naoConfere = await nav.EnviarFormAsync("/Account/EsqueciSenha", Passo2(codigo!, "Nova@12345", "Outra@12345"));
        var semCodigo = await nav.EnviarFormAsync("/Account/EsqueciSenha", Passo2("", "Nova@12345", "Nova@12345"));
        var fraca = await nav.EnviarFormAsync("/Account/EsqueciSenha", Passo2(codigo!, "abc", "abc"));
        var codigoErrado = await nav.EnviarFormAsync("/Account/EsqueciSenha", Passo2("000000", "Nova@12345", "Nova@12345"));

        Assert.Contains("não conferem", await naoConfere.HtmlAsync());
        Assert.Contains("Informe o código e a nova senha.", await semCodigo.HtmlAsync());
        Assert.Contains("não atende aos requisitos", await fraca.HtmlAsync());
        Assert.Contains("Código inválido", await codigoErrado.HtmlAsync());

        var ok = await nav.EnviarFormAsync("/Account/EsqueciSenha", Passo2(codigo!, "Nova@12345", "Nova@12345"));
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);

        var login = await nav.GetAsync(ok.Destino());
        Assert.Contains("Senha redefinida com sucesso", await login.HtmlAsync());

        var entrar = await _fx.Web.NovoNavegador().EnviarFormAsync("/Account/Login",
            new() { ["Email"] = acesso.Email, ["Senha"] = "Nova@12345" });
        Assert.Equal(HttpStatusCode.Redirect, entrar.StatusCode);
        Assert.Contains("Metricas", entrar.Destino());
    }

    [Fact(DisplayName = "WI-16 Troca de senha: confirmação diferente, campos vazios, senha atual errada e senha repetida")]
    public async Task TrocarSenha_Validacoes()
    {
        var cliente = await _fx.ClienteLogadoAsync();
        var nav = cliente.Nav;
        Assert.Equal(HttpStatusCode.OK, (await nav.GetAsync("/Account/TrocarSenha")).StatusCode);

        Campos Form(string atual, string nova, string conf) => new()
        {
            ["SenhaAtual"] = atual, ["SenhaNova"] = nova, ["SenhaNovaConfirmacao"] = conf, ["EmailSecundario"] = ""
        };

        var naoConfere = await nav.EnviarFormAsync("/Account/TrocarSenha", Form(cliente.Senha, "Outra@12345", "Diferente@123"));
        var vazia = await nav.EnviarFormAsync("/Account/TrocarSenha", Form("", "Outra@12345", "Outra@12345"));
        var igual = await nav.EnviarFormAsync("/Account/TrocarSenha", Form(cliente.Senha, cliente.Senha, cliente.Senha));
        var atualErrada = await nav.EnviarFormAsync("/Account/TrocarSenha", Form("Errada@12345", "Outra@12345", "Outra@12345"));

        Assert.Contains("não conferem", await naoConfere.HtmlAsync());
        Assert.Contains("Preencha todos os campos.", await vazia.HtmlAsync());
        Assert.Contains("não pode ser igual", await igual.HtmlAsync());
        Assert.Contains("Senha atual inválida", await atualErrada.HtmlAsync());
    }

    [Fact(DisplayName = "WI-17 Perfil: mostra o e-mail de recuperação e valida o que o cliente salva")]
    public async Task Perfil_ExibeEValida()
    {
        var cliente = await _fx.ClienteLogadoAsync();
        var nav = cliente.Nav;

        var tela = await nav.GetAsync("/Account/Perfil");
        Assert.Equal(HttpStatusCode.OK, tela.StatusCode);
        var html = await tela.HtmlAsync();
        Assert.Contains("Meu Perfil", html);
        Assert.Contains(cliente.EmailRecuperacao, html);

        var vazio = await nav.EnviarFormAsync("/Account/Perfil", new() { ["EmailSecundario"] = "" });
        var invalido = await nav.EnviarFormAsync("/Account/Perfil", new() { ["EmailSecundario"] = "isso-nao-e-email" });
        var novo = $"novo_{Guid.NewGuid():N}@teste.local";
        var valido = await nav.EnviarFormAsync("/Account/Perfil", new() { ["EmailSecundario"] = novo });

        Assert.Contains("Informe o e-mail de recuperação.", await vazio.HtmlAsync());
        Assert.Contains("Não foi possível salvar o e-mail de recuperação", await invalido.HtmlAsync());
        Assert.Contains("salvo com sucesso", await valido.HtmlAsync());
        Assert.Contains(novo, await (await nav.GetAsync("/Account/Perfil")).HtmlAsync());
    }

    [Fact(DisplayName = "WI-18 Perfil e telas da conta exigem login")]
    public async Task TelasDaConta_ExigemLogin()
    {
        var nav = _fx.Web.NovoNavegador();

        foreach (var rota in new[] { "/Account/Perfil", "/Account/TrocarSenha", "/Account/EncerrarConta", "/Account/RecusarPoliticaPrivacidade" })
        {
            var r = await nav.GetAsync(rota);
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.Contains("/Account/Login", r.Destino());
        }
    }

    [Fact(DisplayName = "WI-19 Política de privacidade: visitante só lê; cliente logado concorda e volta ao perfil")]
    public async Task Politica_VisitanteECliente()
    {
        var visitante = _fx.Web.NovoNavegador();
        Assert.Contains("Política de Privacidade", await (await visitante.GetAsync("/Account/PoliticaPrivacidade")).HtmlAsync());

        var leu = await visitante.EnviarFormAsync("/Account/PoliticaPrivacidade", new() { ["Escolha"] = "concordar" });
        Assert.Equal(HttpStatusCode.Redirect, leu.StatusCode);
        Assert.Contains("Obrigada por ler", await (await visitante.GetAsync(leu.Destino())).HtmlAsync());

        var recusa = await visitante.EnviarFormAsync("/Account/PoliticaPrivacidade", new() { ["Escolha"] = "nao_concordar" });
        Assert.Contains("Login", recusa.Destino());
        Assert.Contains("encerre a sua conta", await (await visitante.GetAsync(recusa.Destino())).HtmlAsync());

        var cliente = await _fx.ClienteLogadoAsync();
        var concorda = await cliente.Nav.EnviarFormAsync("/Account/PoliticaPrivacidade", new() { ["Escolha"] = "concordar" });
        Assert.Contains("Perfil", concorda.Destino());
        Assert.Contains("Seu acesso permanece liberado", await (await cliente.Nav.GetAsync(concorda.Destino())).HtmlAsync());
    }

    [Fact(DisplayName = "WI-20 Não concordar com a política encerra a conta do cliente")]
    public async Task Politica_NaoConcordar_EncerraConta()
    {
        var cliente = await _fx.ClienteLogadoAsync();

        var escolha = await cliente.Nav.EnviarFormAsync("/Account/PoliticaPrivacidade", new() { ["Escolha"] = "nao_concordar" });
        Assert.Contains("RecusarPoliticaPrivacidade", escolha.Destino());

        var tela = await cliente.Nav.GetAsync("/Account/RecusarPoliticaPrivacidade");
        Assert.Contains("Você não concorda mais", await tela.HtmlAsync());

        var encerra = await cliente.Nav.EnviarFormAsync("/Account/RecusarPoliticaPrivacidade", new());
        Assert.Equal(HttpStatusCode.Redirect, encerra.StatusCode);

        var api = await _fx.Api.NovoClient().PostAsJsonAsync("api/Cliente/login",
            new { email = cliente.Acesso.Email, senha = cliente.Senha });
        await api.AssertStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "WI-21 Encerrar conta: exige digitar ENCERRAR e depois remove o acesso")]
    public async Task EncerrarConta_Fluxo()
    {
        var cliente = await _fx.ClienteLogadoAsync();
        var servico = await cliente.Prestador.CriarServicoAsync();
        await cliente.Prestador.CriarAgendamentoAsync(cliente.Acesso.Id, servico, ApiHelper.Futuro(2));

        var tela = await cliente.Nav.GetAsync("/Account/EncerrarConta");
        Assert.Contains("Encerrar conta", await tela.HtmlAsync());

        var errado = await cliente.Nav.EnviarFormAsync("/Account/EncerrarConta", new() { ["Confirmacao"] = "talvez" });
        Assert.Equal(HttpStatusCode.Redirect, errado.StatusCode);
        Assert.Contains("Digite ENCERRAR", await (await cliente.Nav.GetAsync(errado.Destino())).HtmlAsync());

        var certo = await cliente.Nav.EnviarFormAsync("/Account/EncerrarConta", new() { ["Confirmacao"] = "encerrar" });
        Assert.Equal(HttpStatusCode.Redirect, certo.StatusCode);
        Assert.Contains("Login", certo.Destino());
        Assert.Contains("Conta encerrada", await (await cliente.Nav.GetAsync(certo.Destino())).HtmlAsync());

        var depois = await cliente.Nav.GetAsync("/Metricas");
        Assert.Contains("/Account/Login", depois.Destino());
        var api = await _fx.Api.NovoClient().PostAsJsonAsync("api/Cliente/login",
            new { email = cliente.Acesso.Email, senha = cliente.Senha });
        await api.AssertStatusAsync(HttpStatusCode.Unauthorized);
    }
}
