using System.Net;
using System.Text.RegularExpressions;
using DayPlannio.Api.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DayPlannio.Web.IntegrationTests.Infra;

public sealed class WebFactory : WebApplicationFactory<DayPlannio.Web.Controllers.HomeController>
{
    private readonly ApiFactory _api;

    public WebFactory(ApiFactory api)
    {
        _api = api;
        Environment.SetEnvironmentVariable("Api__Key", ApiFactory.ApiKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddHttpClient("Api")
                .ConfigurePrimaryHttpMessageHandler(() => _api.Server.CreateHandler());
        });
    }

    public HttpClient NovoNavegador() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });
}

public static class WebHelper
{
    private static readonly Regex TokenRegex = new(
        "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled);

    public static string Texto(string html) => WebUtility.HtmlDecode(html);

    public static async Task<string> TokenAsync(this HttpClient navegador, string url)
    {
        var html = await navegador.GetStringAsync(url);
        var m = TokenRegex.Match(html);
        if (!m.Success) throw new InvalidOperationException($"Token antiforgery não encontrado em {url}.");
        return WebUtility.HtmlDecode(m.Groups[1].Value);
    }

    public static async Task<HttpResponseMessage> EnviarFormAsync(
        this HttpClient navegador, string url, Dictionary<string, string> campos)
    {
        var token = await navegador.TokenAsync(url);
        campos["__RequestVerificationToken"] = token;
        return await navegador.PostAsync(url, new FormUrlEncodedContent(campos));
    }

    public static string Destino(this HttpResponseMessage r) => r.Headers.Location?.ToString() ?? string.Empty;
}
