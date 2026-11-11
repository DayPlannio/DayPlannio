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
public class ClientesApiTests
{
    private readonly ApiFactory _f;
    public ClientesApiTests(ApiFactory f) => _f = f;

    [Fact(DisplayName = "IT-135 Editar cliente atualiza os dados e normaliza o raio do geofence")]
    public async Task Editar_AtualizaENormalizaRaio()
    {
        var p = await _f.CriarPrestadorAsync();
        var id = await p.CriarClienteAsync("Nome Antigo");

        var r = await p.Client.PutAsJsonAsync($"api/Cliente/edit/{id}", new
        {
            usuarioId = p.Id,
            nome = "Nome Novo",
            telefone = "14988887777",
            endereco = "Avenida Nova, 200",
            latitude = -22.29,
            longitude = -48.55,
            raioGeofenceMetros = 75,
            observacoes = "Portão azul"
        });

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var c = (await p.Client.GetFromJsonAsync<JsonElement>($"api/Cliente/{p.Id}")).EnumerateArray().Single();
        Assert.Equal("Nome Novo", c.GetProperty("nome").GetString());
        Assert.Equal("Avenida Nova, 200", c.GetProperty("endereco").GetString());
        Assert.Equal(100, c.GetProperty("raioGeofenceMetros").GetInt32());
        Assert.Equal("Portão azul", c.GetProperty("observacoes").GetString());
        Assert.Equal(-22.29, c.GetProperty("latitude").GetDouble());
    }

    [Fact(DisplayName = "IT-136 Editar cliente: inexistente 404, outro prestador 403, nome ou telefone vazios 400")]
    public async Task Editar_Validacoes()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var id = await a.CriarClienteAsync();
        object Corpo(Prestador p, string nome, string telefone) => new
        {
            usuarioId = p.Id, nome, telefone, endereco = "Rua X, 1"
        };

