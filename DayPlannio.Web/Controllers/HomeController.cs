using System.Text.Json;
using DayPlannio.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DayPlannio.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Login", "Account");
    }

    public IActionResult Login()
    {
        return RedirectToAction("Login", "Account");
    }

    public IActionResult Metricas()
    {
        return RedirectToAction("Metricas", "Dashboard");
    }

    [HttpGet("portfolio")]
    public async Task<IActionResult> Portfolio()
    {
        var model = new List<PortfolioItem>();
        PortfolioStats? stats = null;

        try
        {
            var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

            var resp = await client.GetAsync("api/fotos/portfolio-publico");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                model = JsonSerializer.Deserialize<List<PortfolioItem>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }

            var statsResp = await client.GetAsync("api/fotos/portfolio-stats");
            if (statsResp.IsSuccessStatusCode)
            {
                var statsJson = await statsResp.Content.ReadAsStringAsync();
                stats = JsonSerializer.Deserialize<PortfolioStats>(
                    statsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        }
        catch { }

        ViewBag.Stats = stats;
        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
