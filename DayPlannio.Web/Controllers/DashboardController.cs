using System.Security.Claims;
using System.Text.Json;
using DayPlannio.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DayPlannio.Web.Controllers;

[Authorize(Policy = "Cliente")]
public class DashboardController : Controller
{
    [HttpGet]
    public async Task<IActionResult> Metricas(string periodo = "mensal")
    {
        var model = new MetricasViewModel { Periodo = periodo };

        var clienteId = User.FindFirst("clienteId")?.Value;
        var usuarioId = User.FindFirst("usuarioId")?.Value;

        if (string.IsNullOrEmpty(clienteId) || string.IsNullOrEmpty(usuarioId))
            return RedirectToAction("Login", "Account");

        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

        var resp = await client.GetAsync(
            $"api/agendamentos/metricas-cliente/{clienteId}?usuarioId={usuarioId}&periodo={periodo}");

        if (resp.IsSuccessStatusCode)
        {
            var json = await resp.Content.ReadAsStringAsync();
            model.Metricas = JsonSerializer.Deserialize<MetricasClienteDto>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        else
        {
            var json = await resp.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(json))
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("message", out var message))
                    model.ErroMensagem = message.GetString();
            }
        }

        model.BuildChartData();
        return View(model);
    }

    [HttpGet("HistoricoCliente")]
    public async Task<IActionResult> HistoricoCliente()
    {
        var model = new HistoricoClienteViewModel();

        var clienteId = User.FindFirst("clienteId")?.Value;
        var usuarioId = User.FindFirst("usuarioId")?.Value;

        if (string.IsNullOrEmpty(clienteId) || string.IsNullOrEmpty(usuarioId))
            return RedirectToAction("Login", "Account");

        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

        var resp = await client.GetAsync(
            $"api/agendamentos/historico-completo/{clienteId}?usuarioId={usuarioId}");

        if (resp.IsSuccessStatusCode)
        {
            var json = await resp.Content.ReadAsStringAsync();
            var itens = JsonSerializer.Deserialize<List<HistoricoItem>>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            model.Historico = itens ?? new List<HistoricoItem>();
        }
        else
        {
            var json = await resp.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("message", out var message))
                        model.ErroMensagem = message.GetString();
                }
                catch (JsonException)
                {
                }
            }
        }

        return View(model);
    }
}
