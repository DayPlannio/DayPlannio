using System.Net;
using System.Net.Http.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using DayPlannio.Web.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Web.IntegrationTests.Tests;

[Collection(PortalCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class DashboardEPortfolioWebTests
{
    private readonly PortalFixture _fx;
    public DashboardEPortfolioWebTests(PortalFixture fx) => _fx = fx;

    private static async Task ConcluirAsync(Prestador p, Guid cliente, Guid servico, int dias, decimal valor)
    {
        var ag = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(dias), valor);
        (await p.Client.PutAsync($"api/Agendamentos/concluir/{ag}", null)).EnsureSuccessStatusCode();
    }

    [Theory(DisplayName = "WI-22 Indicadores do cliente mostram os serviços concluídos em cada período")]
    [InlineData("mensal")]
    [InlineData("semanal")]
    [InlineData("diario")]
    public async Task Metricas_MostramServicos(string periodo)
    {
        var cliente = await _fx.ClienteLogadoAsync();
        var pintura = await cliente.Prestador.CriarServicoAsync("Pintura");
        var limpeza = await cliente.Prestador.CriarServicoAsync("Limpeza Pesada");
        await ConcluirAsync(cliente.Prestador, cliente.Acesso.Id, pintura, 2, 300m);
        await ConcluirAsync(cliente.Prestador, cliente.Acesso.Id, limpeza, 3, 120m);

        var r = await cliente.Nav.GetAsync($"/Metricas?periodo={periodo}");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.HtmlAsync();
        Assert.Contains("Meus Indicadores", html);
        if (periodo == "mensal")
        {
            Assert.Contains("Pintura", html);
            Assert.Contains("Limpeza Pesada", html);
        }
    }

    [Fact(DisplayName = "WI-23 Histórico de atendimentos mostra serviço e profissional")]
    public async Task Historico_MostraAtendimentos()
    {
        var cliente = await _fx.ClienteLogadoAsync();
        var servico = await cliente.Prestador.CriarServicoAsync("Jardinagem");
        await ConcluirAsync(cliente.Prestador, cliente.Acesso.Id, servico, 2, 80m);

        var r = await cliente.Nav.GetAsync("/HistoricoCliente");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.HtmlAsync();
        Assert.Contains("Histórico de Atendimentos", html);
        Assert.Contains("Jardinagem", html);
        Assert.Contains(cliente.Prestador.Nome, html);
    }

    [Fact(DisplayName = "WI-24 Histórico sem atendimentos e sem login")]
    public async Task Historico_SemAtendimentosESemLogin()
    {
        var cliente = await _fx.ClienteLogadoAsync();

        var vazio = await cliente.Nav.GetAsync("/HistoricoCliente");
        var anonimo = await _fx.Web.NovoNavegador().GetAsync("/HistoricoCliente");

        Assert.Equal(HttpStatusCode.OK, vazio.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, anonimo.StatusCode);
        Assert.Contains("/Account/Login", anonimo.Destino());
    }

    [Fact(DisplayName = "WI-25 Profissional que saiu do DayPlannio: indicadores e histórico mostram o aviso")]
    public async Task PrestadorRemovido_MostraAviso()
    {
        var cliente = await _fx.ClienteLogadoAsync();
        (await _fx.Api.ClientAdmin().DeleteAsync($"api/Users/delete/{cliente.Prestador.Id}")).EnsureSuccessStatusCode();

        var metricas = await cliente.Nav.GetAsync("/Metricas");
        var historico = await cliente.Nav.GetAsync("/HistoricoCliente");

        Assert.Contains("não faz mais parte do DayPlannio", await metricas.HtmlAsync());
        Assert.Contains("não faz mais parte do DayPlannio", await historico.HtmlAsync());
    }

    [Fact(DisplayName = "WI-26 Indicadores do cliente exigem que o profissional tenha o plano Full")]
    public async Task Metricas_PlanoDoProfissionalInsuficiente()
    {
        var cliente = await _fx.ClienteLogadoAsync();
        var p = cliente.Prestador;
        (await p.Client.PostAsJsonAsync("api/Users/plano/solicitar",
            new { usuarioId = p.Id.ToString(), plano = "Basico" })).EnsureSuccessStatusCode();
        (await _fx.Api.ClientAdmin().PostAsync($"api/Admin/planos/{p.Id}/aprovar", null)).EnsureSuccessStatusCode();

        var r = await cliente.Nav.GetAsync("/Metricas");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("exige o plano Full", await r.HtmlAsync());
    }

    [Fact(DisplayName = "WI-27 Portfólio público mostra o profissional, a cidade e o contato quando são visíveis")]
    public async Task Portfolio_MostraProfissional()
    {
        var p = await _fx.Api.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();
        var servico = await p.CriarServicoAsync("Marcenaria");
        var ag = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));
        await p.InserirFotoAsync(ag, publica: true, aprovada: true, descricao: "Armário planejado");
        var cidade = $"Cidade{Guid.NewGuid():N}"[..14];
        (await p.Client.PutAsJsonAsync($"api/Users/edit/{p.Id}", new
        {
            nomeCompleto = p.Nome,
            email = p.Email,
            telefone = "14977776666",
            cidade,
            cidadeVisivel = true,
            telefoneVisivel = true
        })).EnsureSuccessStatusCode();

        var r = await _fx.Web.NovoNavegador().GetAsync("/portfolio");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.HtmlAsync();
        Assert.Contains("Qualidade que você pode ver", html);
        Assert.Contains(p.Nome, html);
        Assert.Contains("Marcenaria", html);
        Assert.Contains(cidade, html);
        Assert.Contains("wa.me/55", html);
    }

    [Fact(DisplayName = "WI-28 Portfólio público aceita o filtro por cidade")]
    public async Task Portfolio_FiltraPorCidade()
    {
        var p = await _fx.Api.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();
        var servico = await p.CriarServicoAsync();
        var ag = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));
        await p.InserirFotoAsync(ag, aprovada: true);
        var cidade = $"Cidade{Guid.NewGuid():N}"[..14];
        (await p.Client.PutAsJsonAsync($"api/Users/edit/{p.Id}", new
        {
            nomeCompleto = p.Nome, email = p.Email, telefone = "14977776666", cidade, cidadeVisivel = true, telefoneVisivel = false
        })).EnsureSuccessStatusCode();

        var r = await _fx.Web.NovoNavegador().GetAsync($"/portfolio?cidade={Uri.EscapeDataString(cidade)}");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains(p.Nome, await r.HtmlAsync());
    }

    [Fact(DisplayName = "WI-29 Página de erro do portal é entregue")]
    public async Task PaginaDeErro_EEntregue()
    {
        var r = await _fx.Web.NovoNavegador().GetAsync("/Home/Error");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("An error occurred", await r.HtmlAsync());
    }
}
