using System.Globalization;
using System.Text.Json;

namespace DayPlannio.App.Services;

public class ResultadoEndereco
{
    public string Descricao { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public bool TemNumero { get; set; }
}

public static class GeocodificacaoService
{
    private const string BaseUrl = "https://nominatim.openstreetmap.org/";

    private const string CodigoPais = "br";

    private static readonly HttpClient _http = CriarHttpClient();

    private static HttpClient CriarHttpClient()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };

        http.DefaultRequestHeaders.UserAgent.ParseAdd("DayPlannio/1.0");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR");

        return http;
    }

    public static async Task<List<ResultadoEndereco>> Buscar(string texto, CancellationToken ct = default)
    {
        var resultados = new List<ResultadoEndereco>();

        if (string.IsNullOrWhiteSpace(texto))
            return resultados;

        var url =
            "search?format=jsonv2&addressdetails=1&limit=5" +
            "&accept-language=pt-BR" +
            (string.IsNullOrEmpty(CodigoPais) ? "" : $"&countrycodes={CodigoPais}") +
            $"&q={Uri.EscapeDataString(texto.Trim())}";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!TryLerCoordenadas(item, out var lat, out var lon))
                continue;

            resultados.Add(new ResultadoEndereco
            {
                Descricao = FormatarEndereco(item),
                Latitude = lat,
                Longitude = lon,
                TemNumero = item.TryGetProperty("address", out var a)
                    && a.ValueKind == JsonValueKind.Object
                    && a.TryGetProperty("house_number", out _)
            });
        }

        return resultados;
    }

    public static async Task<string?> BuscarEndereco(double latitude, double longitude, CancellationToken ct = default)
    {
        var url =
            "reverse?format=jsonv2&addressdetails=1&zoom=18&accept-language=pt-BR" +
            $"&lat={latitude.ToString("0.#######", CultureInfo.InvariantCulture)}" +
            $"&lon={longitude.ToString("0.#######", CultureInfo.InvariantCulture)}";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("error", out _))
            return null;

        var texto = FormatarEndereco(doc.RootElement);

        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    private static bool TryLerCoordenadas(JsonElement item, out double lat, out double lon)
    {
        lat = 0;
        lon = 0;

        return item.TryGetProperty("lat", out var latEl)
            && item.TryGetProperty("lon", out var lonEl)
            && double.TryParse(latEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
            && double.TryParse(lonEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out lon);
    }

    private static string FormatarEndereco(JsonElement item)
    {
        var displayName = item.TryGetProperty("display_name", out var dn)
            ? dn.GetString() ?? string.Empty
            : string.Empty;

        if (!item.TryGetProperty("address", out var addr) || addr.ValueKind != JsonValueKind.Object)
            return Limitar(displayName);

        string? Campo(params string[] chaves)
        {
            foreach (var chave in chaves)
            {
                if (addr.TryGetProperty(chave, out var el))
                {
                    var valor = el.GetString();
                    if (!string.IsNullOrWhiteSpace(valor))
                        return valor;
                }
            }

            return null;
        }

        var rua = Campo("road", "pedestrian", "footway", "path", "residential");
        var numero = Campo("house_number");
        var bairro = Campo("suburb", "neighbourhood", "quarter", "city_district");
        var cidade = Campo("city", "town", "village", "municipality", "county");

        string? uf = null;
        var iso = Campo("ISO3166-2-lvl4");
        if (iso != null && iso.Contains('-'))
            uf = iso[(iso.IndexOf('-') + 1)..];

        if (rua == null)
            return Limitar(displayName);

        var partes = rua;

        if (numero != null)
            partes += $", {numero}";

        if (bairro != null)
            partes += $" - {bairro}";

        if (cidade != null)
            partes += $", {cidade}{(uf != null ? "/" + uf : "")}";

        return Limitar(partes);
    }

    private static string Limitar(string texto) =>
        texto.Length <= 200 ? texto : texto[..200];
}
