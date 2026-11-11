using DayPlannio.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mongo2Go;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Infra;

public sealed class ApiFactory : WebApplicationFactory<DayPlannio.Api.Controllers.AccountController>
{
    public const string ApiKey = "chave-de-teste-web-api";
    public const string AdminEmail = "admin@teste.local";
    public const string AdminSenha = "Admin@12345";

    private readonly MongoDbRunner _mongo;

    public FakeEmailService Emails { get; } = new();
    public BlobFalso Blob { get; } = new();

    public ApiFactory()
    {
        _mongo = MongoDbRunner.Start();

        Environment.SetEnvironmentVariable("MongoConnection__ConnectionString", _mongo.ConnectionString);
        Environment.SetEnvironmentVariable("MongoConnection__Database", "dayplannio_testes");
        Environment.SetEnvironmentVariable("MongoConnection__IsSSL", "false");
        Environment.SetEnvironmentVariable("WebApiKey", ApiKey);
        Environment.SetEnvironmentVariable("AdminSettings__Email", AdminEmail);
        Environment.SetEnvironmentVariable("AdminSettings__Senha", AdminSenha);
        Environment.SetEnvironmentVariable("AdminSettings__Nome", "Admin dos Testes");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<EmailService>();
            services.AddSingleton<EmailService>(Emails);

            services.RemoveAll<AzureBlobStorageService>();
            services.AddSingleton(Blob.CriarServico());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _mongo.Dispose();
    }
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Nome = "API + MongoDB";
}
