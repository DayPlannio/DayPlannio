using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Tests;

[Collection(ApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class FluxoDeNegocioTests
{
    private readonly ApiFactory _f;
    public FluxoDeNegocioTests(ApiFactory f) => _f = f;

    [Fact(DisplayName = "IT-13 Tipo de serviço: criar → listar → editar → excluir (persistindo no Mongo)")]
    public async Task TipoServico_Crud_Completo()
    {
        var p = await _f.CriarPrestadorAsync();
        var id = await p.CriarServicoAsync("Limpeza", 60);

        var lista = await p.Client.GetFromJsonAsync<JsonElement>($"api/TiposServico/{p.Id}");
        Assert.Equal("Limpeza", lista.EnumerateArray().Single().GetProperty("tipo").GetString());

        var edit = await p.Client.PutAsJsonAsync($"api/TiposServico/edit/{id}",
            new { usuarioId = p.Id, tipo = "Limpeza Pesada", tempoEstimado = 90 });
        await edit.AssertStatusAsync(HttpStatusCode.OK);

        lista = await p.Client.GetFromJsonAsync<JsonElement>($"api/TiposServico/{p.Id}");
        var item = lista.EnumerateArray().Single();
        Assert.Equal("Limpeza Pesada", item.GetProperty("tipo").GetString());
        Assert.Equal(90, item.GetProperty("tempoEstimado").GetInt32());

        (await p.Client.DeleteAsync($"api/TiposServico/delete/{id}")).EnsureSuccessStatusCode();

        lista = await p.Client.GetFromJsonAsync<JsonElement>($"api/TiposServico/{p.Id}");
        Assert.Equal(0, lista.GetArrayLength());
    }

    [Theory(DisplayName = "IT-14 Tipo de serviço com tempo estimado inválido é recusado")]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1441)]
    public async Task TipoServico_TempoInvalido_Retorna400(int minutos)
    {
        var p = await _f.CriarPrestadorAsync();

        var r = await p.Client.PostAsJsonAsync("api/TiposServico/create",
            new { usuarioId = p.Id, tipo = "Qualquer", tempoEstimado = minutos });

        await r.AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-15 Editar ou excluir tipo de serviço inexistente retorna 404")]
    public async Task TipoServico_Inexistente_Retorna404()
    {
        var p = await _f.CriarPrestadorAsync();

        var edit = await p.Client.PutAsJsonAsync($"api/TiposServico/edit/{Guid.NewGuid()}",
            new { usuarioId = p.Id, tipo = "X", tempoEstimado = 10 });
        var del = await p.Client.DeleteAsync($"api/TiposServico/delete/{Guid.NewGuid()}");

        await edit.AssertStatusAsync(HttpStatusCode.NotFound);
        await del.AssertStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-16 Cliente: cadastrar e listar; raio de geofence é normalizado pelo Service")]
    public async Task Cliente_CriaELista()
    {
        var p = await _f.CriarPrestadorAsync();
        var id = await p.CriarClienteAsync("Maria da Silva");

        var lista = await p.Client.GetFromJsonAsync<JsonElement>($"api/Cliente/{p.Id}");
        var c = lista.EnumerateArray().Single();

        Assert.Equal(id, c.GetProperty("id").GetGuid());
        Assert.Equal("Maria da Silva", c.GetProperty("nome").GetString());
        Assert.True(c.GetProperty("ativo").GetBoolean());
        Assert.True(c.GetProperty("raioGeofenceMetros").GetInt32() > 0);
    }

    [Fact(DisplayName = "IT-17 Cliente sem nome ou com telefone inválido é recusado")]
    public async Task Cliente_Invalido_Retorna400()
    {
        var p = await _f.CriarPrestadorAsync();

        var semNome = await p.Client.PostAsJsonAsync("api/Cliente/create",
            new { usuarioId = p.Id, nome = "", telefone = "14999990000", endereco = "Rua A" });
        var telefoneRuim = await p.Client.PostAsJsonAsync("api/Cliente/create",
            new { usuarioId = p.Id, nome = "João", telefone = "abc", endereco = "Rua A" });

        await semNome.AssertStatusAsync(HttpStatusCode.BadRequest);
        await telefoneRuim.AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-18 Agendamento: não permite dois no mesmo horário, mas permite reagendar após cancelar")]
    public async Task Agendamento_HorarioDuplicado_ERebook()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();
        var servico = await p.CriarServicoAsync();
        var horario = ApiHelper.Futuro(dias: 3, hora: 10);

        var primeiro = await p.CriarAgendamentoAsync(cliente, servico, horario);

        var duplicado = await p.CriarAgendamentoRawAsync(cliente, servico, horario);
        await duplicado.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("Já existe um agendamento", await duplicado.MensagemAsync());

        var cancelar = await p.Client.PutAsJsonAsync($"api/Agendamentos/cancelar/{primeiro}", new { motivo = "Cliente desistiu" });
        await cancelar.AssertStatusAsync(HttpStatusCode.OK);

        var reagendado = await p.CriarAgendamentoRawAsync(cliente, servico, horario);
        await reagendado.AssertStatusAsync(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "IT-19 Cancelar exige motivo e não pode ser repetido")]
    public async Task Agendamento_Cancelar_Regras()
    {
        var p = await _f.CriarPrestadorAsync();
        var ag = await p.CriarAgendamentoAsync(await p.CriarClienteAsync(), await p.CriarServicoAsync(), ApiHelper.Futuro(4));

        var semMotivo = await p.Client.PutAsJsonAsync($"api/Agendamentos/cancelar/{ag}", new { motivo = "  " });
        await semMotivo.AssertStatusAsync(HttpStatusCode.BadRequest);

        var ok = await p.Client.PutAsJsonAsync($"api/Agendamentos/cancelar/{ag}", new { motivo = "Imprevisto" });
        await ok.AssertStatusAsync(HttpStatusCode.OK);

        var repetido = await p.Client.PutAsJsonAsync($"api/Agendamentos/cancelar/{ag}", new { motivo = "Imprevisto" });
        await repetido.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("já foi cancelado", await repetido.MensagemAsync());
    }

    [Fact(DisplayName = "IT-20 Concluir agendamento atualiza o status e não pode ser repetido")]
    public async Task Agendamento_Concluir_Regras()
    {
        var p = await _f.CriarPrestadorAsync();
        var ag = await p.CriarAgendamentoAsync(await p.CriarClienteAsync(), await p.CriarServicoAsync(), ApiHelper.Futuro(5));

        var ok = await p.Client.PutAsync($"api/Agendamentos/concluir/{ag}", null);
        await ok.AssertStatusAsync(HttpStatusCode.OK);

        var repetido = await p.Client.PutAsync($"api/Agendamentos/concluir/{ag}", null);
        await repetido.AssertStatusAsync(HttpStatusCode.BadRequest);

        var concluidos = await p.Client.GetFromJsonAsync<JsonElement>($"api/Agendamentos/{p.Id}/status/Concluido");
        Assert.Contains(concluidos.EnumerateArray(), a => a.GetProperty("id").GetGuid() == ag);
    }

    [Fact(DisplayName = "IT-21 Agendamento inexistente retorna 404")]
    public async Task Agendamento_Inexistente_Retorna404()
    {
        var p = await _f.CriarPrestadorAsync();

        var r = await p.Client.PutAsync($"api/Agendamentos/concluir/{Guid.NewGuid()}", null);

        await r.AssertStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-22 Financeiro: resumo mensal soma entradas, saídas e calcula o saldo")]
    public async Task Financeiro_Resumo_CalculaSaldo()
    {
        var p = await _f.CriarPrestadorAsync();

        async Task Lancar(string tipo, string descricao, decimal valor)
        {
            var r = await p.Client.PostAsJsonAsync("api/Financeiros/create", new
            {
                usuarioId = p.Id,
                tipo,
                descricao,
                valor,
                data = DateTime.UtcNow
            });
            await r.AssertStatusAsync(HttpStatusCode.OK);
        }

        await Lancar("Entrada", "Serviço de limpeza", 500m);
        await Lancar("Entrada", "Serviço de pintura", 250m);
        await Lancar("Saida", "Material", 200m);

        var resumo = await p.Client.GetFromJsonAsync<JsonElement>($"api/Financeiros/resumo/{p.Id}?periodo=mensal");

        Assert.Equal(750m, resumo.GetProperty("totalEntradas").GetDecimal());
        Assert.Equal(200m, resumo.GetProperty("totalSaidas").GetDecimal());
        Assert.Equal(550m, resumo.GetProperty("saldo").GetDecimal());
    }

    [Fact(DisplayName = "IT-23 Financeiro: valor zero ou descrição vazia é recusado")]
    public async Task Financeiro_Invalido_Retorna400()
    {
        var p = await _f.CriarPrestadorAsync();

        var valorZero = await p.Client.PostAsJsonAsync("api/Financeiros/create",
            new { usuarioId = p.Id, tipo = "Entrada", descricao = "X", valor = 0, data = DateTime.UtcNow });
        var semDescricao = await p.Client.PostAsJsonAsync("api/Financeiros/create",
            new { usuarioId = p.Id, tipo = "Entrada", descricao = "", valor = 10, data = DateTime.UtcNow });

        await valorZero.AssertStatusAsync(HttpStatusCode.BadRequest);
        await semDescricao.AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-24 Ações do prestador geram logs de auditoria consultáveis pelo Admin")]
    public async Task Logs_RegistramAsAcoes()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();
        var servico = await p.CriarServicoAsync();
        var ag = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(6));
        await p.Client.PutAsync($"api/Agendamentos/concluir/{ag}", null);

        var resp = await _f.ClientAdmin().GetFromJsonAsync<JsonElement>("api/Admin/logs?pagina=1&tamanhoPagina=1000");
        var tipos = resp.GetProperty("logs").EnumerateArray()
            .Where(l => l.GetProperty("usuarioId").GetString() == p.Id.ToString())
            .Select(l => l.GetProperty("tipo").GetString())
            .ToHashSet();

        Assert.Contains("prestador_criado", tipos);
        Assert.Contains("cliente_criado", tipos);
        Assert.Contains("servico_criado", tipos);
        Assert.Contains("agendamento_criado", tipos);
        Assert.Contains("agendamento_concluido", tipos);
    }

    [Fact(DisplayName = "IT-25 Fluxo completo do prestador: serviço → cliente → agenda → conclusão → métricas do cliente")]
    public async Task FluxoCompleto_ChegaNasMetricasDoCliente()
    {
        var p = await _f.CriarPrestadorAsync();
        var servico = await p.CriarServicoAsync("Corte de cabelo", 45);
        var cliente = await p.CriarClienteAsync("Cliente Métricas");
        var ag = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(7), valor: 80m);
        (await p.Client.PutAsync($"api/Agendamentos/concluir/{ag}", null)).EnsureSuccessStatusCode();

        var m = await p.Client.GetFromJsonAsync<JsonElement>(
            $"api/Agendamentos/metricas-cliente/{cliente}?usuarioId={p.Id}&periodo=mensal");

        Assert.Equal(1, m.GetProperty("resumoGeral").GetProperty("totalServicos").GetInt32());
    }
}
