using DayPlannio.App.Models;
using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Services
{
    public class ClienteService
    {
        public static async Task<List<Cliente>?> GetClientes(string usuarioId)
        {
            List<Cliente>? clientes = null;

            var response =
                await App.HttpClient.GetAsync(
                    $"api/cliente/{usuarioId}");

            if (response.IsSuccessStatusCode)
            {
                var json =
                    await response.Content.ReadAsStringAsync();

                clientes =
                    JsonSerializer.Deserialize<List<Cliente>>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
            }

            return clientes;
        }

        public static async Task<(bool sucesso, string erro)> Create(object cliente)
        {
            var json =
                JsonSerializer.Serialize(cliente);

            var content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            var response =
                await App.HttpClient.PostAsync(
                    "api/cliente/create",
                    content);

            var body =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, body);

            return (true, "");
        }

        public static async Task<(bool sucesso, string erro)> Edit(
            string id,
            object cliente)
        {
            var json =
                JsonSerializer.Serialize(cliente);

            var content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            var response =
                await App.HttpClient.PutAsync(
                    $"api/cliente/edit/{id}",
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
                    $"api/cliente/delete/{id}");

            var body =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, body);

            return (true, "");
        }
    }
}