        await (await a.Client.PutAsJsonAsync($"api/Cliente/edit/{Guid.NewGuid()}", Corpo(a, "N", "14999990000")))
            .AssertStatusAsync(HttpStatusCode.NotFound);
        await (await b.Client.PutAsJsonAsync($"api/Cliente/edit/{id}", Corpo(b, "Invasor", "14999990000")))
            .AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await a.Client.PutAsJsonAsync($"api/Cliente/edit/{id}", Corpo(a, "  ", "14999990000")))
            .AssertStatusAsync(HttpStatusCode.BadRequest);
        await (await a.Client.PutAsJsonAsync($"api/Cliente/edit/{id}", Corpo(a, "Nome", "  ")))
            .AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-137 Gerar credenciais mantém o e-mail já cadastrado e cria e-mails únicos para os demais")]
    public async Task GerarCredenciais_EmailsUnicos()
    {
        var p = await _f.CriarPrestadorAsync();
        var comEmail = await p.Client.PostAsJsonAsync("api/Cliente/create", new
        {
            usuarioId = p.Id,
            nome = "Cliente Com Email",
            telefone = "14999990000",
            endereco = "Rua A, 1",
            email = $"proprio_{Guid.NewGuid():N}@teste.local"
        });
        var idComEmail = (await comEmail.JsonAsync()).GetProperty("id").GetGuid();
        var primeiro = await p.CriarClienteAsync("Maria Teste");
        var segundo = await p.CriarClienteAsync("Maria Teste");

        var existente = await p.Client.PostAsync($"api/Cliente/gerar-credenciais/{idComEmail}", null);
        var a = await p.GerarAcessoClienteAsync(primeiro);
        var b = await p.GerarAcessoClienteAsync(segundo);

        await existente.AssertStatusAsync(HttpStatusCode.OK);
        Assert.StartsWith("proprio_", (await existente.JsonAsync()).GetProperty("email").GetString());
        Assert.NotEqual(a.Email, b.Email);
        Assert.StartsWith("mariateste_prestador", a.Email);
        Assert.EndsWith("@dayplannio.com", b.Email);
        await (await p.Client.PostAsync($"api/Cliente/gerar-credenciais/{Guid.NewGuid()}", null))
            .AssertStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-138 Gerar credenciais de cliente de outro prestador retorna 403")]
    public async Task GerarCredenciais_DeOutro_Retorna403()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var cliente = await a.CriarClienteAsync();

        var r = await b.Client.PostAsync($"api/Cliente/gerar-credenciais/{cliente}", null);

        await r.AssertStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "IT-139 Registrar concordância do cliente com a política: só o servidor web (Admin) pode")]
    public async Task PoliticaDoCliente_SoAdmin()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();

        var ok = await _f.ClientAdmin().PostAsync($"api/Cliente/politica/aceitar/{cliente}", null);
        var inexistente = await _f.ClientAdmin().PostAsync($"api/Cliente/politica/aceitar/{Guid.NewGuid()}", null);
        var prestador = await p.Client.PostAsync($"api/Cliente/politica/aceitar/{cliente}", null);

        await ok.AssertStatusAsync(HttpStatusCode.OK);
        Assert.False(string.IsNullOrWhiteSpace((await ok.JsonAsync()).GetProperty("versao").GetString()));
        await inexistente.AssertStatusAsync(HttpStatusCode.NotFound);
        await prestador.AssertStatusAsync(HttpStatusCode.Forbidden);
        var salvo = await new ContextMongodb().Cliente.Find(c => c.Id == cliente).SingleAsync();
        Assert.NotNull(salvo.PoliticaAceitaEm);
        Assert.NotNull(salvo.PoliticaVersao);
    }

    [Fact(DisplayName = "IT-140 Perfil do cliente mostra agendamentos futuros e política; só Admin consulta")]
    public async Task PerfilDoCliente()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();
        var servico = await p.CriarServicoAsync();
        await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));
        await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(3));

        var ok = await _f.ClientAdmin().GetAsync($"api/Cliente/perfil/{cliente}");
        var inexistente = await _f.ClientAdmin().GetAsync($"api/Cliente/perfil/{Guid.NewGuid()}");
        var prestador = await p.Client.GetAsync($"api/Cliente/perfil/{cliente}");

        await ok.AssertStatusAsync(HttpStatusCode.OK);
        var json = await ok.JsonAsync();
        Assert.Equal(2, json.GetProperty("agendamentosFuturos").GetInt32());
        Assert.Equal(string.Empty, json.GetProperty("email").GetString());
        await inexistente.AssertStatusAsync(HttpStatusCode.NotFound);
        await prestador.AssertStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "IT-141 E-mail secundário: formato inválido, vazio, cliente desconhecido e acesso indevido")]
    public async Task EmailSecundario_Validacoes()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();
        var admin = _f.ClientAdmin();

        var invalido = await admin.PostAsJsonAsync("api/Cliente/definir-email-secundario",
            new { clienteId = cliente, emailSecundario = "isso-nao-e-email" });
        var vazio = await admin.PostAsJsonAsync("api/Cliente/definir-email-secundario",
            new { clienteId = cliente, emailSecundario = "  " });
        var desconhecido = await admin.PostAsJsonAsync("api/Cliente/definir-email-secundario",
            new { clienteId = Guid.NewGuid(), emailSecundario = "ok@teste.local" });
        var prestador = await p.Client.PostAsJsonAsync("api/Cliente/definir-email-secundario",
            new { clienteId = cliente, emailSecundario = "ok@teste.local" });

        await invalido.AssertStatusAsync(HttpStatusCode.BadRequest);
        await vazio.AssertStatusAsync(HttpStatusCode.BadRequest);
        await desconhecido.AssertStatusAsync(HttpStatusCode.Unauthorized);
        await prestador.AssertStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "IT-142 Cliente encerra a própria conta: agenda futura cancelada e prestador notificado")]
    public async Task DeletarConta_CancelaFuturosENotifica()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync("Cliente que Sai");
        var servico = await p.CriarServicoAsync();
        await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));
        await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(3));

        var r = await _f.ClientAdmin().DeleteAsync($"api/Cliente/delete-conta/{cliente}?motivo=politica");

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var json = await r.JsonAsync();
        Assert.Equal(2, json.GetProperty("agendamentosFuturos").GetInt32());
        Assert.Contains("cancelados", json.GetProperty("message").GetString());
        var notificacoes = await p.Client.GetFromJsonAsync<JsonElement>($"api/Admin/notificacoes/{p.Id}");
        Assert.Contains(notificacoes.EnumerateArray(), n =>
            n.GetProperty("tipo").GetString() == "cliente_encerrou_conta" &&
            n.GetProperty("mensagem").GetString()!.Contains("2 agendamentos futuros"));
        var clientes = await p.Client.GetFromJsonAsync<JsonElement>($"api/Cliente/{p.Id}");
        Assert.Equal(0, clientes.GetArrayLength());
    }

    [Fact(DisplayName = "IT-143 Encerrar conta de cliente sem agenda futura; inexistente 404; prestador 403")]
    public async Task DeletarConta_SemFuturos()
    {
        var p = await _f.CriarPrestadorAsync();
        var cliente = await p.CriarClienteAsync();

        var ok = await _f.ClientAdmin().DeleteAsync($"api/Cliente/delete-conta/{cliente}");
        var inexistente = await _f.ClientAdmin().DeleteAsync($"api/Cliente/delete-conta/{Guid.NewGuid()}");
        var prestador = await p.Client.DeleteAsync($"api/Cliente/delete-conta/{cliente}");

        await ok.AssertStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, (await ok.JsonAsync()).GetProperty("agendamentosFuturos").GetInt32());
        await inexistente.AssertStatusAsync(HttpStatusCode.NotFound);
        await prestador.AssertStatusAsync(HttpStatusCode.Forbidden);
    }
}
