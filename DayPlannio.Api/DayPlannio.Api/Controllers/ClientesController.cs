using DayPlannio.Api.Helpers;
using DayPlannio.Api.Models;
using DayPlannio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace DayPlannio.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ClienteController : ControllerBase
    {
        private readonly ContextMongodb _context = new ContextMongodb();
        private readonly EmailService _emailService;
        private readonly LogService _logService;

        public ClienteController(EmailService emailService, LogService logService)
        {
            _emailService = emailService;
            _logService = logService;
        }

        [HttpGet("{usuarioId}")]
        public async Task<IActionResult> GetAll(Guid usuarioId)
        {
            var clientes = await _context.Cliente
                .Find(c => c.UsuarioId == usuarioId)
                .ToListAsync();

            return Ok(clientes);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] Cliente cliente)
        {
            if (cliente == null)
                return BadRequest(new { message = "Dados inválidos." });

            if (string.IsNullOrWhiteSpace(cliente.Nome))
                return BadRequest(new { message = "O nome é obrigatório." });

            if (string.IsNullOrWhiteSpace(cliente.Telefone))
                return BadRequest(new { message = "O telefone é obrigatório." });

            cliente.Id = Guid.NewGuid();
            cliente.CreatedAt = DateTime.UtcNow;
            cliente.Ativo = true;

            await _context.Cliente.InsertOneAsync(cliente);

            await _logService.RegistrarAsync(
                "cliente_criado", $"Cliente '{cliente.Nome}' cadastrado por prestador",
                "web", cliente.UsuarioId.ToString(), "prestador", null);

            return Ok(new
            {
                message = "Cliente cadastrado com sucesso.",
                id = cliente.Id
            });
        }

        [HttpPost("gerar-credenciais/{id}")]
        public async Task<IActionResult> GerarCredenciais(Guid id)
        {
            var existing = await _context.Cliente.Find(c => c.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Cliente não encontrado." });

            var acesso = await PlanoService.VerificarAsync(existing.UsuarioId, PlanoService.Full, _context);
            if (!acesso.autorizado)
                return StatusCode(403, new { message = acesso.mensagem });

            var prestador = await _context.ApplicationUsers
                .Find(u => u.Id == existing.UsuarioId)
                .FirstOrDefaultAsync();

            var nomePrestador = prestador?.NomeCompleto
                ?? prestador?.UserName
                ?? "prestador";

            var slugCliente = SenhaHelper.Slug(existing.Nome);
            var slugPrestador = SenhaHelper.Slug(PrimeiroNome(nomePrestador));

            var email = string.IsNullOrWhiteSpace(existing.Email)
                ? await GerarEmailUnico($"{slugCliente}_{slugPrestador}")
                : existing.Email;

            var senha = SenhaHelper.GerarSenhaProvisoria();

            existing.Email = email;
            existing.SenhaHash = SenhaHelper.GerarHash(senha);
            existing.PrimeiroAcesso = true;

            await _context.Cliente.ReplaceOneAsync(
                c => c.Id == id,
                existing);

            await _logService.RegistrarAsync(
                "credenciais_cliente_geradas", $"Credenciais de acesso geradas para cliente '{existing.Nome}'",
                "web", existing.UsuarioId.ToString(), nomePrestador, email);

            return Ok(new
            {
                message = "Acesso do cliente gerado com sucesso.",
                email,
                senhaProvisoria = senha
            });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginClienteDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Senha))
                return BadRequest(new { message = "E-mail e senha são obrigatórios." });

            var cliente = await _context.Cliente
                .Find(c => c.Email == dto.Email)
                .FirstOrDefaultAsync();

            if (cliente == null || !cliente.Ativo)
                return Unauthorized(new { message = "Credenciais inválidas." });

            if (!SenhaHelper.Verificar(dto.Senha, cliente.SenhaHash))
                return Unauthorized(new { message = "Credenciais inválidas." });

            await _logService.RegistrarAsync(
                "login_cliente", $"Cliente '{cliente.Nome}' fez login",
                "web", cliente.Id.ToString(), cliente.Nome, cliente.Email);

            return Ok(new
            {
                clienteId = cliente.Id,
                usuarioId = cliente.UsuarioId,
                email = cliente.Email,
                primeiroAcesso = cliente.PrimeiroAcesso
            });
        }

        [HttpPost("trocarsenha")]
        public async Task<IActionResult> TrocarSenha([FromBody] TrocarSenhaClienteDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email)
                || string.IsNullOrWhiteSpace(dto.SenhaAtual)
                || string.IsNullOrWhiteSpace(dto.SenhaNova))
                return BadRequest(new { message = "Todos os campos são obrigatórios." });

            var cliente = await _context.Cliente
                .Find(c => c.Email == dto.Email)
                .FirstOrDefaultAsync();

            if (cliente == null || !cliente.Ativo)
                return Unauthorized(new { message = "Credenciais inválidas." });

            if (!SenhaHelper.Verificar(dto.SenhaAtual, cliente.SenhaHash))
                return Unauthorized(new { message = "Senha atual inválida." });

            if (SenhaHelper.Verificar(dto.SenhaNova, cliente.SenhaHash))
                return BadRequest(new { message = "A nova senha não pode ser igual à senha atual." });

            if (!RequisitosSenhaOk(dto.SenhaNova))
                return BadRequest(new { message = "A nova senha não atende aos requisitos: mínimo 6 caracteres, com maiúscula, minúscula, número e caractere especial." });

            cliente.SenhaHash = SenhaHelper.GerarHash(dto.SenhaNova);
            cliente.PrimeiroAcesso = false;

            await _context.Cliente.ReplaceOneAsync(c => c.Id == cliente.Id, cliente);

            await _logService.RegistrarAsync(
                "senha_trocada", $"Cliente '{cliente.Nome}' trocou a senha",
                "web", cliente.Id.ToString(), cliente.Nome, cliente.Email);

            return Ok(new { message = "Senha alterada com sucesso." });
        }

        [HttpPost("definir-email-secundario")]
        public async Task<IActionResult> DefinirEmailSecundario([FromBody] DefinirEmailSecundarioDto dto)
        {
            if (dto.ClienteId == Guid.Empty || string.IsNullOrWhiteSpace(dto.EmailSecundario))
                return BadRequest(new { message = "Dados inválidos." });

            var cliente = await _context.Cliente
                .Find(c => c.Id == dto.ClienteId)
                .FirstOrDefaultAsync();

            if (cliente == null || !cliente.Ativo)
                return Unauthorized(new { message = "Cliente não encontrado." });

            var emailSecundario = dto.EmailSecundario.Trim().ToLowerInvariant();

            try
            {
                var addr = new System.Net.Mail.MailAddress(emailSecundario);
                if (addr.Address != emailSecundario)
                    throw new FormatException();
            }
            catch
            {
                return BadRequest(new { message = "E-mail secundário inválido." });
            }

            cliente.EmailSecundario = emailSecundario;
            await _context.Cliente.ReplaceOneAsync(c => c.Id == cliente.Id, cliente);

            await _logService.RegistrarAsync(
                "email_secundario_atualizado", $"Cliente '{cliente.Nome}' atualizou e-mail de recuperação",
                "web", cliente.Id.ToString(), cliente.Nome, cliente.Email);

            return Ok(new { message = "E-mail de recuperação salvo com sucesso." });
        }

        [HttpGet("perfil/{clienteId}")]
        public async Task<IActionResult> Perfil(Guid clienteId)
        {
            var cliente = await _context.Cliente
                .Find(c => c.Id == clienteId)
                .FirstOrDefaultAsync();

            if (cliente == null)
                return NotFound(new { message = "Cliente não encontrado." });

            var agendamentosFuturos = await ContarAgendamentosFuturosAsync(cliente.Id);

            return Ok(new
            {
                clienteId = cliente.Id,
                email = cliente.Email ?? string.Empty,
                emailSecundario = cliente.EmailSecundario ?? string.Empty,
                agendamentosFuturos
            });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordClienteDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { message = "Informe o e-mail." });

            var cliente = await _context.Cliente
                .Find(c => c.Email == dto.Email.Trim().ToLowerInvariant())
                .FirstOrDefaultAsync();

            if (cliente == null || !cliente.Ativo)
                return Ok(new { message = "Se o e-mail estiver cadastrado, você receberá as instruções." });

            var destino = cliente.EmailSecundario;
            if (string.IsNullOrWhiteSpace(destino))
                return BadRequest(new { message = "Nenhum e-mail de recuperação cadastrado. Contate o prestador." });

            var emailKey = dto.Email.Trim().ToLowerInvariant();

            bool reenviado = false;
            string code;
            if (cliente.CodigoRecuperacaoExpira != null
                            && DateTime.UtcNow <= cliente.CodigoRecuperacaoExpira
                            && !string.IsNullOrWhiteSpace(cliente.CodigoRecuperacao))
            {
                reenviado = true;
                code = cliente.CodigoRecuperacao!;
                cliente.TentativasCodigo = 0;
                cliente.CodigoBloqueado = false;
                await _context.Cliente.ReplaceOneAsync(c => c.Id == cliente.Id, cliente);
            }
            else
            {
                var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(4);
                code = (BitConverter.ToUInt32(bytes, 0) % 900000 + 100000).ToString();
                cliente.CodigoRecuperacao = code;
                cliente.CodigoRecuperacaoExpira = DateTime.UtcNow.AddMinutes(15);
                cliente.TentativasCodigo = 0;
                cliente.CodigoBloqueado = false;
                await _context.Cliente.ReplaceOneAsync(c => c.Id == cliente.Id, cliente);
            }

            string corpo = $@"
            <h2>Redefinição de Senha - DayPlannio</h2>
            <p>Olá, {cliente.Nome}! Use o código abaixo para redefinir a senha do seu acesso:</p>
            <h1 style='letter-spacing:10px;color:#006260;'>{code}</h1>
            <p>O código expira em 15 minutos.</p>
            <p>Se não foi você quem solicitou, ignore este e-mail.</p>";

            try
            {
                await _emailService.SendEmailAsync(destino, "Redefinição de Senha - DayPlannio", corpo);
            }
            catch
            {
                return BadRequest(new { message = "Não foi possível enviar o e-mail de recuperação. Tente novamente." });
            }

            await _logService.RegistrarAsync(
                "recuperacao_solicitada", $"Cliente '{cliente.Nome}' solicitou recuperação de senha",
                "web", cliente.Id.ToString(), cliente.Nome, cliente.Email);

            return Ok(new
            {
                message = reenviado
                ? "Reenviamos o mesmo código. Ele continua válido por 15 minutos."
                : "Se o e-mail estiver cadastrado, você receberá as instruções."
            });
        }

        private const int MaxTentativasCodigo = 5;

        private static bool RequisitosSenhaOk(string senha)
        {
            return senha.Length >= 6
                && senha.Any(char.IsUpper)
                && senha.Any(char.IsLower)
                && senha.Any(char.IsDigit)
                && senha.Any(c => !char.IsLetterOrDigit(c));
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordClienteDto dto)
        {
            var emailKey = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty;

            var cliente = await _context.Cliente
                .Find(c => c.Email == emailKey)
                .FirstOrDefaultAsync();

            if (cliente == null || !cliente.Ativo)
                return BadRequest(new { message = "Cliente não encontrado." });

            if (cliente.CodigoBloqueado)
                return BadRequest(new { message = "Número de tentativas excedido. Solicite um novo código." });

            if (cliente.CodigoRecuperacaoExpira != null && DateTime.UtcNow > cliente.CodigoRecuperacaoExpira)
            {
                cliente.CodigoRecuperacao = null;
                cliente.CodigoRecuperacaoExpira = null;
                cliente.TentativasCodigo = 0;
                cliente.CodigoBloqueado = false;
                await _context.Cliente.ReplaceOneAsync(c => c.Id == cliente.Id, cliente);
                return BadRequest(new { message = "Código inválido ou expirado." });
            }

            var codigoOk = !string.IsNullOrWhiteSpace(cliente.CodigoRecuperacao)
                && !string.IsNullOrWhiteSpace(dto.Code)
                && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(cliente.CodigoRecuperacao),
                    System.Text.Encoding.UTF8.GetBytes(dto.Code));

            if (!codigoOk || string.IsNullOrWhiteSpace(dto.SenhaNova))
            {
                cliente.TentativasCodigo++;
                if (cliente.TentativasCodigo >= MaxTentativasCodigo)
                {
                    cliente.CodigoRecuperacao = null;
                    cliente.CodigoRecuperacaoExpira = null;
                    cliente.CodigoBloqueado = true;
                    await _context.Cliente.ReplaceOneAsync(c => c.Id == cliente.Id, cliente);
                    return BadRequest(new { message = "Número de tentativas excedido. Solicite um novo código." });
                }

                await _context.Cliente.ReplaceOneAsync(c => c.Id == cliente.Id, cliente);
                return BadRequest(new { message = "Código inválido ou expirado." });
            }

            if (SenhaHelper.Verificar(dto.SenhaNova, cliente.SenhaHash))
                return BadRequest(new { message = "A nova senha não pode ser igual à senha atual." });

            if (!RequisitosSenhaOk(dto.SenhaNova))
                return BadRequest(new { message = "A nova senha não atende aos requisitos: mínimo 6 caracteres, com maiúscula, minúscula, número e caractere especial." });

            cliente.SenhaHash = SenhaHelper.GerarHash(dto.SenhaNova);
            cliente.PrimeiroAcesso = false;
            cliente.CodigoRecuperacao = null;
            cliente.CodigoRecuperacaoExpira = null;
            cliente.TentativasCodigo = 0;
            cliente.CodigoBloqueado = false;
            await _context.Cliente.ReplaceOneAsync(c => c.Id == cliente.Id, cliente);

            await _logService.RegistrarAsync(
                "senha_redefinida", $"Cliente '{cliente.Nome}' redefiniu a senha via recuperação",
                "web", cliente.Id.ToString(), cliente.Nome, cliente.Email);

            return Ok(new { message = "Senha redefinida com sucesso." });
        }

        [HttpPut("edit/{id}")]
        public async Task<IActionResult> Edit(Guid id, [FromBody] Cliente cliente)
        {
            var existing = await _context.Cliente.Find(c => c.Id == id).FirstOrDefaultAsync();
            if (existing == null)
                return NotFound(new { message = "Cliente não encontrado." });

            if (string.IsNullOrWhiteSpace(cliente.Nome))
                return BadRequest(new { message = "O nome é obrigatório." });

            if (string.IsNullOrWhiteSpace(cliente.Telefone))
                return BadRequest(new { message = "O telefone é obrigatório." });

            existing.Nome = cliente.Nome;
            existing.Telefone = cliente.Telefone;
            existing.Endereco = cliente.Endereco;
            existing.Observacoes = cliente.Observacoes;

            await _context.Cliente.ReplaceOneAsync(c => c.Id == id, existing);

            await _logService.RegistrarAsync(
                "cliente_editado", $"Cliente '{existing.Nome}' atualizado",
                "web", existing.UsuarioId.ToString(), existing.Nome, existing.Email);

            return Ok(new { message = "Cliente atualizado com sucesso." });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await _context.Cliente.Find(c => c.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Cliente não encontrado." });

            await _context.Cliente.DeleteOneAsync(c => c.Id == id);

            await _logService.RegistrarAsync(
                "cliente_deletado", $"Cliente '{existing.Nome}' deletado",
                "web", existing.UsuarioId.ToString(), existing.Nome, existing.Email);

            return Ok(new { message = "Cliente deletado com sucesso." });
        }

        [HttpDelete("delete-conta/{id}")]
        public async Task<IActionResult> DeletarConta(Guid id)
        {
            var existing = await _context.Cliente.Find(c => c.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Cliente não encontrado." });

            var cancelados = await ContaClienteHelper.EncerrarENotificarAsync(_context, existing);

            var filtros = new List<FilterDefinition<Log>>
            {
                Builders<Log>.Filter.Eq(l => l.UsuarioId, id.ToString()),
                Builders<Log>.Filter.Eq(l => l.UsuarioNome, existing.Nome)
            };

            if (!string.IsNullOrWhiteSpace(existing.Email))
                filtros.Add(Builders<Log>.Filter.Eq(l => l.Email, existing.Email));

            await _context.Log.DeleteManyAsync(Builders<Log>.Filter.Or(filtros));

            return Ok(new
            {
                message = cancelados > 0
                    ? "Conta encerrada. Seus dados foram removidos e seus agendamentos futuros foram cancelados."
                    : "Conta encerrada. Seus dados foram removidos.",
                agendamentosFuturos = cancelados
            });
        }

        private async Task<int> ContarAgendamentosFuturosAsync(Guid clienteId)
        {
            var filtro = Builders<Agendamento>.Filter.And(
                Builders<Agendamento>.Filter.Eq(a => a.ClienteId, clienteId),
                Builders<Agendamento>.Filter.Gte(a => a.DataHora, DateTime.UtcNow),
                Builders<Agendamento>.Filter.Nin(a => a.Status, new[] { StatusAgendamento.Cancelado, StatusAgendamento.Concluido }));

            return (int)await _context.Agendamento.CountDocumentsAsync(filtro);
        }

        private static string PrimeiroNome(string nome)
        {
            var semDominio = nome.Split('@')[0];
            var partes = semDominio.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return partes.Length > 0 ? partes[0] : "prestador";
        }

        private async Task<string> GerarEmailUnico(string baseSlug)
        {
            var candidato = $"{baseSlug}@dayplannio.com";
            if (await _context.Cliente.Find(c => c.Email == candidato).FirstOrDefaultAsync() == null)
                return candidato;

            for (int i = 2; i < 10000; i++)
            {
                candidato = $"{baseSlug}{i}@dayplannio.com";
                if (await _context.Cliente.Find(c => c.Email == candidato).FirstOrDefaultAsync() == null)
                    return candidato;
            }

            return $"{baseSlug}_{Guid.NewGuid():N}@dayplannio.com";
        }
    }

    public class LoginClienteDto
    {
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
    }

    public class TrocarSenhaClienteDto
    {
        public string Email { get; set; } = string.Empty;
        public string SenhaAtual { get; set; } = string.Empty;
        public string SenhaNova { get; set; } = string.Empty;
    }

    public class DefinirEmailSecundarioDto
    {
        public Guid ClienteId { get; set; }
        public string EmailSecundario { get; set; } = string.Empty;
    }

    public class ForgotPasswordClienteDto
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordClienteDto
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string SenhaNova { get; set; } = string.Empty;
    }
}