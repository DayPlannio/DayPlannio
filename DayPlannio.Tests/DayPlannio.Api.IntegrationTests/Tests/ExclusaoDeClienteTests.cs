using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Tests;

[Collection(ApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class ExclusaoDeClienteTests
{
    private readonly ApiFactory _f;
    public ExclusaoDeClienteTests(ApiFactory f) => _f = f;

    private static async Task<JsonElement> PorStatusAsync(Prestador p, string status) =>
        await p.Client.GetFromJsonAsync<JsonElement>($"api/Agendamentos/{p.Id}/status/{status}");

    [Fact(DisplayName = "IT-52 Raio de geofence padrão é 100 m; valores fora da lista (20/50/100) são normalizados")]
    public async Task Cliente_RaioPadraoE_Normalizacao()
    {
        var p = await _f.CriarPrestadorAsync();

        async Task<int> CriarComRaio(object? raio)
        {
            var corpo = new Dictionary<string, object?>
            {
                ["usuarioId"] = p.Id, ["nome"] = $"Raio {Guid.NewGuid():N}"[..14],
                ["telefone"] = "14999990000", ["endereco"] = "Rua A, 1"
            };
            if (raio is not null) corpo["raioGeofenceMetros"] = raio;
            var r = await p.Client.PostAsJsonAsync("api/Cliente/create", corpo);
            await r.AssertStatusAsync(HttpStatusCode.OK);
            var id = (await r.JsonAsync()).GetProperty("id").GetGuid();
            var lista = await p.Client.GetFromJsonAsync<JsonElement>($"api/Cliente/{p.Id}");
            return lista.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == id)
                .GetProperty("raioGeofenceMetros").GetInt32();
        }

        Assert.Equal(100, await CriarComRaio(null));
        Assert.Equal(20, await CriarComRaio(20));
        Assert.Equal(50, await CriarComRaio(50));
        Assert.Equal(100, await CriarComRaio(77));
    }

    [Fact(DisplayName = "IT-53 Excluir cliente: remove futuros, mantém o histórico sem dados pessoais e remove o cliente da lista")]
    public async Task ExcluirCliente_RemoveFuturos_MantemHistoricoAnonimizado()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync("Cliente a Excluir");
        var servico = await p.CriarServicoAsync();

        var concluidoResp = await p.Client.PostAsJsonAsync("api/Agendamentos/create", new
        {
            usuarioId = p.Id, clienteId = cliente, tipoServicoId = servico,
            dataHora = ApiHelper.Futuro(2, 8), valorCobrado = 100m, custoMaterial = 10m,
            enderecoAtendimento = "Rua Privada, 99", observacoes = "Portão azul, cachorro bravo",
            latitude = -22.29, longitude = -48.55
        });
        await concluidoResp.AssertStatusAsync(HttpStatusCode.OK);
        var concluido = (await concluidoResp.JsonAsync()).GetProperty("id").GetGuid();
        (await p.Client.PutAsync($"api/Agendamentos/concluir/{concluido}", null)).EnsureSuccessStatusCode();

        var futuro = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(9, 15));

        var del = await p.Client.DeleteAsync($"api/Cliente/delete/{cliente}");
        await del.AssertStatusAsync(HttpStatusCode.OK);
        var json = await del.JsonAsync();
        Assert.Equal(1, json.GetProperty("agendamentosFuturosRemovidos").GetInt32());
        Assert.Contains("histórico foi mantido", json.GetProperty("message").GetString());

        var clientes = await p.Client.GetFromJsonAsync<JsonElement>($"api/Cliente/{p.Id}");
        Assert.DoesNotContain(clientes.EnumerateArray(), c => c.GetProperty("id").GetGuid() == cliente);

        var agendados = await PorStatusAsync(p, "Agendado");
        Assert.DoesNotContain(agendados.EnumerateArray(), a => a.GetProperty("id").GetGuid() == futuro);

        var concluidos = await PorStatusAsync(p, "Concluido");
        var h = concluidos.EnumerateArray().Single(a => a.GetProperty("id").GetGuid() == concluido);
        Assert.Equal(JsonValueKind.Null, h.GetProperty("enderecoAtendimento").ValueKind);
        Assert.Equal(JsonValueKind.Null, h.GetProperty("observacoes").ValueKind);
        Assert.Equal(JsonValueKind.Null, h.GetProperty("latitude").ValueKind);
        Assert.Equal(JsonValueKind.Null, h.GetProperty("longitude").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, h.GetProperty("clienteEncerradoEm").ValueKind);
        Assert.Equal(100m, h.GetProperty("valorCobrado").GetDecimal());
    }

    [Fact(DisplayName = "IT-54 Excluir cliente sem agendamentos futuros informa 0 removidos")]
    public async Task ExcluirCliente_SemFuturos_ZeroRemovidos()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();

        var del = await p.Client.DeleteAsync($"api/Cliente/delete/{cliente}");

        await del.AssertStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, (await del.JsonAsync()).GetProperty("agendamentosFuturosRemovidos").GetInt32());
    }

    [Fact(DisplayName = "IT-55 Excluir cliente: inexistente = 404; de outro prestador = 403 e o cliente permanece")]
    public async Task ExcluirCliente_Inexistente_EDeOutro()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var clienteDoA = await a.CriarClienteAsync();

        await (await a.Client.DeleteAsync($"api/Cliente/delete/{Guid.NewGuid()}")).AssertStatusAsync(HttpStatusCode.NotFound);

        await (await b.Client.DeleteAsync($"api/Cliente/delete/{clienteDoA}")).AssertStatusAsync(HttpStatusCode.Forbidden);

        var lista = await a.Client.GetFromJsonAsync<JsonElement>($"api/Cliente/{a.Id}");
        Assert.Equal(1, lista.GetArrayLength());
    }

    [Fact(DisplayName = "IT-56 Cliente excluído perde o acesso ao portal (login retorna 401)")]
    public async Task ExcluirCliente_PerdeAcessoAoPortal()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();
        var acesso = await p.GerarAcessoClienteAsync(cliente);
        await (await _f.NovoClient().PostAsJsonAsync("api/Cliente/login",
            new { email = acesso.Email, senha = acesso.SenhaProvisoria })).AssertStatusAsync(HttpStatusCode.OK);

        (await p.Client.DeleteAsync($"api/Cliente/delete/{cliente}")).EnsureSuccessStatusCode();

        await (await _f.NovoClient().PostAsJsonAsync("api/Cliente/login",
            new { email = acesso.Email, senha = acesso.SenhaProvisoria })).AssertStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "IT-57 Exclusão de cliente é registrada na auditoria com a contagem de futuros removidos")]
    public async Task ExcluirCliente_GeraLog()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync("Cliente Log");
        await p.CriarAgendamentoAsync(cliente, await p.CriarServicoAsync(), ApiHelper.Futuro(12, 10));

        (await p.Client.DeleteAsync($"api/Cliente/delete/{cliente}")).EnsureSuccessStatusCode();

        var resp = await _f.ClientAdmin().GetFromJsonAsync<JsonElement>("api/Admin/logs?pagina=1&tamanhoPagina=1000");
        var log = resp.GetProperty("logs").EnumerateArray().Single(l =>
            l.GetProperty("usuarioId").GetString() == p.Id.ToString() &&
            l.GetProperty("tipo").GetString() == "cliente_deletado");
        Assert.Contains("1 agendamento(s) futuro(s) removido(s)", log.GetProperty("descricao").GetString());
    }
}
