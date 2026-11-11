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
public class UsuariosApiTests
{
    private readonly ApiFactory _f;
    public UsuariosApiTests(ApiFactory f) => _f = f;

    private static async Task<JsonElement> PerfilAsync(Prestador p) =>
        await p.Client.GetFromJsonAsync<JsonElement>($"api/Users/meu-perfil/{p.Id}");

    [Fact(DisplayName = "IT-79 Meu perfil devolve os dados do plano e da política de privacidade")]
    public async Task MeuPerfil_DevolveDadosDoPlano()
    {
        var p = await _f.CriarPrestadorAsync();

        var perfil = await PerfilAsync(p);

        Assert.Equal(p.Id, perfil.GetProperty("id").GetGuid());
        Assert.Equal(p.Email, perfil.GetProperty("email").GetString());
        Assert.Equal("Full", perfil.GetProperty("planoEfetivo").GetString());
        Assert.Equal("trial", perfil.GetProperty("planoOrigem").GetString());
        Assert.NotEqual(JsonValueKind.Null, perfil.GetProperty("politicaAceitaEm").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, perfil.GetProperty("politicaVersao").ValueKind);
    }

    [Fact(DisplayName = "IT-80 Meu perfil: outro prestador recebe 403, Admin lê e id desconhecido retorna 404")]
    public async Task MeuPerfil_AcessoEInexistente()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();

