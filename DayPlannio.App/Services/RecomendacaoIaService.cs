using System.Net.Http.Json;
using System.Text.Json;

namespace DayPlannio.App.Services;

/// <summary>
/// Pede à API (que usa o Azure OpenAI) para redigir em linguagem natural as recomendações
/// já calculadas por RecomendacaoService. Os números continuam vindo das regras; a IA só
/// escreve o texto. Se a chamada falhar, as recomendações originais são mantidas.
/// </summary>
public static class RecomendacaoIaService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed record RecomendacaoDto(string Tipo, string Titulo, string Texto, bool Alerta, decimal? GanhoEstimado);
    private sealed record Requisicao(string Periodo, List<RecomendacaoDto> Recomendacoes);
    private sealed record RecomendacaoIaDto(string Tipo, string Titulo, string Texto);

    public static async Task<List<Recomendacao>> RedigirAsync(
        string usuarioId, string periodo, List<Recomendacao> recomendacoes)
    {
        if (recomendacoes.Count == 0)
            return recomendacoes;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));

            var corpo = new Requisicao(periodo, recomendacoes
                .Select(r => new RecomendacaoDto(r.Tipo.ToString(), r.Titulo, r.Texto, r.Alerta, r.GanhoEstimado))
                .ToList());

            var resposta = await App.HttpClient.PostAsJsonAsync(
                $"api/agendamentos/metricas/{usuarioId}/recomendacoes-ia", corpo, cts.Token);

            if (!resposta.IsSuccessStatusCode)
                return recomendacoes;

            var json = await resposta.Content.ReadAsStringAsync(cts.Token);
            var ia = JsonSerializer.Deserialize<List<RecomendacaoIaDto>>(json, Json);

            if (ia == null || ia.Count != recomendacoes.Count)
                return recomendacoes;

            for (var i = 0; i < recomendacoes.Count; i++)
            {
                if (!string.Equals(ia[i].Tipo, recomendacoes[i].Tipo.ToString(), StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(ia[i].Titulo)
                    || string.IsNullOrWhiteSpace(ia[i].Texto))
                    return recomendacoes;
            }

            for (var i = 0; i < recomendacoes.Count; i++)
            {
                recomendacoes[i].Titulo = ia[i].Titulo.Trim();
                recomendacoes[i].Texto = ia[i].Texto.Trim();
                recomendacoes[i].GeradaPorIa = true;
            }

            return recomendacoes;
        }
        catch
        {
            return recomendacoes;
        }
    }
}
