using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Services;

public record LeituraResumo(string Cliente, double? DistanciaMetros, int? RaioMetros, string Status = "", DateTime? DataHora = null);

public record RespostaLocalizacao(
    bool Parar,
    int? IntervaloSegundos,
    string? Resumo = null,
    IReadOnlyList<LeituraResumo>? Leituras = null);

public static class EnviadorLocalizacao
{
    public static string UltimoResumo { get; private set; } = string.Empty;

    public static RespostaLocalizacao? UltimaResposta { get; private set; }

    public static DateTime? UltimoEnvioEm { get; private set; }

    public static void RegistrarResumo(string texto) =>
        UltimoResumo = $"{DateTime.Now:HH:mm:ss}  {texto}";

    public static async Task<RespostaLocalizacao?> EnviarAsync(double latitude, double longitude, double? precisao)
    {
        try
        {
            var userId = Preferences.Get("userId", string.Empty);
            if (string.IsNullOrEmpty(userId))
                return new RespostaLocalizacao(true, null);

            var origem = $"GPS (precisão {precisao?.ToString("0") ?? "?"} m)";

            if (SimulacaoLocalizacao.Ativa)
            {
                latitude = SimulacaoLocalizacao.Latitude;
                longitude = SimulacaoLocalizacao.Longitude;
                precisao = SimulacaoLocalizacao.Precisao;
                origem = "posição simulada";
            }

            var corpo = JsonSerializer.Serialize(new
            {
                usuarioId = userId,
                latitude,
                longitude,
                precisao
            });

            using var conteudo = new StringContent(corpo, Encoding.UTF8, "application/json");
            using var resposta = await App.HttpClient.PostAsync("api/agendamentos/localizacao", conteudo);

            if ((int)resposta.StatusCode is 403 or 410)
            {
                RegistrarResumo($"[{origem}] API recusou o envio (HTTP {(int)resposta.StatusCode}): assinatura inativa?");
                return new RespostaLocalizacao(true, null);
            }

            if (!resposta.IsSuccessStatusCode)
            {
                RegistrarResumo($"[{origem}] Falha na API: HTTP {(int)resposta.StatusCode}");
                UltimaResposta = null;
                return null;
            }

            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            var raiz = doc.RootElement;

            var parar = raiz.TryGetProperty("rastrear", out var rastrear) && !rastrear.GetBoolean();

            int? intervalo = raiz.TryGetProperty("intervaloSegundos", out var i) && i.TryGetInt32(out var s) && s > 0
                ? s
                : null;

            var resumo = MontarResumo(raiz, origem, parar);
            RegistrarResumo(resumo);

            var leiturasLidas = new List<LeituraResumo>();
            if (raiz.TryGetProperty("leituras", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var l in arr.EnumerateArray())
                {
                    leiturasLidas.Add(new LeituraResumo(
                        l.TryGetProperty("cliente", out var c) ? c.GetString() ?? "?" : "?",
                        l.TryGetProperty("distanciaMetros", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : null,
                        l.TryGetProperty("raioMetros", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetInt32() : null,
                        l.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "",
                        l.TryGetProperty("dataHora", out var dh) && dh.TryGetDateTime(out var dt) ? dt : null));
                }
            }

            var final = new RespostaLocalizacao(parar, intervalo, resumo, leiturasLidas);
            UltimaResposta = final;
            UltimoEnvioEm = DateTime.Now;
            return final;
        }
        catch (Exception ex)
        {
            RegistrarResumo($"Não foi possível falar com a API: {ex.Message}");
            UltimaResposta = null;
            return null;
        }
    }

    private static string MontarResumo(JsonElement raiz, string origem, bool parar)
    {
        var linhas = new List<string>();

        if (raiz.TryGetProperty("leituras", out var leituras) && leituras.ValueKind == JsonValueKind.Array)
        {
            foreach (var l in leituras.EnumerateArray())
            {
                var cliente = l.TryGetProperty("cliente", out var c) ? c.GetString() : "?";
                var obs = l.TryGetProperty("observacao", out var o) ? o.GetString() : string.Empty;

                if (l.TryGetProperty("distanciaMetros", out var d) && d.ValueKind == JsonValueKind.Number
                    && l.TryGetProperty("raioMetros", out var r) && r.ValueKind == JsonValueKind.Number)
                    linhas.Add($"{cliente}: a {d.GetDouble():0} m (raio {r.GetInt32()} m) - {obs}");
                else
                    linhas.Add($"{cliente}: {obs}");
            }
        }

        if (linhas.Count == 0)
            linhas.Add(parar
                ? "Nenhum agendamento a acompanhar (ou cliente sem localização)."
                : "Nenhum agendamento na janela de horário.");

        return $"[{origem}] " + string.Join(" | ", linhas);
    }
}
