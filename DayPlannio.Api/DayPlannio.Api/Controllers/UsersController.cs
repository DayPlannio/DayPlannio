using DayPlannio.Api.Helpers;
using DayPlannio.Api.Models;
using DayPlannio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DayPlannio.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly LogService _logService;
        private readonly AzureBlobStorageService _blobService;
        private readonly EmailService _emailService;
        private readonly ContextMongodb _context = new ContextMongodb();
        public UsersController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, LogService logService, AzureBlobStorageService blobService, EmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logService = logService;
            _blobService = blobService;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var usuarios = _userManager.Users.ToList();
            return Ok(usuarios);
        }

        [HttpPost("create")]
        [AllowAnonymous]
        public async Task<IActionResult> Create([FromBody] User user)
        {
            if (user.Senha != user.ConfirmeSenha)
                return BadRequest(new { message = "As senhas não coincidem." });

            if (await _userManager.FindByEmailAsync(user.Email) != null)
                return BadRequest(new { message = "Este e-mail já está cadastrado. Tente fazer login." });

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

            var escolheuTeste = user.UsarTesteGratuito;
            var escolheuPlano = EhPlanoValido(user.Plano);

            if (!escolheuTeste && !escolheuPlano)
                return BadRequest(new { message = "Escolha um plano para continuar o cadastro." });

            var appuser = new ApplicationUser
            {
                UserName = userNameFinal,
                Email = user.Email,
                NomeCompleto = user.NomeCompleto,
                Telefone = user.Telefone,
                Plano = null,
                PlanoAtivo = false,
                PlanoOrigem = "cadastro",
                CriadoEm = DateTime.UtcNow
            };

            if (escolheuTeste)
            {
                appuser.Plano = "Full";
                appuser.PlanoAtivo = true;
                appuser.PlanoExpiraEm = DateTime.UtcNow.AddDays(7);
                appuser.PlanoOrigem = "trial";
            }
            else
            {
                appuser.PlanoPendente = PlanoService.Normalizar(user.Plano);
                appuser.PlanoSolicitadoEm = DateTime.UtcNow;
            }

            var result = await _userManager.CreateAsync(appuser, user.Senha);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(appuser, isPersistent: false);

                var planoInicial = appuser.PlanoOrigem == "trial"
                    ? "Full (teste gratuito de 7 dias)"
                    : appuser.PlanoPendente != null
                        ? $"{PlanoService.NomeExibicao(appuser.PlanoPendente)} (solicitado, aguardando aprovação)"
                        : "Sem assinatura";

                await _logService.RegistrarAsync(
                    "prestador_criado", $"Prestador '{user.NomeCompleto}' se cadastrou no plano {planoInicial}",
                    "mobile", appuser.Id.ToString(), user.NomeCompleto, user.Email);

                return Ok(new
                {
                    message = "Usuário cadastrado com sucesso.",
                    id = appuser.Id,
                    plano = appuser.PlanoOrigem == "trial" ? "Full" : null,
                    testeGratuito = appuser.PlanoOrigem == "trial",
                    planoSolicitado = appuser.PlanoPendente
                });
            }

            var errors = result.Errors.Select(e =>
            {
                var desc = e.Description;
                if (desc.Contains("is already taken"))
                    return "Já existe um usuário com este nome.";
                return desc;
            });
            return BadRequest(new { errors });
        }

        [HttpPut("edit/{id}")]
        public async Task<IActionResult> Edit(Guid id, [FromBody] EditUserDto user)
        {
            var identityUser = await _userManager.FindByIdAsync(id.ToString());
            if (identityUser == null) return NotFound(new { message = "Usuário não encontrado." });

            identityUser.NomeCompleto = user.NomeCompleto ?? identityUser.NomeCompleto;
            identityUser.Email = user.Email ?? identityUser.Email;
            identityUser.Telefone = user.Telefone;
            identityUser.Cidade = user.Cidade;
            if (user.CidadeVisivel.HasValue) identityUser.CidadeVisivel = user.CidadeVisivel.Value;
            if (user.TelefoneVisivel.HasValue) identityUser.TelefoneVisivel = user.TelefoneVisivel.Value;

            string userName = (user.NomeCompleto ?? identityUser.NomeCompleto ?? "user").Replace(" ", "");
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
            {
                await _logService.RegistrarAsync(
                    "prestador_editado", $"Prestador '{user.NomeCompleto}' atualizado",
                    "mobile", id.ToString());
                return Ok(new { message = "Usuário atualizado com sucesso." });
            }

            var errors = result.Errors.Select(e =>
            {
                var desc = e.Description;
                if (desc.Contains("is already taken"))
                    return "Já existe um usuário com este nome.";
                return desc;
            });
            return BadRequest(new { errors });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "Usuário não encontrado." });

            var fotos = await _context.Foto
                .Find(Builders<Foto>.Filter.Eq(f => f.PrestadorId, user.Id))
                .ToListAsync();

            foreach (var foto in fotos)
            {
                try
                {
                    await _blobService.DeleteAsync(foto.Url);
                }
                catch
                {
                }
            }

            await _context.Foto.DeleteManyAsync(Builders<Foto>.Filter.Eq(f => f.PrestadorId, user.Id));
            var clientesDoPrestador = await ContaPrestadorHelper.ListarClientesAsync(_context, user.Id);

            await _context.Cliente.DeleteManyAsync(Builders<Cliente>.Filter.Eq(c => c.UsuarioId, user.Id));
            await _context.TipoServico.DeleteManyAsync(Builders<TipoServico>.Filter.Eq(s => s.UsuarioId, user.Id));
            await _context.Agendamento.DeleteManyAsync(Builders<Agendamento>.Filter.Eq(a => a.UsuarioId, user.Id));
            await _context.Financeiro.DeleteManyAsync(Builders<Financeiro>.Filter.Eq(f => f.UsuarioId, user.Id));
            await _context.Notificacao.DeleteManyAsync(Builders<Notificacao>.Filter.Eq(n => n.UsuarioId, user.Id));

            var filtroLog = Builders<Log>.Filter.Or(
                Builders<Log>.Filter.Eq(l => l.UsuarioId, user.Id.ToString()),
                Builders<Log>.Filter.Eq(l => l.UsuarioNome, user.NomeCompleto),
                Builders<Log>.Filter.Eq(l => l.Email, user.Email));

            await _context.Log.DeleteManyAsync(filtroLog);

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { errors });
            }

            ContaPrestadorHelper.AvisarClientesPorEmail(_emailService, clientesDoPrestador, user.NomeCompleto);

            return Ok(new { message = "Conta encerrada. Seus dados foram removidos." });
        }

        [HttpGet("meu-perfil/{id}")]
        public async Task<IActionResult> MeuPerfil(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new { message = "Id inválido." });

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound(new { message = "Usuário não encontrado." });

            await PlanoService.RenovarSeNecessarioAsync(user, _context);

