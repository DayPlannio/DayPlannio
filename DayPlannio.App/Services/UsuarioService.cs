using DayPlannio.App.Models;
using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Services
{
    public class UsuarioService
    {
        public static async Task<(bool sucesso, string mensagem)> SolicitarPlano(string userId, string plano)
        {
            var body = new { usuarioId = userId, plano };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PostAsync("api/users/plano/solicitar", content);

            if (response.IsSuccessStatusCode)
                return (true, "Solicitação enviada. Aguardando a aprovação do administrador.");

            var corpo = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(corpo);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                    return (false, msg.GetString() ?? "Não foi possível solicitar o plano.");
            }
            catch
            {
            }

            return (false, "Não foi possível solicitar o plano.");
        }

        public static async Task<(bool sucesso, string mensagem)> DefinirRenovacao(string userId, bool renovacaoAutomatica)
        {
            var body = new { usuarioId = userId, renovacaoAutomatica };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PostAsync("api/users/plano/renovacao", content);

            var corpo = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    using var doc = JsonDocument.Parse(corpo);
                    if (doc.RootElement.TryGetProperty("message", out var msg))
                        return (true, msg.GetString() ?? "");
                }
                catch
                {
                }

                return (true, renovacaoAutomatica ? "Renovação automática ativada." : "Assinatura cancelada.");
            }

            try
            {
                using var doc = JsonDocument.Parse(corpo);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                    return (false, msg.GetString() ?? "Não foi possível atualizar a assinatura.");
            }
            catch
            {
            }

            return (false, "Não foi possível atualizar a assinatura.");
        }

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

        public static async Task<(string? userId, string erro)> Cadastrar(object usuario)
        {
            var json = JsonSerializer.Serialize(usuario);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PostAsync("api/users/create", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    using var doc = JsonDocument.Parse(responseJson);
                    if (doc.RootElement.TryGetProperty("message", out var msg))
                        return (null, msg.GetString() ?? "");

                    if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                        errors.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var erro in errors.EnumerateArray())
                        {
                            if (erro.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(erro.GetString()))
                                return (null, erro.GetString()!);
                        }
                    }
                }
                catch
                {
                }

                return (null, "");
            }

            string? userId = null;
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(responseJson);
                userId = data.GetProperty("id").GetString();
            }
            catch
            {
            }

            return (userId, "");
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

        public static async Task<(bool sucesso, string mensagem)> EncerrarConta(string id)
        {
            try
            {
                var response = await App.HttpClient.DeleteAsync($"api/users/delete/{id}");
                var corpo = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return (false, ExtrairMensagem(corpo, "Não foi possível encerrar a conta."));

                return (true, ExtrairMensagem(corpo, "Conta encerrada."));
            }
            catch
            {
                return (false, "Não foi possível encerrar a conta. Verifique sua conexão.");
            }
        }

        public static async Task<(bool sucesso, string mensagem)> EnviarRedefinicaoSenha(string email)
        {
            var body = new { email };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PostAsync("api/account/forgot-password", content);

            if (!response.IsSuccessStatusCode)
                return (false, "Não foi possível enviar o e-mail. Tente novamente.");

            var corpo = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(corpo);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                    return (true, msg.GetString() ?? "");
            }
            catch
            {
            }

            return (true, "");
        }

        public static async Task<(bool sucesso, string mensagem)> RedefinirSenha(string email, string code, string newPassword, string confirmPassword)
        {
            var body = new { email, code, newPassword, confirmPassword };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PostAsync("api/account/reset-password", content);

            if (response.IsSuccessStatusCode)
                return (true, "");

            var corpo = await response.Content.ReadAsStringAsync();
            return (false, ExtrairMensagem(corpo, "Código inválido ou expirado."));
        }

        public static async Task<(bool sucesso, string mensagem)> VerificarCodigo(string email, string code)
        {
            var body = new { email, code, newPassword = "TempValidacao@123", confirmPassword = "TempValidacao@123" };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PostAsync("api/account/verify-code", content);

            if (response.IsSuccessStatusCode)
                return (true, "");

            var corpo = await response.Content.ReadAsStringAsync();
            return (false, ExtrairMensagem(corpo, "Código inválido ou expirado."));
        }

        private static string ExtrairMensagem(string corpo, string padrao)
        {
            try
            {
                using var doc = JsonDocument.Parse(corpo);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                    return msg.GetString() ?? padrao;
            }
            catch
            {
            }
            return padrao;
        }
    }
}