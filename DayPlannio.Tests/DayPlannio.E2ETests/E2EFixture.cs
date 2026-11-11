using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace DayPlannio.E2ETests;

public sealed class E2EFixture : IAsyncLifetime
{
    public string WebUrl { get; } = (Environment.GetEnvironmentVariable("DP_WEB_URL") ?? "http://localhost:5140").TrimEnd('/');
    public string ApiUrl { get; } = (Environment.GetEnvironmentVariable("DP_API_URL") ?? "http://localhost:5143").TrimEnd('/');

    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public async Task InitializeAsync()
    {
        var local = new[] { WebUrl, ApiUrl }.All(u => new Uri(u).IsLoopback);
        if (!local && Environment.GetEnvironmentVariable("DP_E2E_ALLOW_REMOTE") != "1")
            throw new InvalidOperationException(
                "DP_WEB_URL/DP_API_URL não apontam para localhost. Os testes E2E criam dados; " +
                "para rodar em outro ambiente defina DP_E2E_ALLOW_REMOTE=1.");

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Environment.GetEnvironmentVariable("HEADED") != "1",
            SlowMo = int.TryParse(Environment.GetEnvironmentVariable("SLOWMO"), out var ms) ? ms : 0
        });
    }

    public async Task DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    public async Task<IPage> NovaPaginaAsync()
    {
        var contexto = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "pt-BR",
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
        });
        contexto.SetDefaultTimeout(15_000);
        return await contexto.NewPageAsync();
    }

    public sealed record ClienteSeed(string Email, string SenhaProvisoria, string NomeCliente);

    public async Task<ClienteSeed> CriarClienteComAcessoAsync()
    {
        using var api = new HttpClient(new HttpClientHandler { UseCookies = true, CookieContainer = new CookieContainer() })
        {
            BaseAddress = new Uri(ApiUrl + "/")
        };
        var sufixo = Guid.NewGuid().ToString("N")[..10];
        const string senha = "Senha@123";

        var cadastro = await api.PostAsJsonAsync("api/Users/create", new
        {
            nomeCompleto = $"Prestador E2E {sufixo}",
            email = $"e2e_{sufixo}@teste.local",
            senha,
            confirmeSenha = senha,
            aceitouPolitica = true,
            usarTesteGratuito = true
        });
        await GarantirSucesso(cadastro, "cadastro do prestador");
        var usuarioId = (await cadastro.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var nomeCliente = $"Cliente E2E {sufixo}";
        var cliente = await api.PostAsJsonAsync("api/Cliente/create", new
        {
            usuarioId,
            nome = nomeCliente,
            telefone = "14999990000",
            endereco = "Rua do E2E, 1 - Jaú/SP"
        });
        await GarantirSucesso(cliente, "cadastro do cliente");
        var clienteId = (await cliente.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var servico = await api.PostAsJsonAsync("api/TiposServico/create",
            new { usuarioId, tipo = "Pintura", tempoEstimado = 120 });
        await GarantirSucesso(servico, "cadastro do serviço");
        var servicoId = (await servico.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var agenda = await api.PostAsJsonAsync("api/Agendamentos/create", new
        {
            usuarioId,
            clienteId,
            tipoServicoId = servicoId,
            dataHora = DateTime.UtcNow.Date.AddDays(1).AddHours(12),
            valorCobrado = 300m,
            custoMaterial = 50m
        });
        await GarantirSucesso(agenda, "agendamento");
        var agendamentoId = (await agenda.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await GarantirSucesso(await api.PutAsync($"api/Agendamentos/concluir/{agendamentoId}", null), "conclusão do agendamento");

        var acesso = await api.PostAsync($"api/Cliente/gerar-credenciais/{clienteId}", null);
        await GarantirSucesso(acesso, "geração de credenciais do cliente");
        var json = await acesso.Content.ReadFromJsonAsync<JsonElement>();

        return new ClienteSeed(
            json.GetProperty("email").GetString()!,
            json.GetProperty("senhaProvisoria").GetString()!,
            nomeCliente);
    }

    private static async Task GarantirSucesso(HttpResponseMessage r, string etapa)
    {
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Falha na massa de dados ({etapa}): {(int)r.StatusCode}. {await r.Content.ReadAsStringAsync()}");
    }
}

[CollectionDefinition(Nome)]
public sealed class E2ECollection : ICollectionFixture<E2EFixture>
{
    public const string Nome = "Navegador (Playwright)";
}

public sealed class AdminFactAttribute : FactAttribute
{
    public AdminFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DP_ADMIN_EMAIL")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DP_ADMIN_SENHA")))
            Skip = "Defina DP_ADMIN_EMAIL e DP_ADMIN_SENHA para executar este teste.";
    }
}