return Ok(new
            {
                id = user.Id,
                nomeCompleto = user.NomeCompleto,
                email = user.Email,
                telefone = user.Telefone,
                cidade = user.Cidade,
                cidadeVisivel = user.CidadeVisivel,
                telefoneVisivel = user.TelefoneVisivel,
                plano = user.Plano,
                planoAtivo = user.PlanoAtivo,
                planoExpiraEm = user.PlanoExpiraEm,
                planoOrigem = user.PlanoOrigem,
                planoPendente = user.PlanoPendente,
                planoSolicitadoEm = user.PlanoSolicitadoEm,
                renovacaoAutomatica = user.RenovacaoAutomatica,
                planoCanceladoEm = user.PlanoCanceladoEm,
                planoEfetivo = PlanoService.ObterPlanoEfetivo(user)
            });
        }

        private static bool EhPlanoValido(string? plano) =>
            !string.IsNullOrWhiteSpace(plano) &&
            PlanoService.Normalizar(plano) is PlanoService.Basico or PlanoService.Profissional or PlanoService.Full;

        [HttpPost("plano/renovacao")]
        [AllowAnonymous]
        public async Task<IActionResult> DefinirRenovacao([FromBody] RenovacaoPlanoDto dto)
        {
            if (string.IsNullOrEmpty(dto.UsuarioId))
                return BadRequest(new { message = "Usuário não informado." });

            var user = await _userManager.FindByIdAsync(dto.UsuarioId);
            if (user == null)
                return NotFound(new { message = "Usuário não encontrado." });

            await PlanoService.RenovarSeNecessarioAsync(user, _context);

            if (dto.RenovacaoAutomatica)
            {
                user.RenovacaoAutomatica = true;
                user.PlanoCanceladoEm = null;
            }
            else
            {
                user.RenovacaoAutomatica = false;
                user.PlanoCanceladoEm = user.PlanoExpiraEm ?? DateTime.UtcNow;
            }

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(new { message = "Não foi possível atualizar a assinatura." });

            await _logService.RegistrarAsync(
                dto.RenovacaoAutomatica ? "renovacao_ativada" : "assinatura_cancelada",
                dto.RenovacaoAutomatica
                    ? $"Prestador '{user.NomeCompleto}' reativou a renovação automática do plano {user.Plano}"
                    : $"Prestador '{user.NomeCompleto}' cancelou a renovação do plano {user.Plano}. Acesso até {user.PlanoCanceladoEm:dd/MM/yyyy}",
                "mobile", user.Id.ToString(), user.NomeCompleto, user.Email);

            if (dto.RenovacaoAutomatica)
            {
                return Ok(new
                {
                    message = "Renovação automática reativada.",
                    renovacaoAutomatica = true,
                    plano = user.Plano,
                    planoExpiraEm = user.PlanoExpiraEm,
                    planoCanceladoEm = (DateTime?)null
                });
            }

            var dataCancelamento = user.PlanoCanceladoEm.HasValue
                ? user.PlanoCanceladoEm.Value.ToLocalTime().ToString("dd/MM/yyyy")
                : null;

            return Ok(new
            {
                message = $"Seu plano {user.Plano} será cancelado em {dataCancelamento}.",
                renovacaoAutomatica = false,
                plano = user.Plano,
                planoExpiraEm = user.PlanoExpiraEm,
                planoCanceladoEm = user.PlanoCanceladoEm
            });
        }

        [HttpPost("plano/solicitar")]
        [AllowAnonymous]
        public async Task<IActionResult> SolicitarPlano([FromBody] SolicitarPlanoDto dto)
        {
            if (string.IsNullOrEmpty(dto.UsuarioId))
                return BadRequest(new { message = "Usuário não informado." });

            var user = await _userManager.FindByIdAsync(dto.UsuarioId);
            if (user == null)
                return NotFound(new { message = "Usuário não encontrado." });

            if (string.IsNullOrWhiteSpace(dto.Plano) || dto.Plano.Equals("Nenhum", StringComparison.OrdinalIgnoreCase))
            {
                var planoAnterior = user.PlanoPendente;

                user.PlanoPendente = null;
                user.PlanoSolicitadoEm = null;
                var cancelamento = await _userManager.UpdateAsync(user);

                if (!cancelamento.Succeeded)
                    return BadRequest(new { message = "Não foi possível cancelar a solicitação." });

                await _logService.RegistrarAsync(
                    "plano_solicitacao_cancelada",
                    $"Prestador '{user.NomeCompleto}' cancelou a solicitação do plano {planoAnterior}",
                    "mobile", user.Id.ToString(), user.NomeCompleto, user.Email);

                return Ok(new { message = "Solicitação cancelada." });
            }

            if (!EhPlanoValido(dto.Plano))
                return BadRequest(new { message = "Plano inválido." });

            user.PlanoPendente = PlanoService.Normalizar(dto.Plano!);
            user.PlanoSolicitadoEm = DateTime.UtcNow;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(new { message = "Não foi possível solicitar o plano." });

            await _logService.RegistrarAsync(
                "plano_solicitado", $"Prestador '{user.NomeCompleto}' solicitou o plano {dto.Plano}",
                "mobile", user.Id.ToString(), user.NomeCompleto, user.Email);

            return Ok(new { message = "Solicitação enviada. Aguardando a aprovação do administrador." });
        }
    }

    public class SolicitarPlanoDto
    {
        public string? UsuarioId { get; set; }
        public string? Plano { get; set; }
    }

    public class RenovacaoPlanoDto
    {
        public string? UsuarioId { get; set; }
        public bool RenovacaoAutomatica { get; set; }
    }
}
