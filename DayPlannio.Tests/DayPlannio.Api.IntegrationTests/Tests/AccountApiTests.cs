using System.Net;
using System.Net.Http.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using DayPlannio.Api.Models;
using MongoDB.Driver;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Tests;

[Collection(ApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class AccountApiTests
{
    private readonly ApiFactory _f;
    public AccountApiTests(ApiFactory f) => _f = f;

    private async Task<string> SolicitarCodigoAsync(Prestador p)
    {
        (await _f.NovoClient().PostAsJsonAsync("api/Account/forgot-password", new { email = p.Email })).EnsureSuccessStatusCode();
        return _f.Emails.UltimoCodigoPara(p.Email)!;
    }

    private static Task ExpirarCodigoAsync(Guid id) =>
        new ContextMongodb().ApplicationUsers.UpdateOneAsync(
            u => u.Id == id,
            Builders<ApplicationUser>.Update.Set(u => u.CodigoRecuperacaoExpira, (DateTime?)DateTime.UtcNow.AddMinutes(-1)));

    [Fact(DisplayName = "IT-144 Recuperação de senha: e-mail vazio retorna 400 e e-mail desconhecido não revela nada")]
    public async Task Forgot_Validacoes()
    {
        var vazio = await _f.NovoClient().PostAsJsonAsync("api/Account/forgot-password", new { email = "" });
        var email = $"ninguem_{Guid.NewGuid():N}@teste.local";
        var desconhecido = await _f.NovoClient().PostAsJsonAsync("api/Account/forgot-password", new { email });

        await vazio.AssertStatusAsync(HttpStatusCode.BadRequest);
        await desconhecido.AssertStatusAsync(HttpStatusCode.OK);
        Assert.DoesNotContain(_f.Emails.Enviados, e => e.Para == email);
    }

    [Fact(DisplayName = "IT-145 Pedir o código duas vezes reenvia o mesmo código")]
    public async Task Forgot_ReenviaOMesmoCodigo()
    {
        var p = await _f.CriarPrestadorAsync();
        var primeiro = await SolicitarCodigoAsync(p);

        var segundo = await _f.NovoClient().PostAsJsonAsync("api/Account/forgot-password", new { email = p.Email });

        await segundo.AssertStatusAsync(HttpStatusCode.OK);
        Assert.Contains("Reenviamos", await segundo.MensagemAsync());
        Assert.Equal(primeiro, _f.Emails.UltimoCodigoPara(p.Email));
    }

    [Fact(DisplayName = "IT-146 Redefinir senha: nova senha vazia, igual à atual ou fraca é recusada")]
    public async Task Reset_SenhaInvalida_Retorna400()
    {
        var p = await _f.CriarPrestadorAsync();
        var codigo = await SolicitarCodigoAsync(p);
        var anon = _f.NovoClient();

        var vazia = await anon.PostAsJsonAsync("api/Account/reset-password", new { email = p.Email, code = codigo, newPassword = "", confirmPassword = "" });
        var igual = await anon.PostAsJsonAsync("api/Account/reset-password", new { email = p.Email, code = codigo, newPassword = p.Senha, confirmPassword = p.Senha });
        var fraca = await anon.PostAsJsonAsync("api/Account/reset-password", new { email = p.Email, code = codigo, newPassword = "abc", confirmPassword = "abc" });

        await vazia.AssertStatusAsync(HttpStatusCode.BadRequest);
        await igual.AssertStatusAsync(HttpStatusCode.BadRequest);
        await fraca.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("igual", await igual.MensagemAsync());
        Assert.Contains("requisitos", await fraca.MensagemAsync());
    }

    [Fact(DisplayName = "IT-147 Redefinir senha com e-mail desconhecido ou código errado retorna 400")]
    public async Task Reset_EmailOuCodigoInvalido_Retorna400()
    {
        var p = await _f.CriarPrestadorAsync();
        await SolicitarCodigoAsync(p);
        var anon = _f.NovoClient();

        var desconhecido = await anon.PostAsJsonAsync("api/Account/reset-password",
            new { email = $"x_{Guid.NewGuid():N}@teste.local", code = "123456", newPassword = "Nova@1234", confirmPassword = "Nova@1234" });
        var codigoErrado = await anon.PostAsJsonAsync("api/Account/reset-password",
            new { email = p.Email, code = "abcdef", newPassword = "Nova@1234", confirmPassword = "Nova@1234" });

        await desconhecido.AssertStatusAsync(HttpStatusCode.BadRequest);
        await codigoErrado.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("inválido", await codigoErrado.MensagemAsync());
    }

    [Fact(DisplayName = "IT-148 Código de recuperação expirado é recusado na verificação e na redefinição")]
    public async Task CodigoExpirado_ERecusado()
    {
        var p = await _f.CriarPrestadorAsync();
        var codigo = await SolicitarCodigoAsync(p);
        await ExpirarCodigoAsync(p.Id);
        var anon = _f.NovoClient();

        var verificar = await anon.PostAsJsonAsync("api/Account/verify-code", new { email = p.Email, code = codigo, newPassword = "x", confirmPassword = "x" });
        await ExpirarCodigoAsync(p.Id);
        var redefinir = await anon.PostAsJsonAsync("api/Account/reset-password",
            new { email = p.Email, code = codigo, newPassword = "Nova@1234", confirmPassword = "Nova@1234" });

        await verificar.AssertStatusAsync(HttpStatusCode.BadRequest);
        await redefinir.AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-149 Verificar código: certo aprova, e-mail desconhecido recusa")]
    public async Task VerifyCode_CertoEDesconhecido()
    {
        var p = await _f.CriarPrestadorAsync();
        var codigo = await SolicitarCodigoAsync(p);
        var anon = _f.NovoClient();

        var certo = await anon.PostAsJsonAsync("api/Account/verify-code", new { email = p.Email, code = codigo, newPassword = "x", confirmPassword = "x" });
        var desconhecido = await anon.PostAsJsonAsync("api/Account/verify-code",
            new { email = $"x_{Guid.NewGuid():N}@teste.local", code = codigo, newPassword = "x", confirmPassword = "x" });

        await certo.AssertStatusAsync(HttpStatusCode.OK);
        await desconhecido.AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-150 Cinco códigos errados na redefinição bloqueiam a conta de recuperação")]
    public async Task Reset_CincoErros_Bloqueia()
    {
        var p = await _f.CriarPrestadorAsync();
        var codigo = await SolicitarCodigoAsync(p);
        var errado = codigo == "000000" ? "111111" : "000000";
        var anon = _f.NovoClient();

        HttpResponseMessage? ultima = null;
        for (var i = 0; i < 5; i++)
            ultima = await anon.PostAsJsonAsync("api/Account/reset-password",
                new { email = p.Email, code = errado, newPassword = "Nova@1234", confirmPassword = "Nova@1234" });

        Assert.Contains("tentativas excedido", await ultima!.MensagemAsync());
        var certo = await anon.PostAsJsonAsync("api/Account/reset-password",
            new { email = p.Email, code = codigo, newPassword = "Nova@1234", confirmPassword = "Nova@1234" });
        await certo.AssertStatusAsync(HttpStatusCode.BadRequest);
    }
}
