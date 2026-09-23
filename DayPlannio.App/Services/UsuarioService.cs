using DayPlannio.App.Models;
using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Services
{
    public class UsuarioService
    {
        public static async Task<string?> Login(string email, string senha)
        {
            string? userId = null;
            var body = new { email, senha };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PostAsync("api/account/login", content);
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(responseJson);
                userId = data.GetProperty("userId").GetString();
            }
            return userId;
        }

        public static async Task<string?> Cadastrar(object usuario)
        {
            string? userId = null;
            var json = JsonSerializer.Serialize(usuario);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PostAsync("api/users/create", content);
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(responseJson);
                userId = data.GetProperty("id").GetString();
            }
            return userId;
        }

        public static async Task<Usuario?> GetPerfil(string id)
        {
            Usuario? perfil = null;
            var response = await App.HttpClient.GetAsync($"api/users/meu-perfil/{id}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                perfil = JsonSerializer.Deserialize<Usuario>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            return perfil;
        }

        public static async Task<(bool sucesso, string erro)> Edit(
     string id,
     object usuario)
        {
            var json =
                JsonSerializer.Serialize(usuario);

            var content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            var response =
                await App.HttpClient.PutAsync(
                    $"api/users/edit/{id}",
                    content);

            var body =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, body);

            return (true, "");
        }

        public static async Task<bool> EnviarRedefinicaoSenha(string email)
        {
            var body = new { email };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PostAsync("api/account/forgot-password", content);
            return response.IsSuccessStatusCode;
        }

        public static async Task<(bool sucesso, string erro)> RedefinirSenha(string email, string code, string newPassword, string confirmPassword)
        {
            var body = new { email, code, newPassword, confirmPassword };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PostAsync("api/account/reset-password", content);

            if (response.IsSuccessStatusCode)
                return (true, "");

            var responseJson = await response.Content.ReadAsStringAsync();
            return (false, ExtrairMensagemErro(responseJson));
        }

        public static async Task<(bool valido, string erro)> VerificarCodigo(string email, string code)
        {
            var body = new { email, code, newPassword = "TempValidacao@123", confirmPassword = "TempValidacao@123" };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PostAsync("api/account/verify-code", content);

            if (response.IsSuccessStatusCode)
                return (true, "");

            var responseJson = await response.Content.ReadAsStringAsync();
            return (false, ExtrairMensagemErro(responseJson));
        }

        private static string ExtrairMensagemErro(string responseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                    return msg.GetString() ?? "";

                if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == JsonValueKind.Array)
                {
                    foreach (var erro in errors.EnumerateArray())
                    {
                        if (erro.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(erro.GetString()))
                            return erro.GetString()!;
                    }
                }
            }
            catch
            {
                // corpo não-JSON
            }

            return "";
        }
    }
}