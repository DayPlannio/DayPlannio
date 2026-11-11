using System.Net;
using DayPlannio.Api.IntegrationTests.Infra;
using DayPlannio.Web.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Web.IntegrationTests.Tests;

[Collection(PortalCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class AdminWebTests
{
    private readonly PortalFixture _fx;
    public AdminWebTests(PortalFixture fx) => _fx = fx;

    private async Task<(Prestador prestador, Guid cliente, Guid agendamento)> CenarioAsync(bool planoPendente = false)
    {
        if (planoPendente)
        {
            var pendente = await _fx.Api.CriarPrestadorAsync(testeGratuito: false, plano: "Profissional");
            return (pendente, Guid.Empty, Guid.Empty);
        }

        var p = await _fx.Api.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync("Cliente do Painel");
        var servico = await p.CriarServicoAsync("Alvenaria");
        var ag = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));
        return (p, cliente, ag);
    }

    [Theory(DisplayName = "WI-30 Telas do Admin exigem login de administrador")]
    [InlineData("/Admin/Painel")]
    [InlineData("/Admin/Logs")]
    [InlineData("/Admin/Clientes")]
    [InlineData("/Admin/Prestadores")]
    [InlineData("/Admin/TodasFotos")]
    [InlineData("/Admin/Fotos")]
    public async Task TelasDoAdmin_ExigemLogin(string rota)
    {
        var r = await _fx.Web.NovoNavegador().GetAsync(rota);

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Account/Login", r.Destino());
    }

    [Fact(DisplayName = "WI-31 Painel do Admin lista prestadores, clientes, planos pendentes, fotos e logs")]
    public async Task Painel_ListaTudo()
    {
        var (pendente, _, _) = await CenarioAsync(planoPendente: true);
        var (comFoto, _, ag) = await CenarioAsync();
        await comFoto.InserirFotoAsync(ag, descricao: "Foto para aprovar");
        var admin = await _fx.AdminLogadoAsync();

        var r = await admin.GetAsync("/Admin/Painel");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.HtmlAsync();
        Assert.Contains("Painel Administrativo", html);
        Assert.Contains(pendente.Nome, html);
    }

    [Fact(DisplayName = "WI-32 Telas de logs, clientes, prestadores e fotos do Admin carregam")]
    public async Task TelasDoAdmin_Carregam()
    {
        var (p, _, ag) = await CenarioAsync();
        await p.InserirFotoAsync(ag);
        var admin = await _fx.AdminLogadoAsync();

        var logs = await (await admin.GetAsync("/Admin/Logs")).HtmlAsync();
        var clientes = await (await admin.GetAsync("/Admin/Clientes")).HtmlAsync();
        var prestadores = await (await admin.GetAsync("/Admin/Prestadores")).HtmlAsync();
        var fotos = await (await admin.GetAsync("/Admin/TodasFotos")).HtmlAsync();
        var redirecionaFotos = await admin.GetAsync("/Admin/Fotos");

        Assert.Contains("Log do Sistema", logs);
        Assert.Contains("Clientes Cadastrados", clientes);
        Assert.Contains("Cliente do Painel", clientes);
        Assert.Contains("Prestadores Cadastrados", prestadores);
        Assert.Contains(p.Nome, prestadores);
        Assert.Contains("Todas as fotos", fotos);
        Assert.Equal(HttpStatusCode.Redirect, redirecionaFotos.StatusCode);
        Assert.Contains("Painel", redirecionaFotos.Destino());
    }

    [Theory(DisplayName = "WI-33 Aprovar foto pelo painel remove a foto da fila e mostra a confirmação")]
    [InlineData("Painel")]
    [InlineData("TodasFotos")]
    [InlineData("")]
    public async Task AprovarFoto_PeloPainel(string origem)
    {
        var (p, _, ag) = await CenarioAsync();
        var foto = await p.InserirFotoAsync(ag);
        var admin = await _fx.AdminLogadoAsync();

        var r = await admin.PostSimplesAsync("/Admin/AprovarFoto",
            new() { ["id"] = foto.Id.ToString(), ["origem"] = origem });

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains(origem == "TodasFotos" ? "TodasFotos" : "Painel", r.Destino());
        Assert.Contains("Foto aprovada com sucesso.", await (await admin.GetAsync(r.Destino())).HtmlAsync());
        var pendentes = await _fx.Api.ClientAdmin().GetStringAsync("api/Admin/fotos-pendentes");
        Assert.DoesNotContain(foto.Id.ToString(), pendentes);
    }

    [Theory(DisplayName = "WI-34 Rejeitar foto pelo painel apaga a foto e mostra a confirmação")]
    [InlineData("Painel")]
    [InlineData("TodasFotos")]
    public async Task RejeitarFoto_PeloPainel(string origem)
    {
        var (p, _, ag) = await CenarioAsync();
        var foto = await p.InserirFotoAsync(ag);
        var admin = await _fx.AdminLogadoAsync();

        var r = await admin.PostSimplesAsync("/Admin/RejeitarFoto",
            new() { ["id"] = foto.Id.ToString(), ["origem"] = origem });

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains(origem, r.Destino());
        Assert.Contains("Foto rejeitada e removida.", await (await admin.GetAsync(r.Destino())).HtmlAsync());
        var pendentes = await _fx.Api.ClientAdmin().GetStringAsync("api/Admin/fotos-pendentes");
        Assert.DoesNotContain(foto.Id.ToString(), pendentes);
    }

    [Theory(DisplayName = "WI-35 Aprovar plano pelo painel ativa o plano e volta para a tela de origem")]
    [InlineData("Painel", "Painel")]
    [InlineData("", "Prestadores")]
    public async Task AprovarPlano_PeloPainel(string origem, string destinoEsperado)
    {
        var (p, _, _) = await CenarioAsync(planoPendente: true);
        var admin = await _fx.AdminLogadoAsync();

        var r = await admin.PostSimplesAsync("/Admin/AprovarPlano", new() { ["id"] = p.Id.ToString(), ["origem"] = origem });

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains(destinoEsperado, r.Destino());
        Assert.Contains("Plano aprovado com sucesso.", await (await admin.GetAsync(r.Destino())).HtmlAsync());
        var perfil = await p.Client.GetStringAsync($"api/Users/meu-perfil/{p.Id}");
        Assert.Contains("\"planoAtivo\":true", perfil);
    }

    [Theory(DisplayName = "WI-36 Rejeitar plano pelo painel limpa a solicitação")]
    [InlineData("Painel", "Painel")]
    [InlineData("", "Prestadores")]
    public async Task RejeitarPlano_PeloPainel(string origem, string destinoEsperado)
    {
        var (p, _, _) = await CenarioAsync(planoPendente: true);
        var admin = await _fx.AdminLogadoAsync();

        var r = await admin.PostSimplesAsync("/Admin/RejeitarPlano", new() { ["id"] = p.Id.ToString(), ["origem"] = origem });

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains(destinoEsperado, r.Destino());
        Assert.Contains("Solicitação de plano rejeitada.", await (await admin.GetAsync(r.Destino())).HtmlAsync());
    }

    [Fact(DisplayName = "WI-37 Aprovar plano sem solicitação pendente mostra o motivo devolvido pela API")]
    public async Task AprovarPlano_SemPendencia_MostraMotivo()
    {
        var (p, _, _) = await CenarioAsync();
        var admin = await _fx.AdminLogadoAsync();

        var r = await admin.PostSimplesAsync("/Admin/AprovarPlano", new() { ["id"] = p.Id.ToString() });

        Assert.Contains("Não foi possível aprovar o plano", await (await admin.GetAsync(r.Destino())).HtmlAsync());
    }

    [Fact(DisplayName = "WI-38 Encerrar conta: tipo inválido, motivo vazio e motivo curto são barrados no portal")]
    public async Task EncerrarConta_ValidacoesDoPortal()
    {
        var admin = await _fx.AdminLogadoAsync();
        var id = Guid.NewGuid().ToString();

        var tipoInvalido = await admin.PostSimplesAsync("/Admin/EncerrarConta",
            new() { ["tipo"] = "outro", ["id"] = id, ["motivo"] = "Motivo válido" });
        Assert.Contains("Painel", tipoInvalido.Destino());
        Assert.Contains("Tipo de conta inválido.", await (await admin.GetAsync(tipoInvalido.Destino())).HtmlAsync());

        var semMotivo = await admin.PostSimplesAsync("/Admin/EncerrarConta",
            new() { ["tipo"] = "cliente", ["id"] = id, ["motivo"] = "  " });
        Assert.Contains("Clientes", semMotivo.Destino());
        Assert.Contains("Informe o motivo", await (await admin.GetAsync(semMotivo.Destino())).HtmlAsync());

        var motivoCurto = await admin.PostSimplesAsync("/Admin/EncerrarConta",
            new() { ["tipo"] = "prestador", ["id"] = id, ["motivo"] = "abc" });
        Assert.Contains("Prestadores", motivoCurto.Destino());
        Assert.Contains("pelo menos 5 caracteres", await (await admin.GetAsync(motivoCurto.Destino())).HtmlAsync());
    }

    [Theory(DisplayName = "WI-39 Encerrar conta pelo portal volta para a lista da conta e exibe o resultado")]
    [InlineData("cliente", "Clientes")]
    [InlineData("prestador", "Prestadores")]
    public async Task EncerrarConta_VoltaParaALista(string tipo, string lista)
    {
        var admin = await _fx.AdminLogadoAsync();

        var r = await admin.PostSimplesAsync("/Admin/EncerrarConta",
            new() { ["tipo"] = tipo, ["id"] = Guid.NewGuid().ToString(), ["motivo"] = "Motivo de teste do portal" });

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains(lista, r.Destino());
        var html = await (await admin.GetAsync(r.Destino())).HtmlAsync();
        Assert.Contains("alert alert-success", html);
    }

    [Fact(DisplayName = "WI-40 Sair do painel encerra a sessão do Admin")]
    public async Task Logout_EncerraSessaoDoAdmin()
    {
        var admin = await _fx.AdminLogadoAsync();

        var saida = await admin.GetAsync("/Admin/Logout");
        var depois = await admin.GetAsync("/Admin/Painel");

        Assert.Equal(HttpStatusCode.Redirect, saida.StatusCode);
        Assert.Contains("Login", saida.Destino());
        Assert.Contains("/Account/Login", depois.Destino());
    }
}
