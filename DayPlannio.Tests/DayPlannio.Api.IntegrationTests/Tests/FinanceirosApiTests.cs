using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Tests;

[Collection(ApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class FinanceirosApiTests
{
    private readonly ApiFactory _f;
    public FinanceirosApiTests(ApiFactory f) => _f = f;

    private static async Task<Guid> LancarAsync(Prestador p, string tipo, string descricao, decimal valor, DateTime? data = null)
    {
        var r = await p.Client.PostAsJsonAsync("api/Financeiros/create", new
        {
            usuarioId = p.Id,
            tipo,
            descricao,
            valor,
            data = data ?? DateTime.UtcNow
        });
        await r.AssertStatusAsync(HttpStatusCode.OK);
        return (await r.JsonAsync()).GetProperty("id").GetGuid();
    }

    private static object Corpo(Prestador p, string tipo, string descricao, decimal valor) => new
    {
        usuarioId = p.Id,
        tipo,
        descricao,
        valor,
        data = DateTime.UtcNow
    };

    [Fact(DisplayName = "IT-127 Financeiro: listar do mais recente para o mais antigo")]
    public async Task Listar_OrdenaPorDataDecrescente()
    {
        var p = await _f.CriarPrestadorAsync();
        var antigo = await LancarAsync(p, "Entrada", "Antigo", 10m, DateTime.UtcNow.AddDays(-3));
        var recente = await LancarAsync(p, "Saida", "Recente", 20m, DateTime.UtcNow);

        var lista = await p.Client.GetFromJsonAsync<JsonElement>($"api/Financeiros/{p.Id}");

        Assert.Equal(2, lista.GetArrayLength());
        Assert.Equal(recente, lista[0].GetProperty("id").GetGuid());
        Assert.Equal(antigo, lista[1].GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "IT-128 Financeiro: editar e excluir registro")]
    public async Task EditarEExcluir_Registro()
    {
        var p = await _f.CriarPrestadorAsync();
        var id = await LancarAsync(p, "Entrada", "Original", 100m);

        var edit = await p.Client.PutAsJsonAsync($"api/Financeiros/edit/{id}", Corpo(p, "Saida", "Corrigido", 55m));

        await edit.AssertStatusAsync(HttpStatusCode.OK);
        var item = (await p.Client.GetFromJsonAsync<JsonElement>($"api/Financeiros/{p.Id}")).EnumerateArray().Single();
        Assert.Equal("Corrigido", item.GetProperty("descricao").GetString());
        Assert.Equal("Saida", item.GetProperty("tipo").GetString());
        Assert.Equal(55m, item.GetProperty("valor").GetDecimal());

        await (await p.Client.DeleteAsync($"api/Financeiros/delete/{id}")).AssertStatusAsync(HttpStatusCode.OK);
        var depois = await p.Client.GetFromJsonAsync<JsonElement>($"api/Financeiros/{p.Id}");
        Assert.Equal(0, depois.GetArrayLength());
    }

    [Fact(DisplayName = "IT-129 Financeiro: registro inexistente 404 e registro de outro prestador 403")]
    public async Task Registro_InexistenteEDeOutro()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var id = await LancarAsync(a, "Entrada", "Do A", 100m);

        await (await a.Client.PutAsJsonAsync($"api/Financeiros/edit/{Guid.NewGuid()}", Corpo(a, "Entrada", "X", 1m)))
            .AssertStatusAsync(HttpStatusCode.NotFound);
        await (await a.Client.DeleteAsync($"api/Financeiros/delete/{Guid.NewGuid()}")).AssertStatusAsync(HttpStatusCode.NotFound);
        await (await b.Client.PutAsJsonAsync($"api/Financeiros/edit/{id}", Corpo(b, "Entrada", "Invasor", 1m)))
            .AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await b.Client.DeleteAsync($"api/Financeiros/delete/{id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await b.Client.GetAsync($"api/Financeiros/{a.Id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await b.Client.GetAsync($"api/Financeiros/resumo-geral/{a.Id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await b.Client.GetAsync($"api/Financeiros/{a.Id}/periodo")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await b.Client.GetAsync($"api/Financeiros/relatorio/{a.Id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
    }

    [Theory(DisplayName = "IT-130 Resumo geral soma agendamentos concluídos e lançamentos avulsos")]
    [InlineData("diario")]
    [InlineData("semanal")]
    [InlineData("mensal")]
    [InlineData("qualquer")]
    public async Task ResumoGeral_SomaTudo(string periodo)
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();
        var servico = await p.CriarServicoAsync();
        var ag = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2), valor: 150m);
        (await p.Client.PutAsync($"api/Agendamentos/concluir/{ag}", null)).EnsureSuccessStatusCode();
        await LancarAsync(p, "Entrada", "Gorjeta", 100m);
        await LancarAsync(p, "Saida", "Combustível", 30m);

        var r = await p.Client.GetFromJsonAsync<JsonElement>($"api/Financeiros/resumo-geral/{p.Id}?periodo={periodo}");

        Assert.Equal(150m, r.GetProperty("receitaAgendamentos").GetDecimal());
        Assert.Equal(20m, r.GetProperty("custoAgendamentos").GetDecimal());
        Assert.Equal(100m, r.GetProperty("entradasAvulsas").GetDecimal());
        Assert.Equal(30m, r.GetProperty("saidasAvulsas").GetDecimal());
        Assert.Equal(250m, r.GetProperty("lucroBruto").GetDecimal());
        Assert.Equal(200m, r.GetProperty("lucroLiquido").GetDecimal());
        Assert.Equal(1, r.GetProperty("totalServicos").GetInt32());
    }

    [Theory(DisplayName = "IT-131 Lançamentos por período trazem só os do intervalo")]
    [InlineData("diario")]
    [InlineData("semanal")]
    [InlineData("mensal")]
    public async Task PorPeriodo_FiltraIntervalo(string periodo)
    {
        var p = await _f.CriarPrestadorAsync();
        var atual = await LancarAsync(p, "Entrada", "Atual", 10m, DateTime.UtcNow);
        await LancarAsync(p, "Entrada", "Muito antigo", 10m, DateTime.UtcNow.AddDays(-90));

        var lista = await p.Client.GetFromJsonAsync<JsonElement>($"api/Financeiros/{p.Id}/periodo?periodo={periodo}");

        Assert.Equal(atual, lista.EnumerateArray().Single().GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "IT-132 Lançamentos por período usam 'diário' como padrão")]
    public async Task PorPeriodo_PadraoEDiario()
    {
        var p = await _f.CriarPrestadorAsync();
        await LancarAsync(p, "Entrada", "Hoje", 10m, DateTime.UtcNow);

        var r = await p.Client.GetAsync($"api/Financeiros/{p.Id}/periodo");

        await r.AssertStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, (await r.JsonAsync()).GetArrayLength());
    }

    [Fact(DisplayName = "IT-133 Relatório financeiro é gerado em PDF")]
    public async Task Relatorio_GeraPdf()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync("Cliente do Relatório");
        var servico = await p.CriarServicoAsync("Pintura");
        var ag = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2), valor: 300m);
        (await p.Client.PutAsync($"api/Agendamentos/concluir/{ag}", null)).EnsureSuccessStatusCode();
        await LancarAsync(p, "Entrada", "Venda de material", 80m);
        await LancarAsync(p, "Saida", "Aluguel de ferramenta", 40m);

        var r = await p.Client.GetAsync($"api/Financeiros/relatorio/{p.Id}?periodo=mensal");

        await r.AssertStatusAsync(HttpStatusCode.OK);
        Assert.Equal("application/pdf", r.Content.Headers.ContentType?.MediaType);
        var bytes = await r.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 500);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Contains("relatorio-mensal-", r.Content.Headers.ContentDisposition?.FileName);
    }

    [Fact(DisplayName = "IT-134 Relatório continua sendo gerado depois que o cliente é excluído")]
    public async Task Relatorio_ComClienteExcluido_GeraPdf()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();
        var servico = await p.CriarServicoAsync();
        var ag = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2), valor: 120m);
        (await p.Client.PutAsync($"api/Agendamentos/concluir/{ag}", null)).EnsureSuccessStatusCode();
        (await p.Client.DeleteAsync($"api/Cliente/delete/{cliente}")).EnsureSuccessStatusCode();

        var r = await p.Client.GetAsync($"api/Financeiros/relatorio/{p.Id}?periodo=semanal");

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var bytes = await r.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
