using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace DayPlannio.E2ETests.Tests;

[Collection(E2ECollection.Nome)]
[Trait("Categoria", "E2E")]
public class PortalClienteE2ETests
{
    private readonly E2EFixture _fx;
    public PortalClienteE2ETests(E2EFixture fx) => _fx = fx;

    private static Regex Url(string trecho) => new(Regex.Escape(trecho), RegexOptions.IgnoreCase);

    private static Regex UrlLogin => new(@"/(Account/)?Login(\?|$)", RegexOptions.IgnoreCase);

    private async Task EntrarAsync(IPage page, string email, string senha)
    {
        await page.GotoAsync($"{_fx.WebUrl}/Account/Login");
        await page.Locator("#Email").FillAsync(email);
        try { await page.Locator("input[name=Senha]").WaitForAsync(new() { Timeout = 5_000 }); }
        catch
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "diagnostico");
            Directory.CreateDirectory(dir);
            var nome = DateTime.Now.ToString("HHmmss");
            await page.ScreenshotAsync(new() { Path = Path.Combine(dir, $"login-{nome}.png"), FullPage = true });
            await File.WriteAllTextAsync(Path.Combine(dir, $"login-{nome}.html"), await page.ContentAsync());
            throw new InvalidOperationException(
                $"Campo de senha (input[name=Senha]) não encontrado em {page.Url}. Veja {dir}\\login-{nome}.png/.html. " +
                $"Inputs da página: {string.Join(", ", await page.Locator("input").EvaluateAllAsync<string[]>("els => els.map(e => e.id + '/' + e.name + '/' + e.type)"))}");
        }
        await page.Locator("input[name=Senha]").FillAsync(senha);
        await page.GetByRole(AriaRole.Button, new() { Name = "Entrar" }).ClickAsync();
    }

    private static async Task RolarAteOFimAsync(IPage page) =>
        await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");

    [Fact(DisplayName = "E2E-01 Jornada completa: login → trocar senha → aceitar política → métricas → sair → entrar com a nova senha")]
    public async Task JornadaCompletaDoCliente()
    {
        var seed = await _fx.CriarClienteComAcessoAsync();
        var page = await _fx.NovaPaginaAsync();
        const string novaSenha = "MinhaNova@123";

        await EntrarAsync(page, seed.Email, seed.SenhaProvisoria);
        await Expect(page).ToHaveURLAsync(Url("/Account/TrocarSenha"));

        await page.Locator("#senhaAtualT").FillAsync(seed.SenhaProvisoria);
        await page.Locator("#senhaNovaT").FillAsync(novaSenha);
        await page.Locator("#senhaConfirmarT").FillAsync(novaSenha);
        await page.Locator("#EmailSecundario").FillAsync("recuperacao@teste.local");
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirmar" }).ClickAsync();

        await Expect(page).ToHaveURLAsync(Url("/Account/PoliticaPrivacidade"));
        await RolarAteOFimAsync(page);
        await Expect(page.Locator("#btnConcordar")).ToBeEnabledAsync();
        await page.Locator("#btnConcordar").ClickAsync();

        await Expect(page).ToHaveURLAsync(Url("/Metricas"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Meus Indicadores" })).ToBeVisibleAsync();
        var totalServicos = page.Locator(".stat-card", new() { HasTextString = "Total de serviços" }).Locator(".stat-value");
        await Expect(totalServicos).ToHaveTextAsync("1");

        await page.GetByRole(AriaRole.Link, new() { Name = "Sair" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(UrlLogin);

        await EntrarAsync(page, seed.Email, novaSenha);
        await Expect(page).ToHaveURLAsync(Url("/Metricas"));

        await page.GetByRole(AriaRole.Link, new() { Name = "Sair" }).ClickAsync();
        await EntrarAsync(page, seed.Email, seed.SenhaProvisoria);
        await Expect(page.Locator(".alert-danger")).ToContainTextAsync("Credenciais inválidas");
    }

    [Fact(DisplayName = "E2E-02 Login com credenciais inválidas mostra erro e permanece no login")]
    public async Task LoginInvalido_MostraErro()
    {
        var page = await _fx.NovaPaginaAsync();

        await EntrarAsync(page, "ninguem@teste.local", "Errada@123");

        await Expect(page.Locator(".alert-danger")).ToContainTextAsync("Credenciais inválidas");
        await Expect(page).ToHaveURLAsync(UrlLogin);
    }

    [Fact(DisplayName = "E2E-03 Áreas restritas redirecionam para o login quando não há sessão")]
    public async Task AreasRestritas_RedirecionamParaLogin()
    {
        var page = await _fx.NovaPaginaAsync();

        foreach (var rota in new[] { "/Metricas", "/Account/Perfil", "/Admin/Painel" })
        {
            await page.GotoAsync(_fx.WebUrl + rota);
            await Expect(page).ToHaveURLAsync(UrlLogin);
        }
    }

    [Fact(DisplayName = "E2E-04 Primeiro acesso: lista de requisitos da senha reage à digitação e senha fraca é recusada")]
    public async Task TrocarSenha_RequisitosEValidacao()
    {
        var seed = await _fx.CriarClienteComAcessoAsync();
        var page = await _fx.NovaPaginaAsync();
        await EntrarAsync(page, seed.Email, seed.SenhaProvisoria);
        await Expect(page).ToHaveURLAsync(Url("/Account/TrocarSenha"));

        var nova = page.Locator("#senhaNovaT");
        await nova.FillAsync("abc");
        await Expect(page.Locator("li[data-req=minusculo]")).ToHaveClassAsync(new Regex("ok"));
        await Expect(page.Locator("li[data-req=maiusculo]")).Not.ToHaveClassAsync(new Regex("ok"));
        await nova.FillAsync("Abc123!");
        foreach (var req in new[] { "tamanho", "maiusculo", "minusculo", "numero", "especial" })
            await Expect(page.Locator($"li[data-req={req}]")).ToHaveClassAsync(new Regex("ok"));

        await page.Locator("#senhaConfirmarT").FillAsync("Outra@123");
        await Expect(page.Locator("#avisoSenhaT")).ToBeVisibleAsync();

        await page.Locator("#senhaAtualT").FillAsync(seed.SenhaProvisoria);
        await nova.FillAsync("abc");
        await page.Locator("#senhaConfirmarT").FillAsync("abc");
        await page.Locator("#EmailSecundario").FillAsync("x@teste.local");
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirmar" }).ClickAsync();
        await Expect(page.Locator(".alert-danger")).ToContainTextAsync("não atende aos requisitos");
    }

    [Fact(DisplayName = "E2E-05 Política de Privacidade: botões só habilitam após rolar até o final")]
    public async Task Politica_BotoesSoHabilitamAposRolar()
    {
        var seed = await _fx.CriarClienteComAcessoAsync();
        var page = await _fx.NovaPaginaAsync();
        const string novaSenha = "MinhaNova@123";
        await EntrarAsync(page, seed.Email, seed.SenhaProvisoria);
        await page.Locator("#senhaAtualT").FillAsync(seed.SenhaProvisoria);
        await page.Locator("#senhaNovaT").FillAsync(novaSenha);
        await page.Locator("#senhaConfirmarT").FillAsync(novaSenha);
        await page.Locator("#EmailSecundario").FillAsync("x@teste.local");
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirmar" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(Url("/Account/PoliticaPrivacidade"));

        await Expect(page.Locator("#btnConcordar")).ToBeDisabledAsync();
        await Expect(page.Locator("#btnNaoConcordar")).ToBeDisabledAsync();

        await RolarAteOFimAsync(page);

        await Expect(page.Locator("#btnConcordar")).ToBeEnabledAsync();
        await Expect(page.Locator("#btnNaoConcordar")).ToBeEnabledAsync();
    }

    [Fact(DisplayName = "E2E-06 Logout encerra a sessão: voltar às métricas exige novo login")]
    public async Task Logout_EncerraSessao()
    {
        var seed = await _fx.CriarClienteComAcessoAsync();
        var page = await _fx.NovaPaginaAsync();
        const string novaSenha = "MinhaNova@123";
        await EntrarAsync(page, seed.Email, seed.SenhaProvisoria);
        await page.Locator("#senhaAtualT").FillAsync(seed.SenhaProvisoria);
        await page.Locator("#senhaNovaT").FillAsync(novaSenha);
        await page.Locator("#senhaConfirmarT").FillAsync(novaSenha);
        await page.Locator("#EmailSecundario").FillAsync("x@teste.local");
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirmar" }).ClickAsync();
        await RolarAteOFimAsync(page);
        await page.Locator("#btnConcordar").ClickAsync();
        await Expect(page).ToHaveURLAsync(Url("/Metricas"));

        await page.GetByRole(AriaRole.Link, new() { Name = "Sair" }).ClickAsync();
        await page.GotoAsync($"{_fx.WebUrl}/Metricas");

        await Expect(page).ToHaveURLAsync(UrlLogin);
    }

    [Fact(DisplayName = "E2E-07 Navegação pública: login → 'Esqueci minha senha' e login → portfólio")]
    public async Task NavegacaoPublica_FuncionaSemLogin()
    {
        var page = await _fx.NovaPaginaAsync();
        await page.GotoAsync($"{_fx.WebUrl}/Account/Login");

        await page.GetByRole(AriaRole.Link, new() { Name = "Esqueci minha senha" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(Url("/Account/EsqueciSenha"));

        await page.GotoAsync($"{_fx.WebUrl}/Account/Login");
        await page.GetByRole(AriaRole.Link, new() { Name = "Ver portfólio de serviços" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(Url("Portfolio"));
    }

    [AdminFact(DisplayName = "E2E-08 Admin entra no portal e chega ao painel")]
    public async Task Admin_EntraNoPainel()
    {
        var page = await _fx.NovaPaginaAsync();

        await EntrarAsync(page,
            Environment.GetEnvironmentVariable("DP_ADMIN_EMAIL")!,
            Environment.GetEnvironmentVariable("DP_ADMIN_SENHA")!);

        await Expect(page).ToHaveURLAsync(Url("/Admin/Painel"));
    }
}