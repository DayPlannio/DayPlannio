using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DayPlannio.Api.Settings;
using Microsoft.Extensions.Options;

namespace DayPlannio.Api.Services
{
    public record RecomendacaoEntrada(string Tipo, string Titulo, string Texto, bool Alerta, decimal? GanhoEstimado);
    public record RecomendacaoSaida(string Tipo, string Titulo, string Texto);

    public class RecomendacaoIaService
    {
        private const string Sistema =
            "Você é o assistente do app DayPlannio, para profissionais autônomos brasileiros. " +
            "Reescreva cada recomendação em português do Brasil, de forma clara, amigável e curta (até 3 frases). " +
            "REGRAS OBRIGATÓRIAS: não invente, não altere e não remova nenhum número, valor em R$, porcentagem " +
            "ou nome de serviço; mantenha o mesmo 'tipo' e a mesma ordem; não crie recomendações novas; " +
            "não dê garantias de ganho. Responda SOMENTE com JSON no formato " +
            "{\"recomendacoes\":[{\"tipo\":\"...\",\"titulo\":\"...\",\"texto\":\"...\"}]}.";

        private readonly HttpClient _http;
        private readonly GeminiSettings _gemini;
        private readonly ILogger<RecomendacaoIaService> _logger;

        public RecomendacaoIaService(
            HttpClient http,
            IOptions<GeminiSettings> gemini,
            ILogger<RecomendacaoIaService> logger)
        {
            _http = http;
            _gemini = gemini.Value;
            _logger = logger;
        }

        public bool Disponivel => _gemini.Configurado;

        public async Task<List<RecomendacaoSaida>?> RedigirAsync(
            string periodo, IReadOnlyList<RecomendacaoEntrada> recomendacoes, CancellationToken ct)
        {
            if (!Disponivel || recomendacoes.Count == 0)
                return null;

            var entradaJson = JsonSerializer.Serialize(new { periodo, recomendacoes });

            try
            {
                var conteudo = await ChamarGeminiAsync(entradaJson, ct);

                if (string.IsNullOrWhiteSpace(conteudo))
                    return null;

                var lista = Extrair(conteudo);
                return Validar(recomendacoes, lista) ? lista : null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                          or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Falha ao redigir recomendações com a IA");
                return null;
            }
        }

        private async Task<string?> ChamarGeminiAsync(string entrada, CancellationToken ct)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_gemini.Model}:generateContent";

            var corpo = new
            {
                systemInstruction = new { parts = new[] { new { text = Sistema } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = entrada } } } },
                generationConfig = new
                {
                    temperature = 0.3,
                    maxOutputTokens = 2048,
                    responseMimeType = "application/json"
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("x-goog-api-key", _gemini.ApiKey);

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini respondeu {Status}", (int)resp.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("candidates")[0]
                .GetProperty("content").GetProperty("parts")[0]
                .GetProperty("text").GetString();
        }

        private static List<RecomendacaoSaida>? Extrair(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;

            if (raiz.ValueKind == JsonValueKind.Object)
            {
                if (raiz.TryGetProperty("recomendacoes", out var r)) raiz = r;
                else
                {
                    foreach (var p in raiz.EnumerateObject())
                        if (p.Value.ValueKind == JsonValueKind.Array) { raiz = p.Value; break; }
                }
            }

            return raiz.ValueKind == JsonValueKind.Array
                ? raiz.Deserialize<List<RecomendacaoSaida>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                : null;
        }

        private static bool Validar(IReadOnlyList<RecomendacaoEntrada> entrada, List<RecomendacaoSaida>? saida)
        {
            if (saida == null || saida.Count != entrada.Count)
                return false;

            for (var i = 0; i < entrada.Count; i++)
            {
                if (!string.Equals(saida[i].Tipo, entrada[i].Tipo, StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(saida[i].Titulo)
                    || string.IsNullOrWhiteSpace(saida[i].Texto)
                    || saida[i].Titulo.Length > 120
                    || saida[i].Texto.Length > 600)
                    return false;
                var originais = Numeros(entrada[i].Titulo + " " + entrada[i].Texto);
                var novos = Numeros(saida[i].Titulo + " " + saida[i].Texto);
                if (originais.Except(novos).Any())
                    return false;
            }

            return true;
        }

        private static HashSet<string> Numeros(string texto) =>
            Regex.Matches(texto, @"\d+(?:[.,]\d+)*").Select(m => m.Value).ToHashSet();
    }
}
