using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Tests;

[Collection(ApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class AdminApiTests
{
    private readonly ApiFactory _f;
    public AdminApiTests(ApiFactory f) => _f = f;

    private static async Task<JsonElement> NotificacoesAsync(Prestador p) =>
        await p.Client.GetFromJsonAsync<JsonElement>($"api/Admin/notificacoes/{p.Id}");

    private static JsonElement? Achar(JsonElement lista, string campo, string valor) =>
        lista.EnumerateArray().Cast<JsonElement?>()
            .FirstOrDefault(x => x!.Value.GetProperty(campo).GetString() == valor);

    private static JsonElement? AcharPorId(JsonElement lista, Guid id) =>
        lista.EnumerateArray().Cast<JsonElement?>()
            .FirstOrDefault(x => x!.Value.GetProperty("id").GetGuid() == id);

    [Fact(DisplayName = "IT-58 Login do Admin sem e-mail ou senha retorna 400")]
    public async Task Login_CamposVazios_Retorna400()
    {
        var r = await _f.NovoClient().PostAsJsonAsync("api/Admin/login", new { email = "", senha = "" });

        await r.AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-59 Login do Admin com e-mail desconhecido ou senha errada retorna 401")]
    public async Task Login_Invalido_Retorna401()
    {
        var desconhecido = await _f.NovoClient().PostAsJsonAsync("api/Admin/login",
            new { email = $"naoexiste_{Guid.NewGuid():N}@teste.local", senha = "Qualquer@123" });
        var senhaErrada = await _f.NovoClient().PostAsJsonAsync("api/Admin/login",
            new { email = ApiFactory.AdminEmail, senha = "Errada@12345" });

        await desconhecido.AssertStatusAsync(HttpStatusCode.Unauthorized);
        await senhaErrada.AssertStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "IT-60 Logout do Admin responde 200 e registra o log")]
    public async Task Logout_Admin_Retorna200()
    {
        var admin = await _f.ClienteAdminLogadoAsync();

        var r = await admin.PostAsync("api/Admin/logout", null);

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var logs = await admin.GetFromJsonAsync<JsonElement>("api/Admin/logs?pagina=1&tamanhoPagina=50");
        Assert.Contains(logs.GetProperty("logs").EnumerateArray(),
            l => l.GetProperty("tipo").GetString() == "logout_admin");
    }

    [Fact(DisplayName = "IT-61 Logs do Admin respeitam a paginação")]
    public async Task Logs_RespeitamPaginacao()
    {
        await _f.CriarPrestadorAsync();
        await _f.CriarPrestadorAsync();

        var r = await _f.ClientAdmin().GetFromJsonAsync<JsonElement>("api/Admin/logs?pagina=1&tamanhoPagina=2");

        Assert.Equal(1, r.GetProperty("pagina").GetInt32());
        Assert.Equal(2, r.GetProperty("tamanhoPagina").GetInt32());
        Assert.True(r.GetProperty("total").GetInt64() >= 2);
        Assert.Equal(2, r.GetProperty("logs").GetArrayLength());
    }

    [Fact(DisplayName = "IT-62 Admin lista todos os clientes cadastrados")]
    public async Task Admin_ListaClientes()
    {
        var p = await _f.CriarPrestadorAsync();
        var clienteId = await p.CriarClienteAsync("Cliente Listado");

        var lista = await _f.ClientAdmin().GetFromJsonAsync<JsonElement>("api/Admin/clientes");

        var item = AcharPorId(lista, clienteId);
        Assert.NotNull(item);
        Assert.Equal("Cliente Listado", item!.Value.GetProperty("nome").GetString());
        Assert.True(item.Value.GetProperty("ativo").GetBoolean());
        Assert.True(item.Value.GetProperty("primeiroAcesso").GetBoolean());
    }

    [Fact(DisplayName = "IT-63 Admin lista todos os prestadores com os dados do plano")]
    public async Task Admin_ListaPrestadores()
    {
        var p = await _f.CriarPrestadorAsync();

        var lista = await _f.ClientAdmin().GetFromJsonAsync<JsonElement>("api/Admin/prestadores");

        var item = AcharPorId(lista, p.Id);
        Assert.NotNull(item);
        Assert.Equal(p.Email, item!.Value.GetProperty("email").GetString());
        Assert.Equal("Full", item.Value.GetProperty("plano").GetString());
        Assert.Equal("trial", item.Value.GetProperty("planoOrigem").GetString());
        Assert.True(item.Value.GetProperty("planoAtivo").GetBoolean());
    }

    [Fact(DisplayName = "IT-64 Aprovar plano ativa a assinatura, liga a renovação e notifica o prestador")]
    public async Task AprovarPlano_AtivaAssinaturaENotifica()
    {
        var p = await _f.CriarPrestadorAsync(testeGratuito: false, plano: "Profissional");

        var r = await _f.ClientAdmin().PostAsync($"api/Admin/planos/{p.Id}/aprovar", null);

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var perfil = await p.Client.GetFromJsonAsync<JsonElement>($"api/Users/meu-perfil/{p.Id}");
        Assert.Equal("Profissional", perfil.GetProperty("plano").GetString());
        Assert.True(perfil.GetProperty("planoAtivo").GetBoolean());
        Assert.Equal("assinatura", perfil.GetProperty("planoOrigem").GetString());
        Assert.True(perfil.GetProperty("renovacaoAutomatica").GetBoolean());
        Assert.Equal(JsonValueKind.Null, perfil.GetProperty("planoPendente").ValueKind);
        Assert.NotNull(Achar(await NotificacoesAsync(p), "tipo", "plano_aprovado"));
    }

    [Fact(DisplayName = "IT-65 Aprovar ou rejeitar plano de prestador inexistente retorna 404")]
    public async Task Planos_PrestadorInexistente_Retorna404()
    {
        var admin = _f.ClientAdmin();

        var aprovar = await admin.PostAsync($"api/Admin/planos/{Guid.NewGuid()}/aprovar", null);
        var rejeitar = await admin.PostAsync($"api/Admin/planos/{Guid.NewGuid()}/rejeitar", null);

        await aprovar.AssertStatusAsync(HttpStatusCode.NotFound);
        await rejeitar.AssertStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-66 Rejeitar plano limpa a solicitação e notifica; sem pendência retorna 400")]
    public async Task RejeitarPlano_LimpaSolicitacaoENotifica()
    {
        var p = await _f.CriarPrestadorAsync(testeGratuito: false, plano: "Basico");
        var admin = _f.ClientAdmin();

        var r = await admin.PostAsync($"api/Admin/planos/{p.Id}/rejeitar", null);
        var repetido = await admin.PostAsync($"api/Admin/planos/{p.Id}/rejeitar", null);

        await r.AssertStatusAsync(HttpStatusCode.OK);
        await repetido.AssertStatusAsync(HttpStatusCode.BadRequest);
        var perfil = await p.Client.GetFromJsonAsync<JsonElement>($"api/Users/meu-perfil/{p.Id}");
        Assert.Equal(JsonValueKind.Null, perfil.GetProperty("planoPendente").ValueKind);
        Assert.Equal("SemPlano", perfil.GetProperty("planoEfetivo").GetString());
        Assert.NotNull(Achar(await NotificacoesAsync(p), "tipo", "plano_rejeitado"));
    }

    [Fact(DisplayName = "IT-67 Notificações: contar não lidas, marcar como lida e proteger contra outro usuário")]
    public async Task Notificacoes_ContarMarcarEProteger()
    {
        var p = await _f.CriarPrestadorComPlanoAsync("Basico");
        var outro = await _f.CriarPrestadorAsync();

        var naoLidas = await p.Client.GetFromJsonAsync<JsonElement>($"api/Admin/notificacoes/{p.Id}/nao-lidas");
        Assert.Equal(1, naoLidas.GetProperty("total").GetInt32());

        var notificacao = (await NotificacoesAsync(p)).EnumerateArray().Single();
        var id = notificacao.GetProperty("id").GetGuid();
        Assert.False(notificacao.GetProperty("lida").GetBoolean());

        await (await outro.Client.PutAsync($"api/Admin/notificacoes/{id}/lida", null)).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await outro.Client.GetAsync($"api/Admin/notificacoes/{p.Id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await outro.Client.GetAsync($"api/Admin/notificacoes/{p.Id}/nao-lidas")).AssertStatusAsync(HttpStatusCode.Forbidden);

        await (await p.Client.PutAsync($"api/Admin/notificacoes/{id}/lida", null)).AssertStatusAsync(HttpStatusCode.OK);

        naoLidas = await p.Client.GetFromJsonAsync<JsonElement>($"api/Admin/notificacoes/{p.Id}/nao-lidas");
        Assert.Equal(0, naoLidas.GetProperty("total").GetInt32());
        Assert.True((await NotificacoesAsync(p)).EnumerateArray().Single().GetProperty("lida").GetBoolean());
    }

    [Fact(DisplayName = "IT-68 Marcar notificação inexistente como lida retorna 404")]
    public async Task MarcarLida_Inexistente_Retorna404()
    {
        var p = await _f.CriarPrestadorAsync();

        var r = await p.Client.PutAsync($"api/Admin/notificacoes/{Guid.NewGuid()}/lida", null);

        await r.AssertStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-69 Fotos pendentes: Admin aprova e a foto aparece no portfólio público")]
    public async Task Fotos_AdminAprova_AparecemNoPortfolio()
    {
        var p = await _f.CriarPrestadorAsync();
        var servico = await p.CriarServicoAsync("Pintura");
        var cliente = await p.CriarClienteAsync();
        var agendamento = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));
        var foto = await p.InserirFotoAsync(agendamento, publica: true, aprovada: false);
        var admin = _f.ClientAdmin();

        var pendentes = await admin.GetFromJsonAsync<JsonElement>("api/Admin/fotos-pendentes");
        var pendente = AcharPorId(pendentes, foto.Id);
        Assert.NotNull(pendente);
        Assert.Equal(p.Nome, pendente!.Value.GetProperty("prestador").GetString());
        Assert.Equal("Pintura", pendente.Value.GetProperty("servico").GetString());
        Assert.StartsWith("https://contateste.blob.core.windows.net/fotos/", pendente.Value.GetProperty("url").GetString());

        var antes = await _f.NovoClient().GetFromJsonAsync<JsonElement>("api/Fotos/portfolio-publico");
        Assert.Null(AcharPorId(antes, foto.Id));

        await (await admin.PutAsync($"api/Admin/fotos/{foto.Id}/aprovar", null)).AssertStatusAsync(HttpStatusCode.OK);

        var depois = await _f.NovoClient().GetFromJsonAsync<JsonElement>("api/Fotos/portfolio-publico");
        var publica = AcharPorId(depois, foto.Id);
        Assert.NotNull(publica);
        Assert.Equal(p.Nome, publica!.Value.GetProperty("prestador").GetString());
        Assert.Equal("Pintura", publica.Value.GetProperty("servico").GetString());

        var pendentesDepois = await admin.GetFromJsonAsync<JsonElement>("api/Admin/fotos-pendentes");
        Assert.Null(AcharPorId(pendentesDepois, foto.Id));
        Assert.NotNull(Achar(await NotificacoesAsync(p), "tipo", "foto_aprovada"));
    }

    [Fact(DisplayName = "IT-70 Admin rejeita foto: ela é removida do banco e do armazenamento")]
    public async Task Fotos_AdminRejeita_RemoveDoBancoEDoBlob()
    {
        var p = await _f.CriarPrestadorAsync();
        var servico = await p.CriarServicoAsync();
        var cliente = await p.CriarClienteAsync();
        var agendamento = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(3));
        var foto = await p.InserirFotoAsync(agendamento);
        var nomeBlob = Path.GetFileName(new Uri(foto.Url).AbsolutePath);

        var r = await _f.ClientAdmin().PutAsync($"api/Admin/fotos/{foto.Id}/rejeitar", null);

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var pendentes = await _f.ClientAdmin().GetFromJsonAsync<JsonElement>("api/Admin/fotos-pendentes");
        Assert.Null(AcharPorId(pendentes, foto.Id));
        Assert.Contains(_f.Blob.Chamadas, c => c.StartsWith("DELETE") && c.Contains(nomeBlob));
        Assert.NotNull(Achar(await NotificacoesAsync(p), "tipo", "foto_rejeitada"));
    }

    [Fact(DisplayName = "IT-71 Aprovar ou rejeitar foto inexistente retorna 404")]
    public async Task Fotos_Inexistente_Retorna404()
    {
        var admin = _f.ClientAdmin();

        await (await admin.PutAsync($"api/Admin/fotos/{Guid.NewGuid()}/aprovar", null)).AssertStatusAsync(HttpStatusCode.NotFound);
        await (await admin.PutAsync($"api/Admin/fotos/{Guid.NewGuid()}/rejeitar", null)).AssertStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-72 Prestador comum não aprova fotos nem planos")]
    public async Task PrestadorComum_NaoAprovaNada()
    {
        var p = await _f.CriarPrestadorAsync();

        (await p.Client.PutAsync($"api/Admin/fotos/{Guid.NewGuid()}/aprovar", null)).AssertProibido();
        (await p.Client.PostAsync($"api/Admin/planos/{p.Id}/aprovar", null)).AssertProibido();
        (await p.Client.GetAsync("api/Admin/fotos-pendentes")).AssertProibido();
    }

    [Theory(DisplayName = "IT-73 Encerrar conta exige motivo válido e tipo conhecido")]
    [InlineData("", "cliente")]
    [InlineData("abc", "cliente")]
    [InlineData("Motivo válido para encerrar", "outro")]
    public async Task EncerrarConta_DadosInvalidos_Retorna400(string motivo, string tipo)
    {
        var admin = await _f.ClienteAdminLogadoAsync();

        var r = await admin.PostAsJsonAsync("api/Admin/contas/encerrar",
            new { tipo, id = Guid.NewGuid(), motivo });

        await r.AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-74 Encerrar conta pela API key (sem administrador logado) retorna 401")]
    public async Task EncerrarConta_SemAdminLogado_Retorna401()
    {
        var r = await _f.ClientAdmin().PostAsJsonAsync("api/Admin/contas/encerrar",
            new { tipo = "cliente", id = Guid.NewGuid(), motivo = "Motivo de teste" });

        await r.AssertStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "IT-75 Encerrar conta inexistente retorna 404")]
    public async Task EncerrarConta_Inexistente_Retorna404()
    {
        var admin = await _f.ClienteAdminLogadoAsync();

        var prestador = await admin.PostAsJsonAsync("api/Admin/contas/encerrar",
            new { tipo = "prestador", id = Guid.NewGuid(), motivo = "Motivo de teste" });
        var cliente = await admin.PostAsJsonAsync("api/Admin/contas/encerrar",
            new { tipo = "Cliente", id = Guid.NewGuid(), motivo = "Motivo de teste" });

        await prestador.AssertStatusAsync(HttpStatusCode.NotFound);
        await cliente.AssertStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-76 Não é possível encerrar a conta de um administrador")]
    public async Task EncerrarConta_DeAdmin_Retorna400()
    {
        var admin = await _f.ClienteAdminLogadoAsync();
        var adminId = await _f.AdminIdAsync();

        var r = await admin.PostAsJsonAsync("api/Admin/contas/encerrar",
            new { tipo = "prestador", id = adminId, motivo = "Tentativa indevida" });

        await r.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("administrador", await r.MensagemAsync());
    }

    [Fact(DisplayName = "IT-77 Admin encerra conta de cliente: agenda futura é removida e o prestador é avisado")]
    public async Task EncerrarConta_Cliente_RemoveFuturosENotificaPrestador()
    {
        var p = await _f.CriarPrestadorAsync();
        var servico = await p.CriarServicoAsync();
        var cliente = await p.CriarClienteAsync("Cliente a Encerrar");
        await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(4));
        var admin = await _f.ClienteAdminLogadoAsync();

        var r = await admin.PostAsJsonAsync("api/Admin/contas/encerrar",
            new { tipo = "cliente", id = cliente, motivo = "Pedido do próprio cliente" });

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var json = await r.JsonAsync();
        Assert.Equal(1, json.GetProperty("agendamentosAfetados").GetInt32());

        var clientes = await p.Client.GetFromJsonAsync<JsonElement>($"api/Cliente/{p.Id}");
        Assert.Equal(0, clientes.GetArrayLength());

        var aviso = Achar(await NotificacoesAsync(p), "tipo", "cliente_encerrou_conta");
        Assert.NotNull(aviso);
        Assert.Contains("1 agendamento futuro", aviso!.Value.GetProperty("mensagem").GetString());
    }

    [Fact(DisplayName = "IT-78 Admin encerra conta de prestador: dados somem, login falha e clientes são avisados por e-mail")]
    public async Task EncerrarConta_Prestador_RemoveTudoEAvisaClientes()
    {
        var p = await _f.CriarPrestadorAsync();
        var servico = await p.CriarServicoAsync();
        var cliente = await p.CriarClienteAsync("Cliente do Prestador");
        await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(5));
        var emailSecundario = $"aviso_{Guid.NewGuid():N}@teste.local";
        var sec = await _f.ClientAdmin().PostAsJsonAsync("api/Cliente/definir-email-secundario",
            new { clienteId = cliente, emailSecundario });
        await sec.AssertStatusAsync(HttpStatusCode.OK);
        var admin = await _f.ClienteAdminLogadoAsync();

        var r = await admin.PostAsJsonAsync("api/Admin/contas/encerrar",
            new { tipo = "prestador", id = p.Id, motivo = "Violação dos termos de uso" });

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var json = await r.JsonAsync();
        Assert.Equal(1, json.GetProperty("agendamentosAfetados").GetInt32());
        Assert.Contains(p.Nome, json.GetProperty("message").GetString());

        var login = await _f.NovoClient().PostAsJsonAsync("api/Account/login", new { email = p.Email, senha = p.Senha });
        Assert.False(login.IsSuccessStatusCode);

        var prestadores = await _f.ClientAdmin().GetFromJsonAsync<JsonElement>("api/Admin/prestadores");
        Assert.Null(AcharPorId(prestadores, p.Id));

        await AdminHelper.EsperarAsync(() => _f.Emails.Enviados.Any(e =>
            e.Para == emailSecundario && e.Assunto.Contains("encerrado")));
    }
}
