using System.Security.Claims;
using System.Text.Json;
using DayPlannio.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DayPlannio.Web.Controllers;

public class AdminController : Controller
{
    [HttpGet]
    public IActionResult Login()
    {
        return RedirectToAction("Login", "Account");
    }

    [Authorize(Policy = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Painel()
    {
        var model = new AdminPainelViewModel();

        if (TempData.ContainsKey("Mensagem"))
            model.Mensagem = TempData["Mensagem"]?.ToString();

        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

        try
        {
            var respLogs = await client.GetAsync("api/admin/logs?pagina=1&tamanhoPagina=100");
            if (respLogs.IsSuccessStatusCode)
            {
                var json = await respLogs.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<AdminLogsResposta>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data?.Logs != null)
                {
                    model.Logs = data.Logs;
                    model.TotalLogs = data.Total;
                }
            }
        }
        catch { }

        try
        {
            var respClientes = await client.GetAsync("api/admin/clientes");
            if (respClientes.IsSuccessStatusCode)
            {
                var json = await respClientes.Content.ReadAsStringAsync();
                var clientes = JsonSerializer.Deserialize<List<AdminClienteItem>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (clientes != null)
                    model.Clientes = clientes;
            }
        }
        catch { }

        try
        {
            var respPrestadores = await client.GetAsync("api/admin/prestadores");
            if (respPrestadores.IsSuccessStatusCode)
            {
                var json = await respPrestadores.Content.ReadAsStringAsync();
                var prestadores = JsonSerializer.Deserialize<List<AdminPrestadorItem>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (prestadores != null)
                    model.Prestadores = prestadores;
            }
        }
        catch { }

        try
        {
            var respFotos = await client.GetAsync("api/admin/fotos-pendentes");
            if (respFotos.IsSuccessStatusCode)
            {
                var json = await respFotos.Content.ReadAsStringAsync();
                var fotos = JsonSerializer.Deserialize<List<AdminFotoItem>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (fotos != null)
                {
                    model.FotosPendentes = fotos.Take(5).ToList();
                    model.TotalFotosPendentes = fotos.Count;
                }
            }
        }
        catch { }

        return View(model);
    }

    [Authorize(Policy = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Logs()
    {
        var model = new AdminPainelViewModel();
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

        try
        {
            var respLogs = await client.GetAsync("api/admin/logs?pagina=1&tamanhoPagina=500");
            if (respLogs.IsSuccessStatusCode)
            {
                var json = await respLogs.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<AdminLogsResposta>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data?.Logs != null)
                {
                    model.Logs = data.Logs;
                    model.TotalLogs = data.Total;
                }
            }
        }
        catch { }

        return View(model);
    }

    [Authorize(Policy = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Clientes()
    {
        var model = new AdminPainelViewModel();
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

        if (TempData.ContainsKey("Mensagem"))
            model.Mensagem = TempData["Mensagem"]?.ToString();

        try
        {
            var respClientes = await client.GetAsync("api/admin/clientes");
            if (respClientes.IsSuccessStatusCode)
            {
                var json = await respClientes.Content.ReadAsStringAsync();
                var clientes = JsonSerializer.Deserialize<List<AdminClienteItem>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (clientes != null)
                    model.Clientes = clientes;
            }
        }
        catch { }

        try
        {
            var respPrestadores = await client.GetAsync("api/admin/prestadores");
            if (respPrestadores.IsSuccessStatusCode)
            {
                var json = await respPrestadores.Content.ReadAsStringAsync();
                var prestadores = JsonSerializer.Deserialize<List<AdminPrestadorItem>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (prestadores != null)
                    model.Prestadores = prestadores;
            }
        }
        catch { }

        try
        {
            var respFotos = await client.GetAsync("api/admin/fotos-pendentes");
            if (respFotos.IsSuccessStatusCode)
            {
                var json = await respFotos.Content.ReadAsStringAsync();
                var fotos = JsonSerializer.Deserialize<List<AdminFotoItem>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (fotos != null)
                {
                    model.FotosPendentes = fotos.Take(5).ToList();
                    model.TotalFotosPendentes = fotos.Count;
                }
            }
        }
        catch { }

        return View(model);
    }

    [Authorize(Policy = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Prestadores()
    {
        var model = new AdminPainelViewModel();
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

        if (TempData.ContainsKey("Mensagem"))
            model.Mensagem = TempData["Mensagem"]?.ToString();

        try
        {
            var respPrestadores = await client.GetAsync("api/admin/prestadores");
            if (respPrestadores.IsSuccessStatusCode)
            {
                var json = await respPrestadores.Content.ReadAsStringAsync();
                var prestadores = JsonSerializer.Deserialize<List<AdminPrestadorItem>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (prestadores != null)
                    model.Prestadores = prestadores;
            }
        }
        catch { }

        return View(model);
    }

    [Authorize(Policy = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Fotos()
    {
        var model = new AdminPainelViewModel();
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

        try
        {
            var resp = await client.GetAsync("api/admin/fotos-pendentes");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var fotos = JsonSerializer.Deserialize<List<AdminFotoItem>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (fotos != null)
                {
                    model.FotosPendentes = fotos.Take(5).ToList();
                    model.TotalFotosPendentes = fotos.Count;
                }
            }
        }
        catch { }

        if (TempData.ContainsKey("Mensagem"))
            model.Mensagem = TempData["Mensagem"]?.ToString();

        return View(model);
    }

    [Authorize(Policy = "Admin")]
    [HttpGet]
    public async Task<IActionResult> TodasFotos()
    {
        var model = new AdminPainelViewModel();
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

        try
        {
            var resp = await client.GetAsync("api/admin/fotos-pendentes");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var fotos = JsonSerializer.Deserialize<List<AdminFotoItem>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (fotos != null)
                    model.FotosPendentes = fotos;
            }
        }
        catch { }

        if (TempData.ContainsKey("Mensagem"))
            model.Mensagem = TempData["Mensagem"]?.ToString();

        return View(model);
    }

    [Authorize(Policy = "Admin")]
    [HttpPost]
    public async Task<IActionResult> AprovarFoto(Guid id, string? origem)
    {
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");
        var adminId = ObterAdminIdLogado();
        var url = $"api/admin/fotos/{id}/aprovar" + (adminId.HasValue ? $"?adminId={adminId}" : "");
        await client.PutAsync(url, null);
        TempData["Mensagem"] = "Foto aprovada com sucesso.";
        var destino = origem switch
        {
            "Painel" => "Painel",
            "TodasFotos" => "TodasFotos",
            _ => "Fotos"
        };
        return RedirectToAction(destino);
    }

    [Authorize(Policy = "Admin")]
    [HttpPost]
    public async Task<IActionResult> RejeitarFoto(Guid id, string? origem)
    {
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");
        var adminId = ObterAdminIdLogado();
        var url = $"api/admin/fotos/{id}/rejeitar" + (adminId.HasValue ? $"?adminId={adminId}" : "");
        await client.PutAsync(url, null);
        TempData["Mensagem"] = "Foto rejeitada e removida.";
        var destino = origem switch
        {
            "Painel" => "Painel",
            "TodasFotos" => "TodasFotos",
            _ => "Fotos"
        };
        return RedirectToAction(destino);
    }

    [Authorize(Policy = "Admin")]
    [HttpPost]
    public async Task<IActionResult> AprovarPlano(Guid id, string? origem)
    {
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");
        await client.PostAsync($"api/admin/planos/{id}/aprovar", null);
        TempData["Mensagem"] = "Plano aprovado com sucesso.";
        var destino = origem switch
        {
            "Painel" => "Painel",
            _ => "Prestadores"
        };
        return RedirectToAction(destino);
    }

    [Authorize(Policy = "Admin")]
    [HttpPost]
    public async Task<IActionResult> RejeitarPlano(Guid id, string? origem)
    {
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");
        await client.PostAsync($"api/admin/planos/{id}/rejeitar", null);
        TempData["Mensagem"] = "Solicitação de plano rejeitada.";
        var destino = origem switch
        {
            "Painel" => "Painel",
            _ => "Prestadores"
        };
        return RedirectToAction(destino);
    }

    [Authorize(Policy = "Admin")]
    [HttpPost]
    public async Task<IActionResult> EncerrarConta(string? tipo, Guid id, string? motivo)
    {
        var tipoNormalizado = (tipo ?? string.Empty).Trim().ToLowerInvariant();

        if (tipoNormalizado != "cliente" && tipoNormalizado != "prestador")
        {
            TempData["Mensagem"] = "Tipo de conta inválido.";
            return RedirectToAction("Painel");
        }

        var motivoLimpo = (motivo ?? string.Empty).Trim();

        if (motivoLimpo.Length == 0)
        {
            TempData["Mensagem"] = "Informe o motivo do encerramento da conta.";
            return RetornarTela(tipoNormalizado);
        }

        if (motivoLimpo.Length < 5)
        {
            TempData["Mensagem"] = "O motivo deve ter pelo menos 5 caracteres.";
            return RetornarTela(tipoNormalizado);
        }

        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");
        var payload = new { tipo = tipoNormalizado, id, motivo = motivoLimpo };

        try
        {
            var response = await client.PostAsJsonAsync("api/admin/contas/encerrar", payload);

            if (response.IsSuccessStatusCode)
            {
                TempData["Mensagem"] = "Conta encerrada com sucesso.";
            }
            else
            {
                var erro = await LerMensagemErroAsync(response);
                TempData["Mensagem"] = erro;
            }
        }
        catch
        {
            TempData["Mensagem"] = "Não foi possível encerrar a conta agora. Tente novamente.";
        }

        return RetornarTela(tipoNormalizado);
    }

    private IActionResult RetornarTela(string tipo) => tipo switch
    {
        "prestador" => RedirectToAction("Prestadores"),
        "cliente" => RedirectToAction("Clientes"),
        _ => RedirectToAction("Painel")
    };

    private static async Task<string> LerMensagemErroAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body))
                return "Não foi possível encerrar a conta.";

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msg) && !string.IsNullOrWhiteSpace(msg.GetString()))
                return msg.GetString()!;

            return "Não foi possível encerrar a conta.";
        }
        catch
        {
            return "Não foi possível encerrar a conta.";
        }
    }

    [Authorize(Policy = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");
        var adminId = ObterAdminIdLogado();
        var url = "api/admin/logout" + (adminId.HasValue ? $"?adminId={adminId}" : "");

        try
        {
            await client.PostAsync(url, null);
        }
        catch { }

        await HttpContext.SignOutAsync("Cliente");
        return RedirectToAction("Login", "Account");
    }

    private Guid? ObterAdminIdLogado()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public class AdminLoginResposta
{
    public Guid UserId { get; set; }
    public string? Nome { get; set; }
    public string? Email { get; set; }
}

public class AdminLogsResposta
{
    public long Total { get; set; }
    public int Pagina { get; set; }
    public int TamanhoPagina { get; set; }
    public List<AdminLogItem> Logs { get; set; } = new();
}
