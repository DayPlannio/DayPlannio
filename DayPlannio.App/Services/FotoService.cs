using System.Text.Json;

namespace DayPlannio.App.Services
{
    public class FotoService
    {
        public static async Task<(bool sucesso, string erro, string? url)> UploadFoto(
            Stream stream, string fileName, string agendamentoId, string prestadorId, string? descricao, bool publica = false)
        {
            try
            {
                var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(stream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                content.Add(streamContent, "arquivo", fileName);
                content.Add(new StringContent(agendamentoId), "agendamentoId");
                content.Add(new StringContent(prestadorId), "prestadorId");
                if (!string.IsNullOrEmpty(descricao))
                    content.Add(new StringContent(descricao), "descricao");
                content.Add(new StringContent(publica.ToString().ToLower()), "publica");

                var response = await App.HttpClient.PostAsync("api/fotos/upload", content);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    var url = result?.GetValueOrDefault("url")?.ToString();
                    return (true, string.Empty, url);
                }

                return (false, body, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public static async Task<List<FotoItem>?> GetFotosAgendamento(string agendamentoId)
        {
            var response = await App.HttpClient.GetAsync($"api/fotos/agendamento/{agendamentoId}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<FotoItem>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            return null;
        }

        public static async Task<bool> DeleteFoto(string fotoId)
        {
            var response = await App.HttpClient.DeleteAsync($"api/fotos/{fotoId}");
            return response.IsSuccessStatusCode;
        }
    }

    public class FotoItem
    {
        public string Id { get; set; } = "";
        public string Url { get; set; } = "";
        public string? Descricao { get; set; }
        public bool Publica { get; set; }
        public DateTime DataUpload { get; set; }
    }
}
