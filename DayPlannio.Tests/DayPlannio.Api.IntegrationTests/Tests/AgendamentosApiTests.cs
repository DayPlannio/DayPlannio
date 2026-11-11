using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Tests;

[Collection(ApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class AgendamentosApiTests
{
    private readonly ApiFactory _f;
    public AgendamentosApiTests(ApiFactory f) => _f = f;

    private static object CorpoEdicao(Prestador p, Guid cliente, Guid servico, DateTime quando, decimal valor) => new
    {
        usuarioId = p.Id,
        clienteId = cliente,
        tipoServicoId = servico,
        dataHora = quando,
        valorCobrado = valor,
        custoMaterial = 10m,
        observacoes = "Levar escada",
        enderecoAtendimento = "Rua Nova, 50",
        latitude = -22.29,
        longitude = -48.55
    };

    private static async Task<(Guid cliente, Guid servico)> BaseAsync(Prestador p) =>
        (await p.CriarClienteAsync(), await p.CriarServicoAsync());

    [Fact(DisplayName = "IT-92 Listar agendamentos do prestador: dono e Admin leem, outro prestador recebe 403")]
    public async Task Listar_AcessoPorDono()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var (cliente, servico) = await BaseAsync(a);
        await a.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));

        var doDono = await a.Client.GetFromJsonAsync<JsonElement>($"api/Agendamentos/{a.Id}");
        var doAdmin = await _f.ClientAdmin().GetFromJsonAsync<JsonElement>($"api/Agendamentos/{a.Id}");

        Assert.Equal(1, doDono.GetArrayLength());
        Assert.Equal(1, doAdmin.GetArrayLength());
        await (await b.Client.GetAsync($"api/Agendamentos/{a.Id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await b.Client.GetAsync($"api/Agendamentos/{a.Id}/status/Agendado")).AssertStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "IT-93 Editar agendamento atualiza horário, valor, observações e endereço")]
    public async Task Editar_AtualizaCampos()
    {
        var p = await _f.CriarPrestadorAsync();
        var (cliente, servico) = await BaseAsync(p);
        var id = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));
        var novoHorario = ApiHelper.Futuro(3, 14);

        var r = await p.Client.PutAsJsonAsync($"api/Agendamentos/edit/{id}", CorpoEdicao(p, cliente, servico, novoHorario, 220m));

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var lista = await p.Client.GetFromJsonAsync<JsonElement>($"api/Agendamentos/{p.Id}");
        var ag = lista.EnumerateArray().Single();
        Assert.Equal(220m, ag.GetProperty("valorCobrado").GetDecimal());
        Assert.Equal("Levar escada", ag.GetProperty("observacoes").GetString());
        Assert.Equal("Rua Nova, 50", ag.GetProperty("enderecoAtendimento").GetString());
        Assert.Equal(novoHorario, ag.GetProperty("dataHora").GetDateTime().ToUniversalTime());
    }

    [Fact(DisplayName = "IT-94 Editar agendamento: inexistente 404, outro prestador 403, horário ocupado 400")]
    public async Task Editar_Validacoes()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var (cliente, servico) = await BaseAsync(a);
        var primeiro = await a.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));
        var segundoHorario = ApiHelper.Futuro(2, 15);
        await a.CriarAgendamentoAsync(cliente, servico, segundoHorario);

        var inexistente = await a.Client.PutAsJsonAsync($"api/Agendamentos/edit/{Guid.NewGuid()}",
            CorpoEdicao(a, cliente, servico, ApiHelper.Futuro(6), 10m));
        var deOutro = await b.Client.PutAsJsonAsync($"api/Agendamentos/edit/{primeiro}",
            CorpoEdicao(b, cliente, servico, ApiHelper.Futuro(6), 10m));
        var conflito = await a.Client.PutAsJsonAsync($"api/Agendamentos/edit/{primeiro}",
            CorpoEdicao(a, cliente, servico, segundoHorario, 10m));

        await inexistente.AssertStatusAsync(HttpStatusCode.NotFound);
        await deOutro.AssertStatusAsync(HttpStatusCode.Forbidden);
        await conflito.AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-95 Excluir agendamento: remove, repetir dá 404 e outro prestador recebe 403")]
    public async Task Excluir_Regras()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var (cliente, servico) = await BaseAsync(a);
        var id = await a.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));

        await (await b.Client.DeleteAsync($"api/Agendamentos/delete/{id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await a.Client.DeleteAsync($"api/Agendamentos/delete/{id}")).AssertStatusAsync(HttpStatusCode.OK);
        await (await a.Client.DeleteAsync($"api/Agendamentos/delete/{id}")).AssertStatusAsync(HttpStatusCode.NotFound);

        var lista = await a.Client.GetFromJsonAsync<JsonElement>($"api/Agendamentos/{a.Id}");
        Assert.Equal(0, lista.GetArrayLength());
    }

    [Theory(DisplayName = "IT-96 Lucro do período soma valor cobrado menos custo dos concluídos")]
    [InlineData("diario")]
    [InlineData("semanal")]
    [InlineData("mensal")]
    public async Task Lucro_PorPeriodo(string periodo)
    {
        var p = await _f.CriarPrestadorAsync();
        var (cliente, servico) = await BaseAsync(p);
        var id = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2), valor: 150m);
        (await p.Client.PutAsync($"api/Agendamentos/concluir/{id}", null)).EnsureSuccessStatusCode();

        var r = await p.Client.GetFromJsonAsync<JsonElement>($"api/Agendamentos/lucro/{p.Id}?periodo={periodo}");

        Assert.Equal(periodo, r.GetProperty("periodo").GetString());
        Assert.Equal(1, r.GetProperty("totalServicos").GetInt32());
        Assert.Equal(150m, r.GetProperty("lucroBruto").GetDecimal());
        Assert.Equal(20m, r.GetProperty("custoTotal").GetDecimal());
        Assert.Equal(130m, r.GetProperty("lucroLiquido").GetDecimal());
    }

    [Fact(DisplayName = "IT-97 Lucro e agenda de outro prestador retornam 403")]
    public async Task LucroEAgenda_OutroPrestador_Retorna403()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();

        await (await b.Client.GetAsync($"api/Agendamentos/lucro/{a.Id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await b.Client.GetAsync($"api/Agendamentos/agenda/{a.Id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await b.Client.GetAsync($"api/Agendamentos/metricas/{a.Id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "IT-98 Agenda 'próximos dias' traz o agendamento futuro; períodos fixos respondem 200")]
    public async Task Agenda_Janelas()
    {
        var p = await _f.CriarPrestadorAsync();
        var (cliente, servico) = await BaseAsync(p);
        var id = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));

        var proximos = await p.Client.GetFromJsonAsync<JsonElement>($"api/Agendamentos/agenda/{p.Id}?periodo=proximos");

        Assert.Equal(id, proximos.EnumerateArray().Single().GetProperty("id").GetGuid());
        foreach (var periodo in new[] { "diario", "semanal", "mensal" })
        {
            var r = await p.Client.GetAsync($"api/Agendamentos/agenda/{p.Id}?periodo={periodo}");
            await r.AssertStatusAsync(HttpStatusCode.OK);
        }
    }

    [Fact(DisplayName = "IT-99 Histórico do cliente: o prestador vê o seu, outro prestador recebe lista vazia")]
    public async Task Historico_FiltraPorPrestador()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var (cliente, servico) = await BaseAsync(a);
        await a.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));
        await a.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(3));

        var doDono = await a.Client.GetFromJsonAsync<JsonElement>($"api/Agendamentos/historico/{cliente}");
        var doOutro = await b.Client.GetFromJsonAsync<JsonElement>($"api/Agendamentos/historico/{cliente}");
        var doAdmin = await _f.ClientAdmin().GetFromJsonAsync<JsonElement>($"api/Agendamentos/historico/{cliente}");

        Assert.Equal(2, doDono.GetArrayLength());
        Assert.Equal(0, doOutro.GetArrayLength());
        Assert.Equal(2, doAdmin.GetArrayLength());
    }

    [Fact(DisplayName = "IT-100 Histórico completo traz serviço, profissional e status; acesso indevido é barrado")]
    public async Task HistoricoCompleto_TrazNomes()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var cliente = await a.CriarClienteAsync();
        var servico = await a.CriarServicoAsync("Jardinagem");
        await a.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));

        var r = await a.Client.GetFromJsonAsync<JsonElement>($"api/Agendamentos/historico-completo/{cliente}?usuarioId={a.Id}");

        var item = r.EnumerateArray().Single();
        Assert.Equal("Jardinagem", item.GetProperty("servico").GetString());
        Assert.Equal(a.Nome, item.GetProperty("profissional").GetString());
        Assert.Equal("Agendado", item.GetProperty("status").GetString());
        await (await b.Client.GetAsync($"api/Agendamentos/historico-completo/{cliente}?usuarioId={a.Id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await _f.ClientAdmin().GetAsync($"api/Agendamentos/historico-completo/{cliente}?usuarioId={Guid.NewGuid()}")).AssertStatusAsync(HttpStatusCode.Gone);
    }

    [Fact(DisplayName = "IT-101 Métricas do prestador somam atendimentos, cancelamentos e motivos")]
    public async Task Metricas_ConsolidaConcluidosECancelados()
    {
        var p = await _f.CriarPrestadorAsync();
        var (cliente, servico) = await BaseAsync(p);
        var concluido = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2), valor: 150m);
        var cancelado = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(3));
        (await p.Client.PutAsync($"api/Agendamentos/concluir/{concluido}", null)).EnsureSuccessStatusCode();
        (await p.Client.PutAsJsonAsync($"api/Agendamentos/cancelar/{cancelado}",
            new { motivo = "Cliente com imprevisto de saúde" })).EnsureSuccessStatusCode();

        var m = await p.Client.GetFromJsonAsync<JsonElement>($"api/Agendamentos/metricas/{p.Id}?periodo=mensal");

        Assert.Equal(1, m.GetProperty("atendimentos").GetInt32());
        Assert.Equal(1, m.GetProperty("clientes").GetInt32());
        Assert.Equal(150m, m.GetProperty("faturamento").GetDecimal());
        Assert.Equal(1, m.GetProperty("cancelamentos").GetInt32());
        Assert.Equal(2, m.GetProperty("totalFinalizados").GetInt32());
        Assert.Equal(50.0, m.GetProperty("taxaCancelamento").GetDouble());
        Assert.Equal(1, m.GetProperty("cancelamentosComMotivo").GetInt32());
        Assert.True(m.GetProperty("motivosCancelamento").GetArrayLength() > 0);
        Assert.Equal(100.0, m.GetProperty("variacoes").GetProperty("atendimentos").GetDouble());
        Assert.Equal("Limpeza", m.GetProperty("porServico")[0].GetProperty("servico").GetString());
        Assert.True(m.GetProperty("evolucao").GetArrayLength() >= 28);
    }

    [Theory(DisplayName = "IT-102 Métricas respondem 200 em todos os períodos")]
    [InlineData("diario")]
    [InlineData("semanal")]
    [InlineData("mensal")]
    public async Task Metricas_TodosOsPeriodos(string periodo)
    {
        var p = await _f.CriarPrestadorAsync();

        var r = await p.Client.GetAsync($"api/Agendamentos/metricas/{p.Id}?periodo={periodo}");

        await r.AssertStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, (await r.JsonAsync()).GetProperty("atendimentos").GetInt32());
    }

    [Fact(DisplayName = "IT-103 Métricas exigem plano Profissional; métricas do cliente exigem Full")]
    public async Task Metricas_ExigemPlano()
    {
        var basico = await _f.CriarPrestadorComPlanoAsync("Basico");
        var profissional = await _f.CriarPrestadorComPlanoAsync("Profissional");
        var cliente = await profissional.CriarClienteAsync();

        var semAcesso = await basico.Client.GetAsync($"api/Agendamentos/metricas/{basico.Id}");
        var clienteSemFull = await profissional.Client.GetAsync(
            $"api/Agendamentos/metricas-cliente/{cliente}?usuarioId={profissional.Id}");

        await semAcesso.AssertStatusAsync(HttpStatusCode.Forbidden);
        await clienteSemFull.AssertStatusAsync(HttpStatusCode.Forbidden);
        Assert.Contains("Profissional", await clienteSemFull.MensagemAsync());
    }

    [Fact(DisplayName = "IT-104 Métricas do cliente de prestador removido retornam 410")]
    public async Task MetricasCliente_PrestadorRemovido_Retorna410()
    {
        var r = await _f.ClientAdmin().GetAsync(
            $"api/Agendamentos/metricas-cliente/{Guid.NewGuid()}?usuarioId={Guid.NewGuid()}");

        await r.AssertStatusAsync(HttpStatusCode.Gone);
    }

    [Fact(DisplayName = "IT-105 Métricas do cliente consolidam serviços concluídos")]
    public async Task MetricasCliente_ConsolidaServicos()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();
        var limpeza = await p.CriarServicoAsync("Limpeza");
        var pintura = await p.CriarServicoAsync("Pintura");
        foreach (var (servico, dia) in new[] { (limpeza, 2), (limpeza, 3), (pintura, 4) })
        {
            var id = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(dia), valor: 100m);
            (await p.Client.PutAsync($"api/Agendamentos/concluir/{id}", null)).EnsureSuccessStatusCode();
        }

        var m = await p.Client.GetFromJsonAsync<JsonElement>(
            $"api/Agendamentos/metricas-cliente/{cliente}?usuarioId={p.Id}&periodo=mensal");

        Assert.Equal(3, m.GetProperty("resumoGeral").GetProperty("totalServicos").GetInt32());
        Assert.Equal(300m, m.GetProperty("resumoGeral").GetProperty("valorTotal").GetDecimal());
        Assert.Equal(2, m.GetProperty("servicosPorCategoria").GetArrayLength());
        Assert.Equal("Limpeza", m.GetProperty("servicosPorCategoria")[0].GetProperty("categoria").GetString());
        Assert.Equal(3, m.GetProperty("historico").GetArrayLength());
    }

    [Fact(DisplayName = "IT-106 Localização inválida: dados vazios 400, coordenadas fora do mundo 400, outro prestador 403")]
    public async Task Localizacao_Validacoes()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();

        var vazio = await a.Client.PostAsJsonAsync("api/Agendamentos/localizacao",
            new { usuarioId = Guid.Empty, latitude = 0, longitude = 0 });
        var foraDoMundo = await a.Client.PostAsJsonAsync("api/Agendamentos/localizacao",
            new { usuarioId = a.Id, latitude = 91, longitude = 0 });
        var longitudeInvalida = await a.Client.PostAsJsonAsync("api/Agendamentos/localizacao",
            new { usuarioId = a.Id, latitude = 0, longitude = 181 });
        var deOutro = await b.Client.PostAsJsonAsync("api/Agendamentos/localizacao",
            new { usuarioId = a.Id, latitude = 0, longitude = 0 });

        await vazio.AssertStatusAsync(HttpStatusCode.BadRequest);
        await foraDoMundo.AssertStatusAsync(HttpStatusCode.BadRequest);
        await longitudeInvalida.AssertStatusAsync(HttpStatusCode.BadRequest);
        await deOutro.AssertStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "IT-107 Localização sem agendamentos na janela manda o app parar de rastrear")]
    public async Task Localizacao_SemAgendamentos_NaoRastreia()
    {
        var p = await _f.CriarPrestadorAsync();

        var r = await p.Client.PostAsJsonAsync("api/Agendamentos/localizacao",
            new { usuarioId = p.Id, latitude = -22.29, longitude = -48.55, precisao = 5 });

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var json = await r.JsonAsync();
        Assert.False(json.GetProperty("rastrear").GetBoolean());
        Assert.Equal(0, json.GetProperty("eventos").GetArrayLength());
    }
}
