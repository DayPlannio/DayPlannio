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
        private static Dictionary<string, (string Code, DateTime Expiry)> _resetCodes = new();

        public AccountController(UserManager<ApplicationUser> userManager,
                                 SignInManager<ApplicationUser> signInManager,
                                 EmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
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

            var code = new Random().Next(100000, 999999).ToString();
            _resetCodes[model.Email] = (code, DateTime.UtcNow.AddMinutes(15));

            string corpo = $@"
            <h2>Redefinição de Senha</h2>
            <p>Use o código abaixo para redefinir sua senha:</p>
            <h1 style='letter-spacing:10px;color:#006260;'>{code}</h1>
            <p>O código expira em 15 minutos.</p>
            <p>Se não solicitou, ignore este e-mail.</p>";

            await _emailService.SendEmailAsync(model.Email, "Redefinição de Senha - DayPlannio", corpo);

            return Ok(new { message = "Se o e-mail estiver cadastrado, você receberá as instruções." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPassword model)
        {
            if (!_resetCodes.TryGetValue(model.Email, out var codeData))
                return BadRequest(new { message = "Código inválido ou expirado." });

            if (codeData.Code != model.Code || DateTime.UtcNow > codeData.Expiry)
            {
                _resetCodes.Remove(model.Email);
                return BadRequest(new { message = "Código inválido ou expirado." });
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return BadRequest(new { message = "Usuário não encontrado." });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                _resetCodes.Remove(model.Email);
                return Ok(new { message = "Senha redefinida com sucesso." });
            }

            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }

        [HttpPost("verify-code")]
        public IActionResult VerifyCode([FromBody] ResetPassword model)
        {
            if (!_resetCodes.TryGetValue(model.Email, out var codeData))
                return BadRequest(new { message = "Código inválido ou expirado." });

            if (codeData.Code != model.Code || DateTime.UtcNow > codeData.Expiry)
            {
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