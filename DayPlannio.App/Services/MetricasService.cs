using DayPlannio.App.Models;
using System.Text.Json;

namespace DayPlannio.App.Services
{
    public class MetricasService
    {
        public static async Task<MetricasPrestador?> GetMetricas(string usuarioId, string periodo = "mensal")
        {
            try
            {
                var userId = string.IsNullOrWhiteSpace(usuarioId)
                    ? "00000000-0000-0000-0000-000000000000"
                    : usuarioId;

                var url = $"api/agendamentos/metricas/{userId}?periodo={periodo}";

                var resposta = await App.HttpClient.GetAsync(url);

                if (!resposta.IsSuccessStatusCode)
                    return null;

                var json = await resposta.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<MetricasPrestador>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }
    }
}
