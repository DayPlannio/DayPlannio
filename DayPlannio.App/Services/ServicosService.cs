using DayPlannio.App.Models;
using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Services
{
    public class ServicoService
    {
        public static async Task<List<Servico>?> GetServicos(string usuarioId)
        {
            List<Servico>? servicos = null;

            var response =
                await App.HttpClient.GetAsync(
                    $"api/tiposservico/{usuarioId}");

            if (response.IsSuccessStatusCode)
            {
                var json =
                    await response.Content.ReadAsStringAsync();

                servicos =
                    JsonSerializer.Deserialize<List<Servico>>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
            }

            return servicos;
        }

        public static async Task<(bool sucesso, string erro)> Create(object servico)
        {
            var json =
                JsonSerializer.Serialize(servico);

            var content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            var response =
                await App.HttpClient.PostAsync(
                    "api/tiposservico/create",
                    content);

            var body =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, body);

            return (true, "");
        }

        public static async Task<(bool sucesso, string erro)> Edit(
            string id,
            object servico)
        {
            var json =
                JsonSerializer.Serialize(servico);

            var content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            var response =
                await App.HttpClient.PutAsync(
                    $"api/tiposservico/edit/{id}",
                    content);

            var body =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, body);

            return (true, "");
        }

        public static async Task<(bool sucesso, string erro)> Delete(string id)
        {
            var response =
                await App.HttpClient.DeleteAsync(
                    $"api/tiposservico/delete/{id}");

            var body =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, body);

            return (true, "");
        }
    }
}