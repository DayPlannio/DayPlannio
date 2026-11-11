using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Azure.Core.Pipeline;
using Azure.Storage;
using Azure.Storage.Blobs;
using DayPlannio.Api.Services;

namespace DayPlannio.Api.IntegrationTests.Infra;

public sealed class BlobFalso : HttpMessageHandler
{
    public ConcurrentQueue<string> Chamadas { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Chamadas.Enqueue($"{request.Method} {request.RequestUri}");

        var resposta = new HttpResponseMessage(request.Method == HttpMethod.Delete ? HttpStatusCode.Accepted : HttpStatusCode.Created)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };
        resposta.Headers.TryAddWithoutValidation("ETag", "\"0x8DC0000000000\"");
        resposta.Headers.TryAddWithoutValidation("x-ms-request-id", Guid.NewGuid().ToString());
        resposta.Headers.TryAddWithoutValidation("x-ms-version", "2024-11-04");
        resposta.Content.Headers.TryAddWithoutValidation("Last-Modified", "Thu, 05 Nov 2026 12:00:00 GMT");
        return Task.FromResult(resposta);
    }

    public AzureBlobStorageService CriarServico()
    {
        const string conta = "contateste";
        var chave = Convert.ToBase64String(new byte[32]);
        var conexao = $"DefaultEndpointsProtocol=https;AccountName={conta};AccountKey={chave};EndpointSuffix=core.windows.net";

        var opcoes = new BlobClientOptions
        {
            Transport = new HttpClientTransport(new HttpClient(this))
        };
        opcoes.Retry.MaxRetries = 0;

        var container = new BlobContainerClient(conexao, "fotos", opcoes);
        var credencial = new StorageSharedKeyCredential(conta, chave);

        var servico = (AzureBlobStorageService)RuntimeHelpers.GetUninitializedObject(typeof(AzureBlobStorageService));
        var tipo = typeof(AzureBlobStorageService);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        tipo.GetField("_container", flags)!.SetValue(servico, container);
        tipo.GetField("_keyCredential", flags)!.SetValue(servico, credencial);
        return servico;
    }
}
