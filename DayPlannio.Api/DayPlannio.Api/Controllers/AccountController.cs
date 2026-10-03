using DayPlannio.Api.Models;
using DayPlannio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace DayPlannio.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly EmailService _emailService;
        private readonly LogService _logService;
        private readonly ContextMongodb _context = new ContextMongodb();

        public AccountController(UserManager<ApplicationUser> userManager,
                                 SignInManager<ApplicationUser> signInManager,
                                 EmailService emailService,
                                 LogService logService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _logService = logService;
        }

        private const int MaxTentativasCodigo = 5;

        private static bool CodigoCorreto(string? codigoArmazenado, string? codigoEnviado)
        {
            if (string.IsNullOrWhiteSpace(codigoArmazenado) || string.IsNullOrWhiteSpace(codigoEnviado))
                return false;

            var a = System.Text.Encoding.UTF8.GetBytes(codigoArmazenado);
            var b = System.Text.Encoding.UTF8.GetBytes(codigoEnviado);
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
        }

        private static string GerarCodigo()
        {
            var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(4);
            var valor = BitConverter.ToUInt32(bytes, 0) % 900000 + 100000;
            return valor.ToString();
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDTO model)
        {
            var appUser = await _userManager.FindByEmailAsync(model.Email);

            if (appUser == null)
                return Unauthorized(new { message = "Credenciais inválidas." });

            var result = await _signInManager.PasswordSignInAsync(appUser, model.Senha, false, false);

            if (result.Succeeded)
            {
                await _logService.RegistrarAsync(
                    "login_prestador", $"Prestador '{appUser.NomeCompleto}' fez login",
                    "mobile", appUser.Id.ToString(), appUser.NomeCompleto, appUser.Email);

                return Ok(new { message = "Login realizado com sucesso.", userId = appUser.Id });
            }

            return Unauthorized(new { message = "Credenciais inválidas." });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            if (string.IsNullOrEmpty(model.Email))
                return BadRequest(new { message = "Informe o e-mail." });

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Ok(new { message = "Se o e-mail estiver cadastrado, você receberá as instruções." });

            var emailKey = model.Email.Trim().ToLowerInvariant();

            bool reenviado = false;
            string code;
            if (user.CodigoRecuperacaoExpira != null
                && DateTime.UtcNow <= user.CodigoRecuperacaoExpira
                && !string.IsNullOrWhiteSpace(user.CodigoRecuperacao))
            {
                reenviado = true;
                code = user.CodigoRecuperacao!;
                var filtroReset = Builders<ApplicationUser>.Filter.Eq("_id", user.Id);
                var updateReset = Builders<ApplicationUser>.Update
                    .Set(u => u.TentativasCodigo, 0)
                    .Set(u => u.CodigoBloqueado, false);
                await _context.ApplicationUsers.UpdateOneAsync(filtroReset, updateReset);
            }
            else
            {
                code = GerarCodigo();
                user.CodigoRecuperacao = code;
                user.CodigoRecuperacaoExpira = DateTime.UtcNow.AddMinutes(15);
                user.TentativasCodigo = 0;
                user.CodigoBloqueado = false;
                var filtro = Builders<ApplicationUser>.Filter.Eq("_id", user.Id);
                var update = Builders<ApplicationUser>.Update
                    .Set(u => u.CodigoRecuperacao, code)
                    .Set(u => u.CodigoRecuperacaoExpira, DateTime.UtcNow.AddMinutes(15))
                    .Set(u => u.TentativasCodigo, 0)
                    .Set(u => u.CodigoBloqueado, false);
                await _context.ApplicationUsers.UpdateOneAsync(filtro, update);
            }

            string corpo = $@"
            <h2>Redefinição de Senha</h2>
            <p>Use o código abaixo para redefinir sua senha:</p>
            <h1 style='letter-spacing:10px;color:#006260;'>{code}</h1>
            <p>O código expira em 15 minutos.</p>
            <p>Se não solicitou, ignore este e-mail.</p>";

            await _emailService.SendEmailAsync(model.Email, "Redefinição de Senha - DayPlannio", corpo);

            await _logService.RegistrarAsync(
                "recuperacao_prestador", $"Prestador '{user.NomeCompleto}' solicitou recuperação de senha",
                "mobile", user.Id.ToString(), user.NomeCompleto, user.Email);

            return Ok(new
            {
                message = reenviado
                ? "Reenviamos o mesmo código. Ele continua válido por 15 minutos."
                : "Se o e-mail estiver cadastrado, você receberá as instruções."
            });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPassword model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return BadRequest(new { message = "Usuário não encontrado." });

            if (user.CodigoBloqueado)
                return BadRequest(new { message = "Número de tentativas excedido. Solicite um novo código." });

            if (user.CodigoRecuperacaoExpira != null && DateTime.UtcNow > user.CodigoRecuperacaoExpira)
            {
                var filtroExp = Builders<ApplicationUser>.Filter.Eq("_id", user.Id);
                var updateExp = Builders<ApplicationUser>.Update
                    .Set(u => u.CodigoRecuperacao, (string?)null)
                    .Set(u => u.CodigoRecuperacaoExpira, (DateTime?)null)
                    .Set(u => u.TentativasCodigo, 0)
                    .Set(u => u.CodigoBloqueado, false);
                await _context.ApplicationUsers.UpdateOneAsync(filtroExp, updateExp);
                return BadRequest(new { message = "Código inválido ou expirado." });
            }

            if (!CodigoCorreto(user.CodigoRecuperacao, model.Code))
            {
                var novasTentativas = user.TentativasCodigo + 1;
                if (novasTentativas >= MaxTentativasCodigo)
                {
                    var filtroBloq = Builders<ApplicationUser>.Filter.Eq("_id", user.Id);
                    var updateBloq = Builders<ApplicationUser>.Update
                        .Inc(u => u.TentativasCodigo, 1)
                        .Set(u => u.CodigoRecuperacao, (string?)null)
                        .Set(u => u.CodigoRecuperacaoExpira, (DateTime?)null)
                        .Set(u => u.CodigoBloqueado, true);
                    await _context.ApplicationUsers.UpdateOneAsync(filtroBloq, updateBloq);
                    return BadRequest(new { message = "Número de tentativas excedido. Solicite um novo código." });
                }

                var filtroTent = Builders<ApplicationUser>.Filter.Eq("_id", user.Id);
                var updateTent = Builders<ApplicationUser>.Update.Inc(u => u.TentativasCodigo, 1);
                await _context.ApplicationUsers.UpdateOneAsync(filtroTent, updateTent);
                return BadRequest(new { message = "Código inválido ou expirado." });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                user.CodigoRecuperacao = null;
                user.CodigoRecuperacaoExpira = null;
                user.TentativasCodigo = 0;
                user.CodigoBloqueado = false;
                var filtro = Builders<ApplicationUser>.Filter.Eq("_id", user.Id);
                var update = Builders<ApplicationUser>.Update
                    .Set(u => u.CodigoRecuperacao, (string?)null)
                    .Set(u => u.CodigoRecuperacaoExpira, (DateTime?)null)
                    .Set(u => u.TentativasCodigo, 0)
                    .Set(u => u.CodigoBloqueado, false);
                await _context.ApplicationUsers.UpdateOneAsync(filtro, update);

                await _logService.RegistrarAsync(
                    "senha_redefinida_prestador", $"Prestador '{user.NomeCompleto}' redefiniu a senha",
                    "mobile", user.Id.ToString(), user.NomeCompleto, user.Email);

                return Ok(new { message = "Senha redefinida com sucesso." });
            }

            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }

        [HttpPost("verify-code")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyCode([FromBody] ResetPassword model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return BadRequest(new { message = "Código inválido ou expirado." });

            if (user.CodigoBloqueado)
                return BadRequest(new { message = "Número de tentativas excedido. Solicite um novo código." });

            if (user.CodigoRecuperacaoExpira != null && DateTime.UtcNow > user.CodigoRecuperacaoExpira)
            {
                var filtroExp = Builders<ApplicationUser>.Filter.Eq("_id", user.Id);
                var updateExp = Builders<ApplicationUser>.Update
                    .Set(u => u.CodigoRecuperacao, (string?)null)
                    .Set(u => u.CodigoRecuperacaoExpira, (DateTime?)null)
                    .Set(u => u.TentativasCodigo, 0)
                    .Set(u => u.CodigoBloqueado, false);
                await _context.ApplicationUsers.UpdateOneAsync(filtroExp, updateExp);
                return BadRequest(new { message = "Código inválido ou expirado." });
            }

            if (!CodigoCorreto(user.CodigoRecuperacao, model.Code))
            {
                var novasTentativas = user.TentativasCodigo + 1;
                if (novasTentativas >= MaxTentativasCodigo)
                {
                    var filtroBloq = Builders<ApplicationUser>.Filter.Eq("_id", user.Id);
                    var updateBloq = Builders<ApplicationUser>.Update
                        .Inc(u => u.TentativasCodigo, 1)
                        .Set(u => u.CodigoRecuperacao, (string?)null)
                        .Set(u => u.CodigoRecuperacaoExpira, (DateTime?)null)
                        .Set(u => u.CodigoBloqueado, true);
                    await _context.ApplicationUsers.UpdateOneAsync(filtroBloq, updateBloq);
                    return BadRequest(new { message = "Número de tentativas excedido. Solicite um novo código." });
                }

                var filtroTent = Builders<ApplicationUser>.Filter.Eq("_id", user.Id);
                var updateTent = Builders<ApplicationUser>.Update.Inc(u => u.TentativasCodigo, 1);
                await _context.ApplicationUsers.UpdateOneAsync(filtroTent, updateTent);
                return BadRequest(new { message = "Código inválido ou expirado." });
            }

            return Ok(new { message = "Código válido." });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var appUser = await _userManager.GetUserAsync(User);

            await _signInManager.SignOutAsync();

            await _logService.RegistrarAsync(
                "logout_prestador", $"Prestador '{appUser?.NomeCompleto}' fez logout",
                "mobile", appUser?.Id.ToString(), appUser?.NomeCompleto, appUser?.Email);

            return Ok(new { message = "Logout realizado com sucesso." });
        }
    }
}