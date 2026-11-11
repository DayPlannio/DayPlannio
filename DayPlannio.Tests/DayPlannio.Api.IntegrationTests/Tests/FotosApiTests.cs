using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DayPlannio.Api.IntegrationTests.Infra;
using DayPlannio.Api.Models;
using MongoDB.Driver;
using Xunit;

namespace DayPlannio.Api.IntegrationTests.Tests;

[Collection(ApiCollection.Nome)]
[Trait("Categoria", "Integracao")]
public class FotosApiTests
{
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };

    private readonly ApiFactory _f;
    public FotosApiTests(ApiFactory f) => _f = f;

    private static MultipartFormDataContent Formulario(
        Guid agendamento, Guid prestador, byte[]? arquivo, string contentType = "image/png", bool publica = true)
    {
        var form = new MultipartFormDataContent();
        if (arquivo != null)
        {
            var conteudo = new ByteArrayContent(arquivo);
            conteudo.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(conteudo, "arquivo", "foto.png");
        }
        form.Add(new StringContent(agendamento.ToString()), "agendamentoId");
        form.Add(new StringContent(prestador.ToString()), "prestadorId");
        form.Add(new StringContent("Antes e depois"), "descricao");
        form.Add(new StringContent(publica ? "true" : "false"), "publica");
        return form;
    }

    private static async Task<(Guid cliente, Guid servico, Guid agendamento)> CenarioAsync(
        Prestador p, string servico = "Limpeza", int dias = 2)
    {
        var cliente = await p.CriarClienteAsync("Cliente Fotografado");
        var tipo = await p.CriarServicoAsync(servico);
        var agendamento = await p.CriarAgendamentoAsync(cliente, tipo, ApiHelper.Futuro(dias));
        return (cliente, tipo, agendamento);
    }

    private static JsonElement? AcharPorId(JsonElement lista, Guid id) =>
        lista.EnumerateArray().Cast<JsonElement?>()
            .FirstOrDefault(x => x!.Value.GetProperty("id").GetGuid() == id);

    [Fact(DisplayName = "IT-118 Upload de foto grava no armazenamento e no banco e deixa a foto em análise")]
    public async Task Upload_Valido_GravaEmAnalise()
    {
        var p = await _f.CriarPrestadorAsync();
        var (_, _, agendamento) = await CenarioAsync(p);

        var r = await p.Client.PostAsync("api/Fotos/upload", Formulario(agendamento, p.Id, Png));

        await r.AssertStatusAsync(HttpStatusCode.OK);
        var json = await r.JsonAsync();
        var id = json.GetProperty("id").GetGuid();
        Assert.StartsWith("https://contateste.blob.core.windows.net/fotos/", json.GetProperty("url").GetString());
        Assert.Contains(_f.Blob.Chamadas, c => c.StartsWith("PUT") && c.Contains("foto.png"));

        var foto = await new ContextMongodb().Foto.Find(f => f.Id == id).SingleAsync();
        Assert.False(foto.Aprovado);
        Assert.True(foto.Publica);
        Assert.Equal("Antes e depois", foto.Descricao);

        var doAgendamento = await p.Client.GetFromJsonAsync<JsonElement>($"api/Fotos/agendamento/{agendamento}");
        Assert.NotNull(AcharPorId(doAgendamento, id));

        var notificacoes = await p.Client.GetFromJsonAsync<JsonElement>($"api/Admin/notificacoes/{p.Id}");
        Assert.Contains(notificacoes.EnumerateArray(), n => n.GetProperty("tipo").GetString() == "foto_enviada");
    }

    [Fact(DisplayName = "IT-119 Upload sem arquivo, vazio ou que não é imagem é recusado com 400")]
    public async Task Upload_ArquivoInvalido_Retorna400()
    {
        var p = await _f.CriarPrestadorAsync();
        var (_, _, agendamento) = await CenarioAsync(p);

        var semArquivo = await p.Client.PostAsync("api/Fotos/upload", Formulario(agendamento, p.Id, null));
        var vazio = await p.Client.PostAsync("api/Fotos/upload", Formulario(agendamento, p.Id, Array.Empty<byte>()));
        var naoImagem = await p.Client.PostAsync("api/Fotos/upload", Formulario(agendamento, p.Id, Png, "text/plain"));

        await semArquivo.AssertStatusAsync(HttpStatusCode.BadRequest);
        await vazio.AssertStatusAsync(HttpStatusCode.BadRequest);
        await naoImagem.AssertStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("imagens", await naoImagem.MensagemAsync());
    }

    [Fact(DisplayName = "IT-120 Upload: sem login 401, em nome de outro prestador 403, agendamento alheio 400")]
    public async Task Upload_Autorizacao()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var (_, _, agendamentoDeA) = await CenarioAsync(a);

        var semLogin = await _f.NovoClient().PostAsync("api/Fotos/upload", Formulario(agendamentoDeA, a.Id, Png));
        var emNomeDeOutro = await b.Client.PostAsync("api/Fotos/upload", Formulario(agendamentoDeA, a.Id, Png));
        var agendamentoAlheio = await b.Client.PostAsync("api/Fotos/upload", Formulario(agendamentoDeA, b.Id, Png));

        semLogin.AssertNaoAutenticado();
        await emNomeDeOutro.AssertStatusAsync(HttpStatusCode.Forbidden);
        await agendamentoAlheio.AssertStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "IT-121 Upload de fotos exige o plano Full")]
    public async Task Upload_PlanoBasico_Retorna403()
    {
        var p = await _f.CriarPrestadorComPlanoAsync("Basico");
        var (_, _, agendamento) = await CenarioAsync(p);

        var r = await p.Client.PostAsync("api/Fotos/upload", Formulario(agendamento, p.Id, Png));

        await r.AssertStatusAsync(HttpStatusCode.Forbidden);
        Assert.Contains("Full", await r.MensagemAsync());
    }

    [Fact(DisplayName = "IT-122 Fotos do agendamento: outro prestador recebe 403; agendamento sem fotos devolve lista vazia")]
    public async Task FotosDoAgendamento_Acesso()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var (_, _, agendamento) = await CenarioAsync(a);
        await a.InserirFotoAsync(agendamento);

        await (await b.Client.GetAsync($"api/Fotos/agendamento/{agendamento}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        var vazia = await a.Client.GetFromJsonAsync<JsonElement>($"api/Fotos/agendamento/{Guid.NewGuid()}");
        Assert.Equal(0, vazia.GetArrayLength());
    }

    [Fact(DisplayName = "IT-123 Portfólio do prestador mostra só fotos públicas e aprovadas, com serviço e cliente")]
    public async Task Portfolio_SoPublicasEAprovadas()
    {
        var p = await _f.CriarPrestadorAsync();
        var (_, _, agendamento) = await CenarioAsync(p, "Pintura");
        var aprovada = await p.InserirFotoAsync(agendamento, publica: true, aprovada: true);
        await p.InserirFotoAsync(agendamento, publica: true, aprovada: false);
        await p.InserirFotoAsync(agendamento, publica: false, aprovada: true);

        var portfolio = await p.Client.GetFromJsonAsync<JsonElement>($"api/Fotos/portfolio/{p.Id}");
        var porServico = await p.Client.GetFromJsonAsync<JsonElement>($"api/Fotos/portfolio-por-servico/{p.Id}");

        var item = portfolio.EnumerateArray().Single();
        Assert.Equal(aprovada.Id, item.GetProperty("id").GetGuid());
        Assert.Equal("Pintura", item.GetProperty("servico").GetString());
        Assert.Equal("Cliente Fotografado", item.GetProperty("cliente").GetString());

        var grupo = porServico.EnumerateArray().Single();
        Assert.Equal("Pintura", grupo.GetProperty("servico").GetString());
        Assert.Equal(1, grupo.GetProperty("fotos").GetArrayLength());
    }

    [Fact(DisplayName = "IT-124 Excluir foto remove do banco e do armazenamento; outro prestador 403; inexistente 404")]
    public async Task ExcluirFoto_Regras()
    {
        var a = await _f.CriarPrestadorAsync();
        var b = await _f.CriarPrestadorAsync();
        var (_, _, agendamento) = await CenarioAsync(a);
        var foto = await a.InserirFotoAsync(agendamento);
        var nomeBlob = Path.GetFileName(new Uri(foto.Url).AbsolutePath);

        await (await b.Client.DeleteAsync($"api/Fotos/{foto.Id}")).AssertStatusAsync(HttpStatusCode.Forbidden);
        await (await a.Client.DeleteAsync($"api/Fotos/{foto.Id}")).AssertStatusAsync(HttpStatusCode.OK);
        await (await a.Client.DeleteAsync($"api/Fotos/{foto.Id}")).AssertStatusAsync(HttpStatusCode.NotFound);

        Assert.Contains(_f.Blob.Chamadas, c => c.StartsWith("DELETE") && c.Contains(nomeBlob));
        Assert.False(await new ContextMongodb().Foto.Find(f => f.Id == foto.Id).AnyAsync());
    }

    [Fact(DisplayName = "IT-125 Portfólio público lista só prestadores com plano Full")]
    public async Task PortfolioPublico_SoPlanoFull()
    {
        var full = await _f.CriarPrestadorAsync();
        var basico = await _f.CriarPrestadorComPlanoAsync("Basico");
        var (_, _, agFull) = await CenarioAsync(full);
        var (_, _, agBasico) = await CenarioAsync(basico);
        var fotoFull = await full.InserirFotoAsync(agFull, aprovada: true);
        var fotoBasico = await basico.InserirFotoAsync(agBasico, aprovada: true);

        var lista = await _f.NovoClient().GetFromJsonAsync<JsonElement>("api/Fotos/portfolio-publico");

        Assert.NotNull(AcharPorId(lista, fotoFull.Id));
        Assert.Null(AcharPorId(lista, fotoBasico.Id));
    }

    [Fact(DisplayName = "IT-126 Estatísticas do portfólio respeitam a visibilidade de cidade e telefone")]
    public async Task PortfolioStats_RespeitaVisibilidade()
    {
        var p = await _f.CriarPrestadorAsync();
        var (_, _, agendamento) = await CenarioAsync(p, "Encanamento");
        await p.InserirFotoAsync(agendamento, aprovada: true);
        var cidade = $"Cidade{Guid.NewGuid():N}"[..14];
        var edit = await p.Client.PutAsJsonAsync($"api/Users/edit/{p.Id}", new
        {
            nomeCompleto = p.Nome,
            email = p.Email,
            telefone = "14977776666",
            cidade,
            cidadeVisivel = true,
            telefoneVisivel = false
        });
        await edit.AssertStatusAsync(HttpStatusCode.OK);

        var stats = await _f.NovoClient().GetFromJsonAsync<JsonElement>("api/Fotos/portfolio-stats");

        Assert.True(stats.GetProperty("totalFotos").GetInt32() >= 1);
        Assert.Contains(stats.GetProperty("cidades").EnumerateArray(), c => c.GetString() == cidade);
        Assert.Contains(stats.GetProperty("servicos").EnumerateArray(), s => s.GetProperty("nome").GetString() == "Encanamento");
        var item = stats.GetProperty("prestadores").EnumerateArray()
            .Single(x => x.GetProperty("cidade").ValueKind == JsonValueKind.String && x.GetProperty("cidade").GetString() == cidade);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("telefone").ValueKind);
        Assert.Equal(1, item.GetProperty("fotos").GetInt32());
        Assert.Equal(1, item.GetProperty("servicos").GetInt32());
    }
}
