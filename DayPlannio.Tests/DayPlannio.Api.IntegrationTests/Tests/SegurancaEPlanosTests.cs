using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Tests;

[Collection(ApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class SegurancaEPlanosTests
{
    private readonly ApiFactory _f;
    public SegurancaEPlanosTests(ApiFactory f) => _f = f;

    [Theory(DisplayName = "IT-26 Rotas protegidas exigem login")]
    [InlineData("api/Users")]
    [InlineData("api/Admin/logs")]
    [InlineData("api/Agendamentos/00000000-0000-0000-0000-000000000001")]
    public async Task RotasProtegidas_SemLogin_NaoPassam(string rota)
    {
        (await _f.NovoClient().GetAsync(rota)).AssertNaoAutenticado();
    }

    [Fact(DisplayName = "IT-27 Prestador B não consegue ler os clientes do prestador A")]
    public async Task Isolamento_LeituraDeOutroPrestador_Retorna403()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        await a.CriarClienteAsync("Cliente do A");

        var r = await b.Client.GetAsync($"api/Cliente/{a.Id}");

        await r.AssertStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "IT-28 Prestador B não consegue gravar dados em nome do prestador A")]
    public async Task Isolamento_EscritaEmNomeDeOutro_Retorna403()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();

        var servico = await b.Client.PostAsJsonAsync("api/TiposServico/create",
            new { usuarioId = a.Id, tipo = "Invasão", tempoEstimado = 10 });
        var cliente = await b.Client.PostAsJsonAsync("api/Cliente/create",
            new { usuarioId = a.Id, nome = "Invasor", telefone = "14999990000", endereco = "Rua X" });

        await servico.AssertStatusAsync(HttpStatusCode.Forbidden);
        await cliente.AssertStatusAsync(HttpStatusCode.Forbidden);

        var doA = await a.Client.GetFromJsonAsync<JsonElement>($"api/TiposServico/{a.Id}");
        Assert.Equal(0, doA.GetArrayLength());
    }

    [Fact(DisplayName = "IT-29 Prestador B não consegue editar nem excluir serviço do prestador A")]
    public async Task Isolamento_EditarExcluirServicoDeOutro_Retorna403()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var servicoDoA = await a.CriarServicoAsync("Do A");

        var edit = await b.Client.PutAsJsonAsync($"api/TiposServico/edit/{servicoDoA}",
            new { usuarioId = b.Id, tipo = "Hackeado", tempoEstimado = 10 });
        var del = await b.Client.DeleteAsync($"api/TiposServico/delete/{servicoDoA}");

        await edit.AssertStatusAsync(HttpStatusCode.Forbidden);
        await del.AssertStatusAsync(HttpStatusCode.Forbidden);

        var lista = await a.Client.GetFromJsonAsync<JsonElement>($"api/TiposServico/{a.Id}");
        Assert.Equal("Do A", lista.EnumerateArray().Single().GetProperty("tipo").GetString());
    }

    [Fact(DisplayName = "IT-30 Prestador comum não acessa rotas de Admin")]
    public async Task PrestadorComum_NaoAcessaAdmin()
    {
        var p = await _f.CriarPrestadorAsync();

        (await p.Client.GetAsync("api/Admin/logs")).AssertProibido();
        (await p.Client.GetAsync("api/Admin/clientes")).AssertProibido();
        (await p.Client.GetAsync("api/Users")).AssertProibido();
    }

    [Fact(DisplayName = "IT-31 X-Api-Key correta concede acesso de Admin; chave errada não")]
    public async Task ApiKey_CorretaEErrada()
    {
        var certa = await _f.ClientAdmin().GetAsync("api/Admin/logs");
        await certa.AssertStatusAsync(HttpStatusCode.OK);

        var errada = _f.NovoClient();
        errada.DefaultRequestHeaders.Add("X-Api-Key", "chave-errada");
        (await errada.GetAsync("api/Admin/logs")).AssertNaoAutenticado();
    }

    [Fact(DisplayName = "IT-32 Admin (via API key) lê dados de qualquer prestador")]
    public async Task Admin_LeDadosDeQualquerPrestador()
    {
        var p = await _f.CriarPrestadorAsync();
        await p.CriarClienteAsync("Cliente Visível ao Admin");

        var r = await _f.ClientAdmin().GetFromJsonAsync<JsonElement>($"api/Cliente/{p.Id}");

        Assert.Equal(1, r.GetArrayLength());
    }

    [Fact(DisplayName = "IT-33 Login do Admin semeado na inicialização da API")]
    public async Task Admin_SemeadoNoStartup_FazLogin()
    {
        var r = await _f.NovoClient().PostAsJsonAsync("api/Admin/login",
            new { email = ApiFactory.AdminEmail, senha = ApiFactory.AdminSenha });

        await r.AssertStatusAsync(HttpStatusCode.OK);
        Assert.Equal(ApiFactory.AdminEmail, (await r.JsonAsync()).GetProperty("email").GetString());
    }

    [Fact(DisplayName = "IT-34 Prestador comum não consegue logar como Admin")]
    public async Task PrestadorComum_NaoLogaComoAdmin()
    {
        var p = await _f.CriarPrestadorAsync();

        var r = await _f.NovoClient().PostAsJsonAsync("api/Admin/login", new { email = p.Email, senha = p.Senha });

        await r.AssertStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "IT-35 Sem assinatura ativa o acesso é bloqueado (403) até o Admin aprovar o plano")]
    public async Task SemAssinatura_Bloqueia_AteAdminAprovar()
    {
        var p = await _f.CriarPrestadorAsync(testeGratuito: false, plano: "Basico");

        var antes = await p.Client.GetAsync($"api/TiposServico/{p.Id}");
        await antes.AssertStatusAsync(HttpStatusCode.Forbidden);
        Assert.Contains("assinatura", await antes.MensagemAsync());

        var aprova = await _f.ClientAdmin().PostAsync($"api/Admin/planos/{p.Id}/aprovar", null);
        await aprova.AssertStatusAsync(HttpStatusCode.OK);

        var depois = await p.Client.GetAsync($"api/TiposServico/{p.Id}");
        await depois.AssertStatusAsync(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "IT-36 Aprovar plano quando não há plano pendente retorna 400")]
    public async Task AprovarPlano_SemPendencia_Retorna400()
    {
        var p = await _f.CriarPrestadorAsync(testeGratuito: true);

        var r = await _f.ClientAdmin().PostAsync($"api/Admin/planos/{p.Id}/aprovar", null);

        await r.AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-37 Plano Básico não libera recursos do plano Full (gerar acesso do cliente)")]
    public async Task PlanoBasico_NaoGeraAcessoDeCliente()
    {
        var p = await _f.CriarPrestadorAsync(testeGratuito: false, plano: "Basico");
        (await _f.ClientAdmin().PostAsync($"api/Admin/planos/{p.Id}/aprovar", null)).EnsureSuccessStatusCode();
        var cliente = await p.CriarClienteAsync();

        var r = await p.Client.PostAsync($"api/Cliente/gerar-credenciais/{cliente}", null);

        await r.AssertStatusAsync(HttpStatusCode.Forbidden);
        Assert.Contains("Full", await r.MensagemAsync());
    }

    [Fact(DisplayName = "IT-38 Plano Básico não libera o módulo financeiro (exige Profissional)")]
    public async Task PlanoBasico_NaoAcessaFinanceiro()
    {
        var p = await _f.CriarPrestadorAsync(testeGratuito: false, plano: "Basico");
        (await _f.ClientAdmin().PostAsync($"api/Admin/planos/{p.Id}/aprovar", null)).EnsureSuccessStatusCode();

        var r = await p.Client.GetAsync($"api/Financeiros/resumo/{p.Id}");

        await r.AssertStatusAsync(HttpStatusCode.Forbidden);
    }
}
