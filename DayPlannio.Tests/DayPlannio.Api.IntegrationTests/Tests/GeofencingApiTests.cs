using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using DayPlannio.Api.Models;
using MongoDB.Driver;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Tests;

[Collection(ApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class GeofencingApiTests
{
    private const double Lat = -22.2965;
    private const double Lon = -48.5584;
    private const double DeslocamentoLonge = 0.01;

    private readonly ApiFactory _f;
    public GeofencingApiTests(ApiFactory f) => _f = f;

    private static async Task<Guid> ClienteComLocalAsync(Prestador p, double? lat, double? lon, string? nome = null)
    {
        var r = await p.Client.PostAsJsonAsync("api/Cliente/create", new
        {
            usuarioId = p.Id,
            nome = nome ?? $"Cliente Geo {Guid.NewGuid():N}"[..20],
            telefone = "14999990000",
            endereco = "Rua do Geofence, 10",
            latitude = lat,
            longitude = lon,
            raioGeofenceMetros = 50
        });
        await r.AssertStatusAsync(HttpStatusCode.OK);
        return (await r.JsonAsync()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> PingAsync(Prestador p, double lat, double lon, double precisao = 5)
    {
        var r = await p.Client.PostAsJsonAsync("api/Agendamentos/localizacao",
            new { usuarioId = p.Id, latitude = lat, longitude = lon, precisao });
        await r.AssertStatusAsync(HttpStatusCode.OK);
        return await r.JsonAsync();
    }

    private static Task AtualizarAsync(Guid id, UpdateDefinition<Agendamento> update) =>
        new ContextMongodb().Agendamento.UpdateOneAsync(a => a.Id == id, update);

    private static List<(Guid ag, string evento, string status)> Eventos(JsonElement json) =>
        json.GetProperty("eventos").EnumerateArray()
            .Select(e => (e.GetProperty("agendamentoId").GetGuid(),
                          e.GetProperty("evento").GetString()!,
                          e.GetProperty("status").GetString()!))
            .ToList();

    private static string Observacao(JsonElement json, Guid agendamento) =>
        json.GetProperty("leituras").EnumerateArray()
            .Single(l => l.GetProperty("agendamentoId").GetGuid() == agendamento)
            .GetProperty("observacao").GetString()!;

    private static async Task<JsonElement> AgendamentoAsync(Prestador p, Guid id)
    {
        var lista = await p.Client.GetFromJsonAsync<JsonElement>($"api/Agendamentos/{p.Id}");
        return lista.EnumerateArray().Single(a => a.GetProperty("id").GetGuid() == id);
    }

    [Fact(DisplayName = "IT-108 Geofence: chegada, início automático, pausa ao sair, retomada e conclusão")]
    public async Task Geofence_CicloCompletoDoAtendimento()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await ClienteComLocalAsync(p, Lat, Lon);
        var servico = await p.CriarServicoAsync();
        var ag = await p.CriarAgendamentoAsync(cliente, servico, DateTime.UtcNow.AddHours(1));

        var chegada = await PingAsync(p, Lat, Lon);
        Assert.Contains(Eventos(chegada), e => e.ag == ag && e.evento == "Chegou");
        Assert.True(chegada.GetProperty("rastrear").GetBoolean());
        Assert.Equal(30, chegada.GetProperty("intervaloSegundos").GetInt32());

        var aguardando = await PingAsync(p, Lat, Lon);
        Assert.Empty(Eventos(aguardando));
        Assert.StartsWith("aguardando", Observacao(aguardando, ag));

        await AtualizarAsync(ag, Builders<Agendamento>.Update
            .Set(a => a.ChegadaEm, (DateTime?)DateTime.UtcNow.AddMinutes(-6))
            .Set(a => a.UltimoPingEm, (DateTime?)DateTime.UtcNow.AddSeconds(-30)));

        var inicio = await PingAsync(p, Lat, Lon);
        Assert.Contains(Eventos(inicio), e => e.ag == ag && e.evento == "Iniciou" && e.status == "EmAtendimento");

        var saiu = await PingAsync(p, Lat + DeslocamentoLonge, Lon);
        Assert.Contains(Eventos(saiu), e => e.ag == ag && e.evento == "Pausou" && e.status == "EmPausa");

        var voltou = await PingAsync(p, Lat, Lon);
        Assert.Contains(Eventos(voltou), e => e.ag == ag && e.evento == "Retomou" && e.status == "EmAtendimento");

        (await p.Client.PutAsync($"api/Agendamentos/concluir/{ag}", null)).EnsureSuccessStatusCode();

        var final = await AgendamentoAsync(p, ag);
        Assert.Equal("Concluido", final.GetProperty("status").GetString());
        Assert.True(final.GetProperty("iniciadoPorGeofence").GetBoolean());
        Assert.InRange(final.GetProperty("duracaoMinutos").GetDouble(), 5, 7);
    }

    [Fact(DisplayName = "IT-109 Geofence: sair do raio antes de confirmar descarta a chegada")]
    public async Task Geofence_SairAntesDeConfirmar_DescartaChegada()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await ClienteComLocalAsync(p, Lat, Lon);
        var servico = await p.CriarServicoAsync();
        var ag = await p.CriarAgendamentoAsync(cliente, servico, DateTime.UtcNow.AddHours(1));
        await PingAsync(p, Lat, Lon);

        var saiu = await PingAsync(p, Lat + DeslocamentoLonge, Lon);

        Assert.Contains(Eventos(saiu), e => e.ag == ag && e.evento == "ChegadaDescartada");
        Assert.Equal("Agendado", (await AgendamentoAsync(p, ag)).GetProperty("status").GetString());
    }

    [Fact(DisplayName = "IT-110 Geofence: 30 minutos fora do raio concluem o atendimento automaticamente")]
    public async Task Geofence_PausaLonga_ConcluiAutomaticamente()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await ClienteComLocalAsync(p, Lat, Lon);
        var servico = await p.CriarServicoAsync();
        var ag = await p.CriarAgendamentoAsync(cliente, servico, DateTime.UtcNow.AddHours(1));
        var agora = DateTime.UtcNow;
        await AtualizarAsync(ag, Builders<Agendamento>.Update
            .Set(a => a.Status, StatusAgendamento.EmPausa)
            .Set(a => a.Inicio, (DateTime?)agora.AddMinutes(-60))
            .Set(a => a.InicioPausa, (DateTime?)agora.AddMinutes(-31)));

        var resultado = await PingAsync(p, Lat + DeslocamentoLonge, Lon);

        Assert.Contains(Eventos(resultado), e => e.ag == ag && e.evento == "Concluiu" && e.status == "Concluido");
        var final = await AgendamentoAsync(p, ag);
        Assert.Equal("Concluido", final.GetProperty("status").GetString());
        Assert.InRange(final.GetProperty("duracaoMinutos").GetDouble(), 28.5, 29.5);
    }

    [Fact(DisplayName = "IT-111 Geofence: concluir manualmente um atendimento em pausa usa o início da pausa")]
    public async Task Concluir_EmPausa_UsaInicioDaPausa()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();
        var servico = await p.CriarServicoAsync();
        var ag = await p.CriarAgendamentoAsync(cliente, servico, DateTime.UtcNow.AddHours(1));
        var agora = DateTime.UtcNow;
        await AtualizarAsync(ag, Builders<Agendamento>.Update
            .Set(a => a.Status, StatusAgendamento.EmPausa)
            .Set(a => a.Inicio, (DateTime?)agora.AddMinutes(-40))
            .Set(a => a.InicioPausa, (DateTime?)agora.AddMinutes(-10)));

        (await p.Client.PutAsync($"api/Agendamentos/concluir/{ag}", null)).EnsureSuccessStatusCode();

        var final = await AgendamentoAsync(p, ag);
        Assert.Equal("Concluido", final.GetProperty("status").GetString());
        Assert.InRange(final.GetProperty("duracaoMinutos").GetDouble(), 29.5, 30.5);
    }

    [Fact(DisplayName = "IT-112 Geofence: leitura com precisão ruim é ignorada")]
    public async Task Geofence_PrecisaoRuim_EIgnorada()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await ClienteComLocalAsync(p, Lat, Lon);
        var servico = await p.CriarServicoAsync();
        var ag = await p.CriarAgendamentoAsync(cliente, servico, DateTime.UtcNow.AddHours(1));

        var r = await PingAsync(p, Lat, Lon, precisao: 5000);

        Assert.Empty(Eventos(r));
        var leitura = r.GetProperty("leituras").EnumerateArray().Single();
        Assert.True(leitura.GetProperty("ignorada").GetBoolean());
        Assert.StartsWith("leitura ignorada", Observacao(r, ag));
    }

    [Fact(DisplayName = "IT-113 Geofence: cliente sem localização cadastrada não é rastreado")]
    public async Task Geofence_SemLocalizacao_NaoRastreia()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await ClienteComLocalAsync(p, null, null);
        var servico = await p.CriarServicoAsync();
        var ag = await p.CriarAgendamentoAsync(cliente, servico, DateTime.UtcNow.AddHours(1));

        var r = await PingAsync(p, Lat, Lon);

        Assert.False(r.GetProperty("rastrear").GetBoolean());
        Assert.StartsWith("sem localização", Observacao(r, ag));
    }

    [Fact(DisplayName = "IT-114 Geofence: longe do raio a leitura informa que ainda está fora")]
    public async Task Geofence_LongeDoRaio_InformaDistancia()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await ClienteComLocalAsync(p, Lat, Lon);
        var servico = await p.CriarServicoAsync();
        var ag = await p.CriarAgendamentoAsync(cliente, servico, DateTime.UtcNow.AddHours(1));

        var r = await PingAsync(p, Lat + DeslocamentoLonge, Lon);

        Assert.Empty(Eventos(r));
        Assert.Equal("ainda fora do raio", Observacao(r, ag));
        Assert.True(r.GetProperty("leituras")[0].GetProperty("distanciaMetros").GetDouble() > 1000);
    }

    [Fact(DisplayName = "IT-115 Geofence: com dois agendamentos no mesmo local vale o de horário mais próximo")]
    public async Task Geofence_DoisAgendamentosNoMesmoLocal_TemPrioridade()
    {
        var p = await _f.CriarPrestadorAsync();
        var clienteA = await ClienteComLocalAsync(p, Lat, Lon);
        var clienteB = await ClienteComLocalAsync(p, Lat, Lon);
        var servico = await p.CriarServicoAsync();
        var proximo = await p.CriarAgendamentoAsync(clienteA, servico, DateTime.UtcNow.AddHours(1));
        var distante = await p.CriarAgendamentoAsync(clienteB, servico, DateTime.UtcNow.AddHours(3));

        var r = await PingAsync(p, Lat, Lon);

        Assert.Contains(Eventos(r), e => e.ag == proximo && e.evento == "Chegou");
        Assert.DoesNotContain(Eventos(r), e => e.ag == distante);
        Assert.Contains("prioridade", Observacao(r, distante));
    }

    [Fact(DisplayName = "IT-116 Geofence: atendimento em andamento bloqueia a chegada a outro cliente no mesmo local")]
    public async Task Geofence_OutroAtendimentoEmAndamento_Bloqueia()
    {
        var p = await _f.CriarPrestadorAsync();
        var clienteA = await ClienteComLocalAsync(p, Lat, Lon);
        var clienteB = await ClienteComLocalAsync(p, Lat, Lon);
        var servico = await p.CriarServicoAsync();
        var emAndamento = await p.CriarAgendamentoAsync(clienteA, servico, DateTime.UtcNow.AddMinutes(-20));
        var esperando = await p.CriarAgendamentoAsync(clienteB, servico, DateTime.UtcNow.AddHours(1));
        await AtualizarAsync(emAndamento, Builders<Agendamento>.Update
            .Set(a => a.Status, StatusAgendamento.EmAtendimento)
            .Set(a => a.Inicio, (DateTime?)DateTime.UtcNow.AddMinutes(-10)));

        var r = await PingAsync(p, Lat, Lon);

        Assert.Equal("em atendimento, dentro do raio", Observacao(r, emAndamento));
        Assert.Contains("outro atendimento em andamento", Observacao(r, esperando));
        Assert.Empty(Eventos(r));
    }

    [Fact(DisplayName = "IT-117 Geofence: chegar a outro cliente conclui o atendimento que estava em pausa")]
    public async Task Geofence_ChegarAOutroCliente_ConcluiPausado()
    {
        var p = await _f.CriarPrestadorAsync();
        var clienteA = await ClienteComLocalAsync(p, Lat, Lon);
        var clienteB = await ClienteComLocalAsync(p, Lat + 0.05, Lon);
        var servico = await p.CriarServicoAsync();
        var pausado = await p.CriarAgendamentoAsync(clienteA, servico, DateTime.UtcNow.AddMinutes(-30));
        var novo = await p.CriarAgendamentoAsync(clienteB, servico, DateTime.UtcNow.AddHours(1));
        var agora = DateTime.UtcNow;
        await AtualizarAsync(pausado, Builders<Agendamento>.Update
            .Set(a => a.Status, StatusAgendamento.EmPausa)
            .Set(a => a.Inicio, (DateTime?)agora.AddMinutes(-20))
            .Set(a => a.InicioPausa, (DateTime?)agora.AddMinutes(-5)));

        var r = await PingAsync(p, Lat + 0.05, Lon);

        Assert.Contains(Eventos(r), e => e.ag == novo && e.evento == "Chegou");
        Assert.Contains(Eventos(r), e => e.ag == pausado && e.evento == "Concluiu");
        Assert.Equal("Concluido", (await AgendamentoAsync(p, pausado)).GetProperty("status").GetString());
    }
}
