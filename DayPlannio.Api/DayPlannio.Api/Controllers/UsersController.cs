using DayPlannio.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DayPlannio.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var usuarios = _userManager.Users.ToList();
            return Ok(usuarios);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] User user)
        {
            if (user.Senha != user.ConfirmeSenha)
                return BadRequest(new { message = "As senhas não coincidem." });

            string userName = user.NomeCompleto.Replace(" ", "");
            var normalizedString = userName.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in normalizedString)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            userName = sb.ToString().Normalize(NormalizationForm.FormC);
            userName = Regex.Replace(userName, @"[^a-zA-Z0-9]", "");

            if (string.IsNullOrWhiteSpace(userName))
                return BadRequest(new { message = "Não foi possível gerar um nome de usuário válido." });

            var userNameFinal = userName;
            int contador = 1;
            while (await _userManager.FindByNameAsync(userNameFinal) != null)
            {
                userNameFinal = $"{userName}{contador}";
                contador++;
            }

            var appuser = new ApplicationUser
            {
                UserName = userNameFinal,
                Email = user.Email,
                NomeCompleto = user.NomeCompleto,
                Telefone = user.Telefone
            };

            var result = await _userManager.CreateAsync(appuser, user.Senha);

            if (result.Succeeded)
                return Ok(new { message = "Usuário cadastrado com sucesso.", id = appuser.Id });

            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }

        [HttpPut("edit/{id}")]
        public async Task<IActionResult> Edit(Guid id, [FromBody] EditUserDto user)
        {
            var identityUser = await _userManager.FindByIdAsync(id.ToString());
            if (identityUser == null) return NotFound(new { message = "Usuário não encontrado." });

            identityUser.NomeCompleto = user.NomeCompleto;
            identityUser.Email = user.Email;
            identityUser.Telefone = user.Telefone;

            string userName = user.NomeCompleto.Replace(" ", "");
            var normalizedString = userName.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in normalizedString)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            userName = sb.ToString().Normalize(NormalizationForm.FormC);
            userName = Regex.Replace(userName, @"[^a-zA-Z0-9]", "");
            identityUser.UserName = userName;
            identityUser.NormalizedUserName = userName.ToUpperInvariant();

            var result = await _userManager.UpdateAsync(identityUser);

            if (result.Succeeded)
                return Ok(new { message = "Usuário atualizado com sucesso." });

            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "Usuário não encontrado." });

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
                return Ok(new { message = "Usuário deletado com sucesso." });

            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }

        [HttpGet("meu-perfil/{id}")]
        public async Task<IActionResult> MeuPerfil(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new { message = "Id inválido." });

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound(new { message = "Usuário não encontrado." });

            return Ok(new
            {
                id = user.Id,
                nomeCompleto = user.NomeCompleto,
                email = user.Email,
                telefone = user.Telefone
            });
        }
    }
}