using System.Net;
using System.Net.Http.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Tests;

[Collection(ApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class AcessoClienteTests
{
    private readonly ApiFactory _f;
    public AcessoClienteTests(ApiFactory f) => _f = f;

    private async Task<(Prestador prestador, ClienteCriado acesso)> NovoClienteComAcessoAsync()
    {
        var p = await _f.CriarPrestadorAsync();
        var id = await p.CriarClienteAsync("Cliente Portal");
        return (p, await p.GerarAcessoClienteAsync(id));
    }

    private Task<HttpResponseMessage> LoginCliente(string email, string senha) =>
        _f.NovoClient().PostAsJsonAsync("api/Cliente/login", new { email, senha });

    [Fact(DisplayName = "IT-39 Gerar acesso devolve e-mail e senha provisória; o login funciona e indica primeiro acesso")]
    public async Task GerarAcesso_LoginIndicaPrimeiroAcesso()
    {
        var (p, acesso) = await NovoClienteComAcessoAsync();

        Assert.False(string.IsNullOrWhiteSpace(acesso.Email));
        Assert.Equal(10, acesso.SenhaProvisoria.Length);

        var login = await LoginCliente(acesso.Email, acesso.SenhaProvisoria);
        await login.AssertStatusAsync(HttpStatusCode.OK);
        var json = await login.JsonAsync();

        Assert.Equal(acesso.Id, json.GetProperty("clienteId").GetGuid());
        Assert.Equal(p.Id, json.GetProperty("usuarioId").GetGuid());
        Assert.True(json.GetProperty("primeiroAcesso").GetBoolean());
    }

    [Fact(DisplayName = "IT-40 A senha do cliente é guardada como hash (nunca em texto puro)")]
    public async Task SenhaDoCliente_NaoFicaEmTextoPuro()
    {
        var (p, acesso) = await NovoClienteComAcessoAsync();

        var lista = await _f.ClientAdmin().GetStringAsync($"api/Cliente/{p.Id}");

        Assert.DoesNotContain(acesso.SenhaProvisoria, lista);
    }

    [Fact(DisplayName = "IT-51 [DEFEITO] A listagem de clientes não deve expor SenhaHash nem CodigoRecuperacao")]
    [Trait("Categoria", "DefeitoConhecido")]
    public async Task ListagemDeClientes_NaoExpoeCamposSensiveis()
    {
        var (p, _) = await NovoClienteComAcessoAsync();

        var lista = await _f.ClientAdmin().GetStringAsync($"api/Cliente/{p.Id}");

        Assert.DoesNotContain("senhaHash", lista, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("codigoRecuperacao", lista, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "IT-41 Login do cliente com senha errada ou e-mail inexistente retorna 401")]
    public async Task LoginCliente_Invalido_Retorna401()
    {
        var (_, acesso) = await NovoClienteComAcessoAsync();

        await (await LoginCliente(acesso.Email, "Errada@123")).AssertStatusAsync(HttpStatusCode.Unauthorized);
        await (await LoginCliente("ninguem@teste.local", "Qualquer@123")).AssertStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "IT-42 Login do cliente sem e-mail ou senha retorna 400")]
    public async Task LoginCliente_CamposVazios_Retorna400()
    {
        await (await LoginCliente("", "")).AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-43 Primeiro acesso: trocar a senha invalida a provisória e zera o 'primeiroAcesso'")]
    public async Task TrocarSenha_PrimeiroAcesso()
    {
        var (_, acesso) = await NovoClienteComAcessoAsync();
        const string nova = "MinhaNova@123";

        var troca = await _f.ClientAdmin().PostAsJsonAsync("api/Cliente/trocarsenha",
            new { email = acesso.Email, senhaAtual = acesso.SenhaProvisoria, senhaNova = nova });
        await troca.AssertStatusAsync(HttpStatusCode.OK);

        await (await LoginCliente(acesso.Email, acesso.SenhaProvisoria)).AssertStatusAsync(HttpStatusCode.Unauthorized);

        var login = await LoginCliente(acesso.Email, nova);
        await login.AssertStatusAsync(HttpStatusCode.OK);
        Assert.False((await login.JsonAsync()).GetProperty("primeiroAcesso").GetBoolean());
    }

    [Theory(DisplayName = "IT-44 Trocar senha: senha fraca é recusada")]
    [InlineData("abc")]
    [InlineData("semmaiuscula1!")]
    [InlineData("SEMMINUSCULA1!")]
    [InlineData("SemNumero!!")]
    [InlineData("SemEspecial123")]
    public async Task TrocarSenha_SenhaFraca_Retorna400(string fraca)
    {
        var (_, acesso) = await NovoClienteComAcessoAsync();

        var r = await _f.ClientAdmin().PostAsJsonAsync("api/Cliente/trocarsenha",
            new { email = acesso.Email, senhaAtual = acesso.SenhaProvisoria, senhaNova = fraca });

        await r.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("requisitos", await r.MensagemAsync());
    }

    [Fact(DisplayName = "IT-45 Trocar senha: informar a senha atual errada retorna 401")]
    public async Task TrocarSenha_SenhaAtualErrada_Retorna401()
    {
        var (_, acesso) = await NovoClienteComAcessoAsync();

        var r = await _f.ClientAdmin().PostAsJsonAsync("api/Cliente/trocarsenha",
            new { email = acesso.Email, senhaAtual = "Errada@123", senhaNova = "Nova@12345" });

        await r.AssertStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "IT-46 Recuperação do cliente: código vai para o e-mail secundário → redefinir → login")]
    public async Task RecuperacaoCliente_FluxoCompleto()
    {
        var (_, acesso) = await NovoClienteComAcessoAsync();
        var admin = _f.ClientAdmin();
        const string emailRecuperacao = "recuperacao_cliente@teste.local";
        const string nova = "Redefinida@789";

        var sec = await admin.PostAsJsonAsync("api/Cliente/definir-email-secundario",
            new { clienteId = acesso.Id, emailSecundario = emailRecuperacao });
        await sec.AssertStatusAsync(HttpStatusCode.OK);

        var anon = _f.NovoClient();
        (await anon.PostAsJsonAsync("api/Cliente/forgot-password", new { email = acesso.Email })).EnsureSuccessStatusCode();

        var codigo = _f.Emails.UltimoCodigoPara(emailRecuperacao);
        Assert.NotNull(codigo);

        var reset = await anon.PostAsJsonAsync("api/Cliente/reset-password",
            new { email = acesso.Email, code = codigo, senhaNova = nova });
        await reset.AssertStatusAsync(HttpStatusCode.OK);

        await (await LoginCliente(acesso.Email, nova)).AssertStatusAsync(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "IT-47 Recuperação do cliente sem e-mail secundário cadastrado retorna 400")]
    public async Task RecuperacaoCliente_SemEmailSecundario_Retorna400()
    {
        var (_, acesso) = await NovoClienteComAcessoAsync();

        var r = await _f.NovoClient().PostAsJsonAsync("api/Cliente/forgot-password", new { email = acesso.Email });

        await r.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("Nenhum e-mail de recuperação", await r.MensagemAsync());
    }

    [Fact(DisplayName = "IT-48 Recuperação do cliente para e-mail desconhecido responde 200 sem revelar nada")]
    public async Task RecuperacaoCliente_EmailDesconhecido_NaoVazaInformacao()
    {
        var r = await _f.NovoClient().PostAsJsonAsync("api/Cliente/forgot-password",
            new { email = $"ninguem_{Guid.NewGuid():N}@teste.local" });

        await r.AssertStatusAsync(HttpStatusCode.OK);
        Assert.Contains("Se o e-mail estiver cadastrado", await r.MensagemAsync());
    }

    [Fact(DisplayName = "IT-49 Recuperação do cliente: 5 códigos errados bloqueiam o código")]
    public async Task RecuperacaoCliente_CincoErros_Bloqueia()
    {
        var (_, acesso) = await NovoClienteComAcessoAsync();
        await _f.ClientAdmin().PostAsJsonAsync("api/Cliente/definir-email-secundario",
            new { clienteId = acesso.Id, emailSecundario = "bloqueio@teste.local" });
        var anon = _f.NovoClient();
        await anon.PostAsJsonAsync("api/Cliente/forgot-password", new { email = acesso.Email });
        var certo = _f.Emails.UltimoCodigoPara("bloqueio@teste.local")!;
        var errado = certo == "000000" ? "111111" : "000000";

        HttpResponseMessage? ultima = null;
        for (var i = 0; i < 5; i++)
            ultima = await anon.PostAsJsonAsync("api/Cliente/reset-password",
                new { email = acesso.Email, code = errado, senhaNova = "Nova@12345" });
        Assert.Contains("tentativas excedido", await ultima!.MensagemAsync());

        var comCodigoCerto = await anon.PostAsJsonAsync("api/Cliente/reset-password",
            new { email = acesso.Email, code = certo, senhaNova = "Nova@12345" });
        await comCodigoCerto.AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-50 Cliente de um prestador sem plano Full não recebe acesso ao portal")]
    public async Task GerarAcesso_PrestadorSemFull_Retorna403()
    {
        var p = await _f.CriarPrestadorAsync(testeGratuito: false, plano: "Profissional");
        (await _f.ClientAdmin().PostAsync($"api/Admin/planos/{p.Id}/aprovar", null)).EnsureSuccessStatusCode();
        var cliente = await p.CriarClienteAsync();

        var r = await p.Client.PostAsync($"api/Cliente/gerar-credenciais/{cliente}", null);

        await r.AssertStatusAsync(HttpStatusCode.Forbidden);
    }
}
