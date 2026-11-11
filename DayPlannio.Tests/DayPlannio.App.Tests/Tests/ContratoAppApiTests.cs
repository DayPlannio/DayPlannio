using System.Text.Json;
using DayPlannio.App.Helpers;
using Xunit;
using ApiGeofence = DayPlannio.Api.Services.GeofenceRegras;

namespace DayPlannio.App.Tests.Tests;

[Trait("Categoria", "Contrato")]
public class ContratoAppApiTests
{
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNameCaseInsensitive = true };

    [Fact(DisplayName = "AC-01 O raio padrão do App é igual ao da API")]
    public void RaioPadrao_AppIgualApi() => Assert.Equal(ApiGeofence.RaioPadraoMetros, RaioGeofence.Padrao);

    [Fact(DisplayName = "AC-02 Todas as opções de raio oferecidas pelo App são aceitas pela API sem alteração")]
    public void OpcoesDeRaio_AppAceitasPelaApi()
    {
        foreach (var texto in RaioGeofence.Opcoes)
        {
            var metros = RaioGeofence.ParaMetros(texto);
            Assert.Equal(metros, ApiGeofence.NormalizarRaio(metros));
        }
        Assert.Equal(ApiGeofence.RaiosPermitidos.OrderBy(x => x), new[] { 20, 50, 100 });
    }

    [Fact(DisplayName = "AC-03 O modelo Cliente do App nasce com o mesmo raio padrão da API")]
    public void ModeloCliente_RaioPadrao()
    {
        Assert.Equal(RaioGeofence.Padrao, new DayPlannio.App.Models.Cliente().RaioGeofenceMetros);
        Assert.Equal(RaioGeofence.Padrao, new DayPlannio.Api.Models.Cliente().RaioGeofenceMetros);
    }

    [Fact(DisplayName = "AC-04 O JSON de um agendamento da API (status como texto, GUIDs, nulos) é lido pelo modelo do App")]
    public void JsonDeAgendamento_DaApi_ELidoPeloApp()
    {
        var id = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new DayPlannio.Api.Models.Agendamento
        {
            Id = id,
            Status = DayPlannio.Api.Models.StatusAgendamento.EmPausa,
            ValorCobrado = 150.75m,
            DataHora = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc),
            Observacoes = null
        }, new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });

        var app = JsonSerializer.Deserialize<DayPlannio.App.Models.Agendamento>(json, Opcoes)!;

        Assert.Equal(id.ToString(), app.Id);
        Assert.Equal("EmPausa", app.Status);
        Assert.Equal(150.75m, app.ValorCobrado);
        Assert.Null(app.Observacoes);
    }

    [Fact(DisplayName = "AC-05 O JSON de um lançamento financeiro da API (tipo como texto) é lido pelo modelo do App")]
    public void JsonDeFinanceiro_DaApi_ELidoPeloApp()
    {
        var json = JsonSerializer.Serialize(new DayPlannio.Api.Models.Financeiro
        {
            Id = Guid.NewGuid(), UsuarioId = Guid.NewGuid(),
            Tipo = DayPlannio.Api.Models.TipoFinanceiro.Entrada, Descricao = "Pintura", Valor = 300m, Data = DateTime.UtcNow
        }, new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });

        var app = JsonSerializer.Deserialize<DayPlannio.App.Models.Financeiro>(json, Opcoes)!;

        Assert.Equal("Entrada", app.Tipo);
        Assert.Equal(300m, app.Valor);
    }
}
