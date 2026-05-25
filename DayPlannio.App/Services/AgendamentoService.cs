using DayPlannio.App.Models;
using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Services
{
    public class AgendamentoService
    {
        public static async Task<List<Agendamento>?> GetHistorico(string clienteId)
        {
            List<Agendamento>? historico = null;
            var response = await App.HttpClient.GetAsync($"api/agendamentos/historico/{clienteId}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                historico = JsonSerializer.Deserialize<List<Agendamento>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            return historico;
        }

        public static async Task<(bool sucesso, string erro)> Create(object agendamento)
        {
            var json = JsonSerializer.Serialize(agendamento);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PostAsync("api/agendamentos/create", content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var erro = JsonSerializer.Deserialize<Dictionary<string, string>>(body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return (false, erro?.GetValueOrDefault("message") ?? "Erro desconhecido.");
                }
                catch
                {
                    return (false, body);
                }
            }

            return (true, string.Empty);
        }

        public static async Task<List<Agendamento>?> GetAgenda(string usuarioId, string periodo = "diario")
        {
            List<Agendamento>? agendamentos = null;
            var response = await App.HttpClient.GetAsync($"api/agendamentos/agenda/{usuarioId}?periodo={periodo}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                agendamentos = JsonSerializer.Deserialize<List<Agendamento>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            return agendamentos;
        }

        public static async Task<bool> Cancelar(string id)
        {
            var response = await App.HttpClient.PutAsync($"api/agendamentos/cancelar/{id}", null);
            return response.IsSuccessStatusCode;
        }

        public static async Task<bool> Concluir(string id)
        {
            var response = await App.HttpClient.PutAsync($"api/agendamentos/concluir/{id}", null);
            return response.IsSuccessStatusCode;
        }

        public static async Task<(bool sucesso, string erro)> Edit(string id, object agendamento)
        {
            var json = JsonSerializer.Serialize(agendamento);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await App.HttpClient.PutAsync($"api/agendamentos/edit/{id}", content);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                try
                {
                    var erro = JsonSerializer.Deserialize<Dictionary<string, string>>(body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return (false, erro?.GetValueOrDefault("message") ?? "Erro ao salvar.");
                }
                catch
                {
                    return (false, body);
                }
            }

            return (true, string.Empty);
        }

        public static async Task<bool> Delete(string id)
        {
            var response = await App.HttpClient.DeleteAsync($"api/agendamentos/delete/{id}");
            return response.IsSuccessStatusCode;
        }
    }
}