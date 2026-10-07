using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DayPlannio.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DayPlannio.Web.Controllers;

public class AccountController : Controller
{
    [HttpGet]
    public IActionResult Login()
    {
        var model = new LoginViewModel();
        if (TempData.ContainsKey("Sucesso"))
            ViewData["Sucesso"] = TempData["Sucesso"]?.ToString();
        if (TempData.ContainsKey("Erro"))
            model.Erro = TempData["Erro"]?.ToString();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Senha))
        {
            model.Erro = "Informe e-mail e senha.";
            return View(model);
        }

        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");
        var payload = JsonSerializer.Serialize(new { email = model.Email, senha = model.Senha });

        try
        {
            var respCliente = await client.PostAsync(
                "api/cliente/login",
                new StringContent(payload, Encoding.UTF8, "application/json"));

            if (respCliente.IsSuccessStatusCode)
            {
                var json = await respCliente.Content.ReadAsStringAsync();
                var login = JsonSerializer.Deserialize<LoginResposta>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (login != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim("tipo", "cliente"),
                        new Claim("clienteId", login.ClienteId.ToString()),
                        new Claim("usuarioId", login.UsuarioId.ToString()),
                        new Claim(ClaimTypes.Email, login.Email)
                    };

                    var identity = new ClaimsIdentity(claims, "Cliente");
                    var principal = new ClaimsPrincipal(identity);
                    await HttpContext.SignInAsync("Cliente", principal);

                    if (login.PrimeiroAcesso)
                        return RedirectToAction("TrocarSenha");

                    return RedirectToAction("Metricas", "Dashboard");
                }
            }
        }
        catch { }

        try
        {
            var respAdmin = await client.PostAsync(
                "api/admin/login",
                new StringContent(payload, Encoding.UTF8, "application/json"));

            if (respAdmin.IsSuccessStatusCode)
            {
                var json = await respAdmin.Content.ReadAsStringAsync();
                var login = JsonSerializer.Deserialize<AdminLoginResposta>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (login != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim("tipo", "admin"),
                        new Claim("adminId", login.UserId.ToString()),
                        new Claim("adminNome", login.Nome ?? ""),
                        new Claim(ClaimTypes.Email, login.Email ?? model.Email)
                    };

                    var identity = new ClaimsIdentity(claims, "Cliente");
                    var principal = new ClaimsPrincipal(identity);
                    await HttpContext.SignInAsync("Cliente", principal);

                    return RedirectToAction("Painel", "Admin");
                }
            }
        }
        catch { }

        model.Erro = "Credenciais inválidas.";
        return View(model);
    }

    [HttpGet]
    public IActionResult EsqueciSenha(string? editar)
    {
        var model = new EsqueciSenhaViewModel { Passo = 1 };

        if (editar == "1")
        {
            var emailAtual = TempData["EsqueciEmail"]?.ToString() ?? string.Empty;
            TempData.Remove("EsqueciEmail");
            TempData.Remove("EsqueciMsg");
            model.Email = emailAtual;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EsqueciSenha(EsqueciSenhaViewModel model)
    {
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

        if (model.Passo <= 1)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                model.Erro = "Informe seu e-mail de acesso.";
                model.Passo = 1;
                return View(model);
            }

            var payloadEmail = JsonSerializer.Serialize(new { email = model.Email.Trim() });
            var resp = await client.PostAsync(
                "api/cliente/forgot-password",
                new StringContent(payloadEmail, Encoding.UTF8, "application/json"));

            var corpo = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                try
                {
                    var err = JsonSerializer.Deserialize<Dictionary<string, string>>(corpo);
                    model.Erro = err != null && err.ContainsKey("message") ? err["message"] : "Não foi possível solicitar a recuperação.";
                }
                catch
                {
                    model.Erro = "Não foi possível solicitar a recuperação.";
                }
                model.Passo = 1;
                return View(model);
            }

            model.Passo = 2;
            model.Mensagem = "Se o e-mail estiver cadastrado, enviamos um código de recuperação para seu e-mail de recuperação.";

            TempData["EsqueciEmail"] = model.Email.Trim();
            TempData["EsqueciMsg"] = model.Mensagem;
            return View(model);
        }

        if (Request.Form["acao"] == "reenviar")
        {
            var payloadEmail = JsonSerializer.Serialize(new { email = model.Email.Trim() });
            var resp = await client.PostAsync(
                "api/cliente/forgot-password",
                new StringContent(payloadEmail, Encoding.UTF8, "application/json"));

            var corpo = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                model.Erro = "Não foi possível reenviar o código. Tente novamente.";
            }
            else
            {
                try
                {
                    var r = JsonSerializer.Deserialize<Dictionary<string, string>>(corpo);
                    model.Mensagem = r != null && r.ContainsKey("message") ? r["message"] : "Reenviamos o mesmo código.";
                }
                catch
                {
                    model.Mensagem = "Reenviamos o código. Ele continua válido por 15 minutos.";
                }
            }
            model.Passo = 2;
            return View(model);
        }

        if (model.SenhaNova != model.SenhaNovaConfirmacao)
        {
            model.Erro = "A nova senha e a confirmação não conferem.";
            model.Passo = 2;
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.Code) || string.IsNullOrWhiteSpace(model.SenhaNova))
        {
            model.Erro = "Informe o código e a nova senha.";
            model.Passo = 2;
            return View(model);
        }

        if (!ValidarSenhaRequisitos(model.SenhaNova))
        {
            model.Erro = "A nova senha não atende aos requisitos: mínimo 6 caracteres, com maiúscula, minúscula, número e caractere especial.";
            model.Passo = 2;
            return View(model);
        }

        var payloadReset = JsonSerializer.Serialize(new
        {
            email = model.Email.Trim(),
            code = model.Code.Trim(),
            senhaNova = model.SenhaNova
        });

        var respReset = await client.PostAsync(
            "api/cliente/reset-password",
            new StringContent(payloadReset, Encoding.UTF8, "application/json"));

        var corpoReset = await respReset.Content.ReadAsStringAsync();
        if (!respReset.IsSuccessStatusCode)
        {
            try
            {
                var err = JsonSerializer.Deserialize<Dictionary<string, string>>(corpoReset);
                model.Erro = err != null && err.ContainsKey("message") ? err["message"] : "Código inválido ou expirado.";
            }
            catch
            {
                model.Erro = "Código inválido ou expirado.";
            }
            model.Passo = 2;
            return View(model);
        }

        TempData.Remove("EsqueciEmail");
        TempData.Remove("EsqueciMsg");
        TempData["Sucesso"] = "Senha redefinida com sucesso. Faça login com a nova senha.";
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult PoliticaPrivacidade()
    {
        return View(new PoliticaPrivacidadeViewModel
        {
            Autenticado = EhCliente(),
            Obrigatoria = TempData.Peek("PoliticaObrigatoria") != null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PoliticaPrivacidade(PoliticaPrivacidadeViewModel model)
    {
        var obrigatoria = TempData.Peek("PoliticaObrigatoria") != null;
        TempData.Remove("PoliticaObrigatoria");

        if (model.Escolha == "nao_concordar")
        {
            if (EhCliente())
                return RedirectToAction(nameof(RecusarPoliticaPrivacidade));

            TempData["Erro"] = "Para não concordar com a Política de Privacidade, encerre a sua conta em "
                + "Minha conta → Encerrar minha conta.";
            return RedirectToAction(nameof(Login));
        }

        if (obrigatoria)
        {
            TempData["Sucesso"] = "Obrigada! Sua senha foi alterada e você concordou com a Política de Privacidade.";
            return RedirectToAction("Metricas", "Dashboard");
        }

        if (EhCliente())
        {
            TempData["Sucesso"] = "Você concorda com a Política de Privacidade. Seu acesso permanece liberado.";
            return RedirectToAction(nameof(Perfil));
        }

        TempData["Sucesso"] = "Concordância registrada. Você já pode entrar no portal.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize(Policy = "Cliente")]
    [HttpGet]
    public async Task<IActionResult> RecusarPoliticaPrivacidade()
    {
        var clienteId = User.FindFirst("clienteId")?.Value;
        var model = new RecusarPoliticaViewModel();

        if (!string.IsNullOrEmpty(clienteId))
        {
            var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

            try
            {
                var resp = await client.GetAsync($"api/cliente/perfil/{clienteId}");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var dados = JsonSerializer.Deserialize<ClientePerfilDto>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (dados != null)
                        model.AgendamentosFuturos = dados.AgendamentosFuturos;
                }
            }
            catch { }
        }

        return View(model);
    }

    [Authorize(Policy = "Cliente")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecusarPoliticaPrivacidade(RecusarPoliticaViewModel model)
    {
        return await ExcluirContaAsync();
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cliente");
        return RedirectToAction("Login");
    }

    [Authorize(Policy = "Cliente")]
    [HttpGet]
    public IActionResult TrocarSenha()
    {
        var model = new TrocarSenhaViewModel
        {
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty
        };
        return View(model);
    }

    [Authorize(Policy = "Cliente")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TrocarSenha(TrocarSenhaViewModel model)
    {
        model.Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

        if (model.SenhaNova != model.SenhaNovaConfirmacao)
        {
            model.Erro = "A nova senha e a confirmação não conferem.";
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.SenhaAtual) || string.IsNullOrWhiteSpace(model.SenhaNova))
        {
            model.Erro = "Preencha todos os campos.";
            return View(model);
        }

        if (!ValidarSenhaRequisitos(model.SenhaNova))
        {
            model.Erro = "A nova senha não atende aos requisitos: mínimo 6 caracteres, com maiúscula, minúscula, número e caractere especial.";
            return View(model);
        }

        if (model.SenhaNova == model.SenhaAtual)
        {
            model.Erro = "A nova senha não pode ser igual à senha atual.";
            return View(model);
        }

        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

        var payload = JsonSerializer.Serialize(new
        {
            email = model.Email,
            senhaAtual = model.SenhaAtual,
            senhaNova = model.SenhaNova
        });

        var resp = await client.PostAsync(
            "api/cliente/trocarsenha",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            model.Erro = "Não foi possível alterar a senha. Verifique a senha atual.";
            try
            {
                var err = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
                if (err != null && err.ContainsKey("message") && !string.IsNullOrWhiteSpace(err["message"]))
                    model.Erro = err["message"];
            }
            catch
            {
            }
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.EmailSecundario))
        {
            var clienteId = User.FindFirst("clienteId")?.Value;
            if (!string.IsNullOrEmpty(clienteId))
            {
                var payloadSec = JsonSerializer.Serialize(new
                {
                    clienteId,
                    emailSecundario = model.EmailSecundario.Trim()
                });
                await client.PostAsync(
                    "api/cliente/definir-email-secundario",
                    new StringContent(payloadSec, Encoding.UTF8, "application/json"));
            }
        }

        TempData["PoliticaObrigatoria"] = "1";

        return RedirectToAction(nameof(PoliticaPrivacidade));
    }

    [Authorize(Policy = "Cliente")]
    [HttpGet]
    public async Task<IActionResult> Perfil()
    {
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");
        var clienteId = User.FindFirst("clienteId")?.Value;

        var model = new PerfilViewModel
        {
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            Mensagem = TempData["Sucesso"]?.ToString(),
            Erro = TempData["Erro"]?.ToString()
        };

        if (!string.IsNullOrEmpty(clienteId))
        {
            var resp = await client.GetAsync($"api/cliente/perfil/{clienteId}");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var dados = JsonSerializer.Deserialize<ClientePerfilDto>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (dados != null)
                {
                    if (!string.IsNullOrWhiteSpace(dados.Email))
                        model.Email = dados.Email;
                    model.EmailSecundario = dados.EmailSecundario ?? string.Empty;
                    model.AgendamentosFuturos = dados.AgendamentosFuturos;
                }
            }
        }

        return View(model);
    }

    [Authorize(Policy = "Cliente")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Perfil(PerfilViewModel model)
    {
        model.Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");
        var clienteId = User.FindFirst("clienteId")?.Value;

        if (string.IsNullOrWhiteSpace(model.EmailSecundario))
        {
            model.Erro = "Informe o e-mail de recuperação.";
            return View(model);
        }

        if (string.IsNullOrEmpty(clienteId))
        {
            model.Erro = "Não foi possível identificar seu perfil.";
            return View(model);
        }

        var payload = JsonSerializer.Serialize(new
        {
            clienteId = Guid.Parse(clienteId),
            emailSecundario = model.EmailSecundario.Trim()
        });

        var resp = await client.PostAsync(
            "api/cliente/definir-email-secundario",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        if (!resp.IsSuccessStatusCode)
        {
            model.Erro = "Não foi possível salvar o e-mail de recuperação. Verifique o e-mail informado.";
            return View(model);
        }

        var corpo = await resp.Content.ReadAsStringAsync();
        var r = JsonSerializer.Deserialize<Dictionary<string, string>>(corpo);
        model.Mensagem = r != null && r.ContainsKey("message") ? r["message"] : "E-mail de recuperação salvo com sucesso.";
        return View(model);
    }

    [Authorize(Policy = "Cliente")]
    [HttpGet]
    public async Task<IActionResult> EncerrarConta()
    {
        var clienteId = User.FindFirst("clienteId")?.Value;
        var model = new EncerrarContaViewModel();

        if (!string.IsNullOrEmpty(clienteId))
        {
            var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

            try
            {
                var resp = await client.GetAsync($"api/cliente/perfil/{clienteId}");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var dados = JsonSerializer.Deserialize<ClientePerfilDto>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (dados != null)
                        model.AgendamentosFuturos = dados.AgendamentosFuturos;
                }
            }
            catch { }
        }

        return View(model);
    }

    [Authorize(Policy = "Cliente")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EncerrarConta(EncerrarContaViewModel model)
    {
        if (!string.Equals(model.Confirmacao?.Trim(), "ENCERRAR", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Erro"] = "Digite ENCERRAR para confirmar o encerramento da conta.";
            return RedirectToAction(nameof(EncerrarConta));
        }

        return await ExcluirContaAsync();
    }

    private async Task<IActionResult> ExcluirContaAsync()
    {
        var clienteId = User.FindFirst("clienteId")?.Value;

        if (string.IsNullOrEmpty(clienteId) || !Guid.TryParse(clienteId, out var id))
        {
            TempData["Erro"] = "Não foi possível identificar seu perfil.";
            return RedirectToAction(nameof(Perfil));
        }

        var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");

        try
        {
            var resp = await client.DeleteAsync($"api/cliente/delete-conta/{id}");

            if (!resp.IsSuccessStatusCode)
            {
                TempData["Erro"] = "Não foi possível encerrar a conta. Tente novamente.";
                return RedirectToAction(nameof(Perfil));
            }

            var corpo = await resp.Content.ReadAsStringAsync();
            var resultado = JsonSerializer.Deserialize<EncerramentoResposta>(corpo,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            await HttpContext.SignOutAsync("Cliente");

            TempData["Sucesso"] = string.IsNullOrWhiteSpace(resultado?.Mensagem)
                ? "Sua conta foi encerrada e seus dados de acesso foram removidos."
                : resultado.Mensagem;

            return RedirectToAction(nameof(Login));
        }
        catch
        {
            TempData["Erro"] = "Não foi possível encerrar a conta. Verifique sua conexão.";
            return RedirectToAction(nameof(Perfil));
        }
    }

    private bool EhCliente() => User.FindFirst("tipo")?.Value == "cliente";

    private static bool ValidarSenhaRequisitos(string senha)
    {
        if (string.IsNullOrWhiteSpace(senha) || senha.Length < 6)
            return false;
        if (!senha.Any(char.IsUpper))
            return false;
        if (!senha.Any(char.IsLower))
            return false;
        if (!senha.Any(char.IsDigit))
            return false;
        if (!senha.Any(c => !char.IsLetterOrDigit(c)))
            return false;
        return true;
    }
}

