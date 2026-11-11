using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DayPlannio.Api.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Tests;

[Collection(ApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class AutenticacaoPrestadorTests
{
    private readonly ApiFactory _f;
    public AutenticacaoPrestadorTests(ApiFactory f) => _f = f;

    private static object NovoUsuario(string email, string senha = ApiHelper.SenhaPadrao,
        string? confirme = null, bool politica = true, bool teste = true, string? plano = null) => new
    {
        nomeCompleto = $"Teste {Guid.NewGuid():N}"[..14],
        email,
        senha,
        confirmeSenha = confirme ?? senha,
        aceitouPolitica = politica,
        usarTesteGratuito = teste,
        plano
    };

    [Fact(DisplayName = "IT-01 Cadastro com teste gratuito grava o prestador no banco com plano Full")]
    public async Task Cadastro_TesteGratuito_PersisteNoBancoComPlanoFull()
    {
        var p = await _f.CriarPrestadorAsync(testeGratuito: true);

        var lista = await _f.ClientAdmin().GetFromJsonAsync<System.Text.Json.JsonElement>("api/Users");
        var salvo = lista.EnumerateArray().Single(u => u.GetProperty("email").GetString() == p.Email);

        Assert.Equal("Full", salvo.GetProperty("plano").GetString());
        Assert.True(salvo.GetProperty("planoAtivo").GetBoolean());
    }

    [Fact(DisplayName = "IT-02 Cadastro sem aceitar a Política de Privacidade é recusado")]
    public async Task Cadastro_SemAceitarPolitica_Retorna400()
    {
        var r = await _f.NovoClient().PostAsJsonAsync("api/Users/create",
            NovoUsuario($"a_{Guid.NewGuid():N}@teste.local", politica: false));

        await r.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("Política de Privacidade", await r.MensagemAsync());
    }

    [Fact(DisplayName = "IT-03 Cadastro com senhas diferentes é recusado")]
    public async Task Cadastro_SenhasDiferentes_Retorna400()
    {
        var r = await _f.NovoClient().PostAsJsonAsync("api/Users/create",
            NovoUsuario($"b_{Guid.NewGuid():N}@teste.local", confirme: "Outra@123"));

        await r.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("não coincidem", await r.MensagemAsync());
    }

    [Fact(DisplayName = "IT-04 Cadastro com e-mail já existente é recusado")]
    public async Task Cadastro_EmailDuplicado_Retorna400()
    {
        var p = await _f.CriarPrestadorAsync();

        var r = await _f.NovoClient().PostAsJsonAsync("api/Users/create", NovoUsuario(p.Email));

        await r.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("já está cadastrado", await r.MensagemAsync());
    }

    [Fact(DisplayName = "IT-05 Cadastro sem plano e sem teste gratuito é recusado")]
    public async Task Cadastro_SemPlanoENemTeste_Retorna400()
    {
        var r = await _f.NovoClient().PostAsJsonAsync("api/Users/create",
            NovoUsuario($"c_{Guid.NewGuid():N}@teste.local", teste: false, plano: null));

        await r.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("Escolha um plano", await r.MensagemAsync());
    }

    [Fact(DisplayName = "IT-06 Senha fora da regra do Identity é recusada")]
    public async Task Cadastro_SenhaFraca_Retorna400()
    {
        var r = await _f.NovoClient().PostAsJsonAsync("api/Users/create",
            NovoUsuario($"d_{Guid.NewGuid():N}@teste.local", senha: "abc"));

        await r.AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-07 Login válido cria sessão e libera rota protegida")]
    public async Task Login_Valido_LiberaRotaProtegida()
    {
        var p = await _f.CriarPrestadorAsync();
        var client = _f.NovoClient();

        (await client.GetAsync($"api/TiposServico/{p.Id}")).AssertNaoAutenticado();

        var login = await client.PostAsJsonAsync("api/Account/login", new { email = p.Email, senha = p.Senha });
        await login.AssertStatusAsync(HttpStatusCode.OK);
        Assert.Equal(p.Id, (await login.JsonAsync()).GetProperty("userId").GetGuid());

        var rota = await client.GetAsync($"api/TiposServico/{p.Id}");
        await rota.AssertStatusAsync(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "IT-08 Login com senha errada retorna 401 sem vazar se o e-mail existe")]
    public async Task Login_SenhaErrada_Retorna401()
    {
        var p = await _f.CriarPrestadorAsync();

        var errada = await _f.NovoClient().PostAsJsonAsync("api/Account/login", new { email = p.Email, senha = "Errada@999" });
        var inexistente = await _f.NovoClient().PostAsJsonAsync("api/Account/login",
            new { email = $"nao_{Guid.NewGuid():N}@teste.local", senha = "Qualquer@123" });

        await errada.AssertStatusAsync(HttpStatusCode.Unauthorized);
        await inexistente.AssertStatusAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(await errada.MensagemAsync(), await inexistente.MensagemAsync());
    }

    [Fact(DisplayName = "IT-09 Após 5 senhas erradas a conta é bloqueada (429), mesmo com a senha certa")]
    public async Task Login_MuitasFalhas_BloqueiaConta()
    {
        var p = await _f.CriarPrestadorAsync();
        var client = _f.NovoClient();

        for (var i = 0; i < 5; i++)
        {
            var r = await client.PostAsJsonAsync("api/Account/login", new { email = p.Email, senha = $"Errada@{i}99" });
            Assert.True(r.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.TooManyRequests);
        }

        var certa = await client.PostAsJsonAsync("api/Account/login", new { email = p.Email, senha = p.Senha });
        await certa.AssertStatusAsync(HttpStatusCode.TooManyRequests);
    }

    [Fact(DisplayName = "IT-10 Logout encerra a sessão")]
    public async Task Logout_EncerraSessao()
    {
        var p = await _f.CriarPrestadorAsync();

        (await p.Client.PostAsync("api/Account/logout", null)).EnsureSuccessStatusCode();

        (await p.Client.GetAsync($"api/TiposServico/{p.Id}")).AssertNaoAutenticado();
    }

    [Fact(DisplayName = "IT-11 Recuperação de senha: e-mail com código → redefinir → login com a nova senha")]
    public async Task RecuperacaoSenha_FluxoCompleto()
    {
        var p = await _f.CriarPrestadorAsync();
        var anon = _f.NovoClient();

        (await anon.PostAsJsonAsync("api/Account/forgot-password", new { email = p.Email })).EnsureSuccessStatusCode();

        var codigo = _f.Emails.UltimoCodigoPara(p.Email);
        Assert.NotNull(codigo);
        Assert.Matches(new Regex(@"^\d{6}$"), codigo!);

        const string nova = "NovaSenha@456";
        var reset = await anon.PostAsJsonAsync("api/Account/reset-password", new
        {
            email = p.Email,
            code = codigo,
            newPassword = nova,
            confirmPassword = nova
        });
        await reset.AssertStatusAsync(HttpStatusCode.OK);

        var antiga = await _f.NovoClient().PostAsJsonAsync("api/Account/login", new { email = p.Email, senha = p.Senha });
        await antiga.AssertStatusAsync(HttpStatusCode.Unauthorized);

        var novaLogin = await _f.NovoClient().PostAsJsonAsync("api/Account/login", new { email = p.Email, senha = nova });
        await novaLogin.AssertStatusAsync(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "IT-12 Recuperação de senha: 5 códigos errados bloqueiam o código válido")]
    public async Task RecuperacaoSenha_CincoErros_BloqueiaCodigo()
    {
        var p = await _f.CriarPrestadorAsync();
        var anon = _f.NovoClient();
        await anon.PostAsJsonAsync("api/Account/forgot-password", new { email = p.Email });
        var codigoCerto = _f.Emails.UltimoCodigoPara(p.Email)!;
        var codigoErrado = codigoCerto == "000000" ? "111111" : "000000";

        HttpResponseMessage? ultima = null;
        for (var i = 0; i < 5; i++)
            ultima = await anon.PostAsJsonAsync("api/Account/verify-code",
                new { email = p.Email, code = codigoErrado, newPassword = "x", confirmPassword = "x" });

        Assert.Contains("tentativas excedido", await ultima!.MensagemAsync());

        var comCodigoCerto = await anon.PostAsJsonAsync("api/Account/verify-code",
            new { email = p.Email, code = codigoCerto, newPassword = "x", confirmPassword = "x" });
        await comCodigoCerto.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("tentativas excedido", await comCodigoCerto.MensagemAsync());
    }
}
