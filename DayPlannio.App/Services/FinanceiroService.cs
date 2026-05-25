using DayPlannio.App.Models;
using System.Text;
using System.Text.Json;

namespace DayPlannio.App.Services;

public class FinanceiroService
{
    public static async Task<List<Financeiro>?> GetAll(string usuarioId)
    {
        List<Financeiro>? registros = null;

        var response =
            await App.HttpClient.GetAsync($"api/financeiros/{usuarioId}");

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();

            registros = JsonSerializer.Deserialize<List<Financeiro>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }

        return registros;
    }

    public static async Task<(bool sucesso, string erro)> Create(object financeiro)
    {
        var json = JsonSerializer.Serialize(financeiro);

        var content =
            new StringContent(json, Encoding.UTF8, "application/json");

        var response =
            await App.HttpClient.PostAsync(
                "api/financeiros/create",
                content);

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var erro =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(
                        body,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                return (
                    false,
                    erro?.GetValueOrDefault("message")
                    ?? "Erro desconhecido.");
            }
            catch
            {
                return (false, body);
            }
        }

        return (true, string.Empty);
    }

    public static async Task<(bool sucesso, string erro)> Edit(
        string id,
        object financeiro)
    {
        var json = JsonSerializer.Serialize(financeiro);

        var content =
            new StringContent(json, Encoding.UTF8, "application/json");

        var response =
            await App.HttpClient.PutAsync(
                $"api/financeiros/edit/{id}",
                content);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();

            try
            {
                var erro =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(
                        body,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                return (
                    false,
                    erro?.GetValueOrDefault("message")
                    ?? "Erro ao salvar.");
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
        var response =
            await App.HttpClient.DeleteAsync(
                $"api/financeiros/delete/{id}");

        return response.IsSuccessStatusCode;
    }

    public static async Task<ResumoFinanceiro?> GetResumoGeral(
        string usuarioId,
        string periodo = "mensal")
    {
        ResumoFinanceiro? resumo = null;

        var response =
            await App.HttpClient.GetAsync(
                $"api/financeiros/resumo-geral/{usuarioId}?periodo={periodo}");

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();

            resumo = JsonSerializer.Deserialize<ResumoFinanceiro>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }

        return resumo;
    }

    public static async Task<List<Financeiro>?> GetByPeriodo(string usuarioId, string periodo = "diario")
    {
        var response = await App.HttpClient.GetAsync($"api/financeiros/{usuarioId}/periodo?periodo={periodo}");

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<Financeiro>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public static async Task<byte[]?> GetRelatorioPdf(
        string usuarioId,
        string periodo = "mensal")
    {
        var response =
            await App.HttpClient.GetAsync(
                $"api/financeiros/relatorio/{usuarioId}?periodo={periodo}");

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsByteArrayAsync();
    }
}