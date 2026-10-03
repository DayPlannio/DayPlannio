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

        public static async Task<(bool sucesso, string email, string senha, string erro)> GerarCredenciais(string id)
        {
            var response =
                await App.HttpClient.PostAsync(
                    $"api/cliente/gerar-credenciais/{id}",
                    null);

            var body =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var mensagem = ExtrairMensagem(body);
                return (false, string.Empty, string.Empty, mensagem);
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var email = root.TryGetProperty("email", out var e) ? e.GetString() ?? string.Empty : string.Empty;
            var senha = root.TryGetProperty("senhaProvisoria", out var s) ? s.GetString() ?? string.Empty : string.Empty;

            return (true, email, senha, "");
        }

        private static string ExtrairMensagem(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "Não foi possível gerar o acesso do cliente.";

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var m))
                    return m.GetString() ?? body;
            }
            catch
            {
            }

            return body;
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