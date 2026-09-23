using DayPlannio.Api.Models;
using DayPlannio.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DayPlannio.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly EmailService _emailService;

        public AccountController(UserManager<ApplicationUser> userManager,
                                 SignInManager<ApplicationUser> signInManager,
                                 EmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
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
        public async Task<IActionResult> Login([FromBody] LoginDTO model)
        {
            var appUser = await _userManager.FindByEmailAsync(model.Email);

            if (appUser == null)
                return Unauthorized(new { message = "Credenciais inválidas." });

            var result = await _signInManager.PasswordSignInAsync(appUser, model.Senha, false, false);

            if (result.Succeeded)
                return Ok(new { message = "Login realizado com sucesso.", userId = appUser.Id });

            return Unauthorized(new { message = "Credenciais inválidas." });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            if (string.IsNullOrEmpty(model.Email))
                return BadRequest(new { message = "Informe o e-mail." });

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Ok(new { message = "Se o e-mail estiver cadastrado, você receberá as instruções." });

            bool reenviado = false;
            string code;
            if (user.CodigoRecuperacaoExpira != null
                && DateTime.UtcNow <= user.CodigoRecuperacaoExpira
                && !string.IsNullOrWhiteSpace(user.CodigoRecuperacao))
            {
                reenviado = true;
                code = user.CodigoRecuperacao!;
            }
            else
            {
                code = GerarCodigo();
                user.CodigoRecuperacao = code;
                user.CodigoRecuperacaoExpira = DateTime.UtcNow.AddMinutes(15);
                user.TentativasCodigo = 0;
                await _userManager.UpdateAsync(user);
            }

            string corpo = $@"
            <h2>Redefinição de Senha</h2>
            <p>Use o código abaixo para redefinir sua senha:</p>
            <h1 style='letter-spacing:10px;color:#006260;'>{code}</h1>
            <p>O código expira em 15 minutos.</p>
            <p>Se não solicitou, ignore este e-mail.</p>";

            await _emailService.SendEmailAsync(model.Email, "Redefinição de Senha - DayPlannio", corpo);

            return Ok(new
            {
                message = reenviado
                ? "Reenviamos o mesmo código. Ele continua válido por 15 minutos."
                : "Se o e-mail estiver cadastrado, você receberá as instruções."
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPassword model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return BadRequest(new { message = "Usuário não encontrado." });

            if (user.CodigoRecuperacaoExpira != null && DateTime.UtcNow > user.CodigoRecuperacaoExpira)
            {
                user.CodigoRecuperacao = null;
                user.CodigoRecuperacaoExpira = null;
                user.TentativasCodigo = 0;
                await _userManager.UpdateAsync(user);
                return BadRequest(new { message = "Código inválido ou expirado." });
            }

            if (user.TentativasCodigo >= MaxTentativasCodigo)
            {
                user.CodigoRecuperacao = null;
                user.CodigoRecuperacaoExpira = null;
                user.TentativasCodigo = 0;
                await _userManager.UpdateAsync(user);
                return BadRequest(new { message = "Número de tentativas excedido. Solicite um novo código." });
            }

            if (!CodigoCorreto(user.CodigoRecuperacao, model.Code))
            {
                user.TentativasCodigo++;
                await _userManager.UpdateAsync(user);
                return BadRequest(new { message = "Código inválido ou expirado." });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                user.CodigoRecuperacao = null;
                user.CodigoRecuperacaoExpira = null;
                user.TentativasCodigo = 0;
                await _userManager.UpdateAsync(user);

                return Ok(new { message = "Senha redefinida com sucesso." });
            }

            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }

        [HttpPost("verify-code")]
        public async Task<IActionResult> VerifyCode([FromBody] ResetPassword model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return BadRequest(new { message = "Código inválido ou expirado." });

            if (user.CodigoRecuperacaoExpira != null && DateTime.UtcNow > user.CodigoRecuperacaoExpira)
            {
                user.CodigoRecuperacao = null;
                user.CodigoRecuperacaoExpira = null;
                user.TentativasCodigo = 0;
                await _userManager.UpdateAsync(user);
                return BadRequest(new { message = "Código inválido ou expirado." });
            }

            if (user.TentativasCodigo >= MaxTentativasCodigo)
            {
                user.CodigoRecuperacao = null;
                user.CodigoRecuperacaoExpira = null;
                user.TentativasCodigo = 0;
                await _userManager.UpdateAsync(user);
                return BadRequest(new { message = "Número de tentativas excedido. Solicite um novo código." });
            }

            if (!CodigoCorreto(user.CodigoRecuperacao, model.Code))
            {
                user.TentativasCodigo++;
                await _userManager.UpdateAsync(user);
                return BadRequest(new { message = "Código inválido ou expirado." });
            }

            return Ok(new { message = "Código válido." });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { message = "Logout realizado com sucesso." });
        }
    }
}