        await (await b.Client.GetAsync($"api/Users/meu-perfil/{a.Id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await _f.ClientAdmin().GetAsync($"api/Users/meu-perfil/{a.Id}")).AssertStatusAsync(HttpStatusCode.OK);
        await (await _f.ClientAdmin().GetAsync($"api/Users/meu-perfil/{Guid.NewGuid()}")).AssertStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-81 Editar perfil atualiza nome, telefone, cidade e visibilidade")]
    public async Task EditarPerfil_AtualizaDados()
    {
        var p = await _f.CriarPrestadorAsync();
        var novoNome = $"Nome Editado {Guid.NewGuid():N}"[..24];

        var r = await p.Client.PutAsJsonAsync($"api/Users/edit/{p.Id}", new
        {
            nomeCompleto = novoNome,
            email = p.Email,
            telefone = "14988887777",
            cidade = "Jaú",
            cidadeVisivel = true,
            telefoneVisivel = true
        });

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var perfil = await PerfilAsync(p);
        Assert.Equal(novoNome, perfil.GetProperty("nomeCompleto").GetString());
        Assert.Equal("14988887777", perfil.GetProperty("telefone").GetString());
        Assert.Equal("Jaú", perfil.GetProperty("cidade").GetString());
        Assert.True(perfil.GetProperty("cidadeVisivel").GetBoolean());
        Assert.True(perfil.GetProperty("telefoneVisivel").GetBoolean());
    }

    [Fact(DisplayName = "IT-82 Editar perfil de outro prestador retorna 403 e de id desconhecido retorna 404")]
    public async Task EditarPerfil_OutroEInexistente()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var corpo = new { nomeCompleto = "Invasor", email = a.Email, telefone = "14999990000" };

        await (await b.Client.PutAsJsonAsync($"api/Users/edit/{a.Id}", corpo)).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await _f.ClientAdmin().PutAsJsonAsync($"api/Users/edit/{Guid.NewGuid()}", corpo)).AssertStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-83 Aceitar a política de privacidade registra a versão; outro usuário não pode")]
    public async Task AceitarPolitica_RegistraVersao()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();

        var ok = await a.Client.PostAsync($"api/Users/politica/aceitar/{a.Id}", null);
        var negado = await b.Client.PostAsync($"api/Users/politica/aceitar/{a.Id}", null);
        var inexistente = await _f.ClientAdmin().PostAsync($"api/Users/politica/aceitar/{Guid.NewGuid()}", null);

        await ok.AssertStatusAsync(HttpStatusCode.OK);
        Assert.False(string.IsNullOrWhiteSpace((await ok.JsonAsync()).GetProperty("versao").GetString()));
        await negado.AssertStatusAsync(HttpStatusCode.Forbidden);
        await inexistente.AssertStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-84 Cancelar e reativar a renovação automática do plano")]
    public async Task Renovacao_CancelaEReativa()
    {
        var p = await _f.CriarPrestadorAsync();

        var cancela = await p.Client.PostAsJsonAsync("api/Users/plano/renovacao",
            new { usuarioId = p.Id.ToString(), renovacaoAutomatica = false });

        await cancela.AssertStatusAsync(HttpStatusCode.OK);
        var jsonCancela = await cancela.JsonAsync();
        Assert.False(jsonCancela.GetProperty("renovacaoAutomatica").GetBoolean());
        Assert.Contains("cancelado", jsonCancela.GetProperty("message").GetString());

        var perfil = await PerfilAsync(p);
        Assert.False(perfil.GetProperty("renovacaoAutomatica").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, perfil.GetProperty("planoCanceladoEm").ValueKind);

        var reativa = await p.Client.PostAsJsonAsync("api/Users/plano/renovacao",
            new { usuarioId = p.Id.ToString(), renovacaoAutomatica = true });

        await reativa.AssertStatusAsync(HttpStatusCode.OK);
        Assert.True((await reativa.JsonAsync()).GetProperty("renovacaoAutomatica").GetBoolean());
        perfil = await PerfilAsync(p);
        Assert.True(perfil.GetProperty("renovacaoAutomatica").GetBoolean());
        Assert.Equal(JsonValueKind.Null, perfil.GetProperty("planoCanceladoEm").ValueKind);
    }

    [Fact(DisplayName = "IT-85 Renovação do plano: sem id 400, sem login 401, outro usuário 403, desconhecido 404")]
    public async Task Renovacao_Validacoes()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();

        var semId = await a.Client.PostAsJsonAsync("api/Users/plano/renovacao", new { usuarioId = "", renovacaoAutomatica = true });
        var semLogin = await _f.NovoClient().PostAsJsonAsync("api/Users/plano/renovacao",
            new { usuarioId = a.Id.ToString(), renovacaoAutomatica = true });
        var deOutro = await b.Client.PostAsJsonAsync("api/Users/plano/renovacao",
            new { usuarioId = a.Id.ToString(), renovacaoAutomatica = true });
        var desconhecido = await _f.ClientAdmin().PostAsJsonAsync("api/Users/plano/renovacao",
            new { usuarioId = Guid.NewGuid().ToString(), renovacaoAutomatica = true });

        await semId.AssertStatusAsync(HttpStatusCode.BadRequest);
        await semLogin.AssertStatusAsync(HttpStatusCode.Unauthorized);
        await deOutro.AssertStatusAsync(HttpStatusCode.Forbidden);
        await desconhecido.AssertStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-86 Solicitar plano e cancelar a solicitação")]
    public async Task SolicitarPlano_EDesistir()
    {
        var p = await _f.CriarPrestadorAsync();

        var solicita = await p.Client.PostAsJsonAsync("api/Users/plano/solicitar",
            new { usuarioId = p.Id.ToString(), plano = "Profissional" });

        await solicita.AssertStatusAsync(HttpStatusCode.OK);
        Assert.Equal("Profissional", (await PerfilAsync(p)).GetProperty("planoPendente").GetString());

        var cancela = await p.Client.PostAsJsonAsync("api/Users/plano/solicitar",
            new { usuarioId = p.Id.ToString(), plano = "Nenhum" });

        await cancela.AssertStatusAsync(HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, (await PerfilAsync(p)).GetProperty("planoPendente").ValueKind);
    }

    [Fact(DisplayName = "IT-87 Solicitar plano: sem id 400, sem login 401, outro usuário 403, desconhecido 404")]
    public async Task SolicitarPlano_Validacoes()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();

        var semId = await a.Client.PostAsJsonAsync("api/Users/plano/solicitar", new { usuarioId = "", plano = "Full" });
        var semLogin = await _f.NovoClient().PostAsJsonAsync("api/Users/plano/solicitar",
            new { usuarioId = a.Id.ToString(), plano = "Full" });
        var deOutro = await b.Client.PostAsJsonAsync("api/Users/plano/solicitar",
            new { usuarioId = a.Id.ToString(), plano = "Full" });
        var desconhecido = await _f.ClientAdmin().PostAsJsonAsync("api/Users/plano/solicitar",
            new { usuarioId = Guid.NewGuid().ToString(), plano = "Full" });

        await semId.AssertStatusAsync(HttpStatusCode.BadRequest);
        await semLogin.AssertStatusAsync(HttpStatusCode.Unauthorized);
        await deOutro.AssertStatusAsync(HttpStatusCode.Forbidden);
        await desconhecido.AssertStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "IT-88 Cadastro com plano pago fica pendente até o Admin aprovar")]
    public async Task Cadastro_ComPlano_FicaPendente()
    {
        var p = await _f.CriarPrestadorAsync(testeGratuito: false, plano: "Full");

        var perfil = await PerfilAsync(p);

        Assert.Equal("Full", perfil.GetProperty("planoPendente").GetString());
        Assert.False(perfil.GetProperty("planoAtivo").GetBoolean());
        Assert.Equal("SemPlano", perfil.GetProperty("planoEfetivo").GetString());
    }

    [Fact(DisplayName = "IT-89 Encerrar a própria conta apaga dados, fotos e avisa os clientes por e-mail")]
    public async Task EncerrarPropriaConta_ApagaTudo()
    {
        var p = await _f.CriarPrestadorAsync();
        var servico = await p.CriarServicoAsync();
        var cliente = await p.CriarClienteAsync();
        var agendamento = await p.CriarAgendamentoAsync(cliente, servico, ApiHelper.Futuro(2));
        var foto = await p.InserirFotoAsync(agendamento);
        var nomeBlob = Path.GetFileName(new Uri(foto.Url).AbsolutePath);
        var emailSecundario = $"saida_{Guid.NewGuid():N}@teste.local";
        (await _f.ClientAdmin().PostAsJsonAsync("api/Cliente/definir-email-secundario",
            new { clienteId = cliente, emailSecundario })).EnsureSuccessStatusCode();

        var r = await p.Client.DeleteAsync($"api/Users/delete/{p.Id}?motivo=politica");

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var banco = new ContextMongodb();
        Assert.False(await banco.Cliente.Find(c => c.UsuarioId == p.Id).AnyAsync());
        Assert.False(await banco.Foto.Find(f => f.PrestadorId == p.Id).AnyAsync());
        Assert.False(await banco.Agendamento.Find(a => a.UsuarioId == p.Id).AnyAsync());
        Assert.Contains(_f.Blob.Chamadas, c => c.StartsWith("DELETE") && c.Contains(nomeBlob));
        await AdminHelper.EsperarAsync(() => _f.Emails.Enviados.Any(e => e.Para == emailSecundario));

        var logs = await _f.ClientAdmin().GetFromJsonAsync<JsonElement>("api/Admin/logs?pagina=1&tamanhoPagina=100");
        Assert.Contains(logs.GetProperty("logs").EnumerateArray(),
            l => l.GetProperty("tipo").GetString() == "politica_recusada");
    }

    [Fact(DisplayName = "IT-90 Encerrar conta: outro usuário recebe 403 e id desconhecido retorna 404")]
    public async Task EncerrarConta_OutroEInexistente()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();

        await (await b.Client.DeleteAsync($"api/Users/delete/{a.Id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await _f.ClientAdmin().DeleteAsync($"api/Users/delete/{Guid.NewGuid()}")).AssertStatusAsync(HttpStatusCode.NotFound);

        var perfil = await _f.ClientAdmin().GetAsync($"api/Users/meu-perfil/{a.Id}");
        await perfil.AssertStatusAsync(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "IT-91 Encerrar a própria conta sem motivo registra o log de encerramento voluntário")]
    public async Task EncerrarPropriaConta_RegistraLog()
    {
        var p = await _f.CriarPrestadorAsync();

        var r = await p.Client.DeleteAsync($"api/Users/delete/{p.Id}");

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var logs = await _f.ClientAdmin().GetFromJsonAsync<JsonElement>("api/Admin/logs?pagina=1&tamanhoPagina=100");
        Assert.Contains(logs.GetProperty("logs").EnumerateArray(),
            l => l.GetProperty("tipo").GetString() == "conta_prestador_encerrada_usuario");
    }
}
