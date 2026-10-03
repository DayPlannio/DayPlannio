using DayPlannio.Api.Helpers;
using DayPlannio.Api.Models;
using DayPlannio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Linq.Expressions;

namespace DayPlannio.Api.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly LogService _logService;
        private readonly AzureBlobStorageService _blobService;
        private readonly EmailService _emailService;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            LogService logService,
            AzureBlobStorageService blobService,
            EmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logService = logService;
            _blobService = blobService;
            _emailService = emailService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] AdminLoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Senha))
                return BadRequest(new { message = "E-mail e senha são obrigatórios." });

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return Unauthorized(new { message = "Credenciais inválidas." });

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (!isAdmin)
                return Unauthorized(new { message = "Credenciais inválidas." });

            var result = await _signInManager.PasswordSignInAsync(user, dto.Senha, false, false);
            if (!result.Succeeded)
                return Unauthorized(new { message = "Credenciais inválidas." });

            await _logService.RegistrarAsync(
                "login_admin", "Login do administrador realizado", "web",
                user.Id.ToString(), user.NomeCompleto, user.Email);

            return Ok(new
            {
                message = "Login realizado com sucesso.",
                userId = user.Id,
                nome = user.NomeCompleto,
                email = user.Email
            });
        }

        [HttpGet("logs")]
        public async Task<IActionResult> Logs([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 50)
        {
            var context = new ContextMongodb();
            var skip = (pagina - 1) * tamanhoPagina;
            var total = await context.Log.CountDocumentsAsync(FilterDefinition<Log>.Empty);
            var logs = await context.Log
                .Find(FilterDefinition<Log>.Empty)
                .SortByDescending(l => l.Data)
                .Skip(skip)
                .Limit(tamanhoPagina)
                .ToListAsync();

            return Ok(new { total, pagina, tamanhoPagina, logs });
        }

        [HttpGet("clientes")]
        public async Task<IActionResult> Clientes()
        {
            var context = new ContextMongodb();
            var clientes = await context.Cliente
                .Find(FilterDefinition<Cliente>.Empty)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();

            var result = clientes.Select(c => new
            {
                id = c.Id,
                nome = c.Nome,
                telefone = c.Telefone,
                email = c.Email,
                emailSecundario = c.EmailSecundario,
                ativo = c.Ativo,
                primeiroAcesso = c.PrimeiroAcesso,
                criadoEm = c.CreatedAt
            });

            return Ok(result);
        }

        [HttpGet("prestadores")]
        public async Task<IActionResult> Prestadores()
        {
            var context = new ContextMongodb();
            var cadastrados = await context.ApplicationUsers.Find(FilterDefinition<ApplicationUser>.Empty).ToListAsync();

            var prestadores = cadastrados
                .Select((u, indice) => (usuario: u, indice))
                .OrderByDescending(x => x.usuario.CriadoEm ?? DateTime.MinValue)
                .ThenByDescending(x => x.indice)
                .Select(x => x.usuario)
                .ToList();

            var result = prestadores.Select(u => new
            {
                id = u.Id,
                nome = u.NomeCompleto,
                email = u.Email,
                telefone = u.Telefone,
                userName = u.UserName,
                plano = u.Plano,
                planoAtivo = u.PlanoAtivo,
                planoExpiraEm = u.PlanoExpiraEm,
                planoOrigem = u.PlanoOrigem,
                planoPendente = u.PlanoPendente,
                planoSolicitadoEm = u.PlanoSolicitadoEm
            });

            return Ok(result);
        }

        [HttpPost("planos/{id}/aprovar")]
        public async Task<IActionResult> AprovarPlano(Guid id)
        {
            var context = new ContextMongodb();
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound(new { message = "Prestador não encontrado." });

            if (string.IsNullOrWhiteSpace(user.PlanoPendente))
                return BadRequest(new { message = "Não há plano pendente para aprovar." });

            user.Plano = user.PlanoPendente;
            user.PlanoAtivo = true;
            user.PlanoExpiraEm = DateTime.UtcNow.AddDays(30);
            user.PlanoOrigem = "assinatura";
            user.PlanoPendente = null;
            user.PlanoSolicitadoEm = null;
            user.RenovacaoAutomatica = true;
            user.PlanoCanceladoEm = null;

            var filtro = Builders<ApplicationUser>.Filter.Eq("_id", user.Id);
            var update = Builders<ApplicationUser>.Update
                .Set(u => u.Plano, user.Plano)
                .Set(u => u.PlanoAtivo, true)
                .Set(u => u.PlanoExpiraEm, DateTime.UtcNow.AddDays(30))
                .Set(u => u.PlanoOrigem, "assinatura")
                .Set(u => u.PlanoPendente, (string?)null)
                .Set(u => u.PlanoSolicitadoEm, (DateTime?)null)
                .Set(u => u.RenovacaoAutomatica, true)
                .Set(u => u.PlanoCanceladoEm, (DateTime?)null);
            await context.ApplicationUsers.UpdateOneAsync(filtro, update);

            var vencimento = DateTime.UtcNow.AddDays(30);

            await context.Notificacao.InsertOneAsync(new Notificacao
            {
                Id = Guid.NewGuid(),
                UsuarioId = user.Id,
                Titulo = "Plano aprovado!",
                Mensagem = $"Seu plano {PlanoService.NomeExibicao(user.Plano)} foi aprovado e já está liberado no app. A renovação automática está ligada e a próxima renovação acontece em {vencimento:dd/MM/yyyy}.",
                Tipo = "plano_aprovado",
                Lida = false,
                DataCriacao = DateTime.UtcNow
            });

            await _logService.RegistrarAsync(
                "plano_aprovado",
                $"Plano '{user.Plano}' aprovado para o prestador '{user.NomeCompleto}' com renovação automática em {vencimento:dd/MM/yyyy}",
                "web", user.Id.ToString(), user.NomeCompleto, user.Email);

            return Ok(new { message = "Plano aprovado com sucesso." });
        }

        [HttpPost("planos/{id}/rejeitar")]
        public async Task<IActionResult> RejeitarPlano(Guid id)
        {
            var context = new ContextMongodb();
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound(new { message = "Prestador não encontrado." });

            if (string.IsNullOrWhiteSpace(user.PlanoPendente))
                return BadRequest(new { message = "Não há plano pendente para rejeitar." });

            var planoRejeitado = user.PlanoPendente;

            var filtro = Builders<ApplicationUser>.Filter.Eq("_id", user.Id);
            var update = Builders<ApplicationUser>.Update
                .Set(u => u.PlanoPendente, (string?)null)
                .Set(u => u.PlanoSolicitadoEm, (DateTime?)null);
            await context.ApplicationUsers.UpdateOneAsync(filtro, update);

            var planoAtual = PlanoService.ObterPlanoEfetivo(user);

            await context.Notificacao.InsertOneAsync(new Notificacao
            {
                Id = Guid.NewGuid(),
                UsuarioId = user.Id,
                Titulo = "Solicitação não aprovada",
                Mensagem = $"Sua solicitação do plano {PlanoService.NomeExibicao(planoRejeitado)} não foi aprovada. Você continua usando o plano {PlanoService.NomeExibicao(planoAtual)} e pode escolher outro plano quando quiser.",
                Tipo = "plano_rejeitado",
                Lida = false,
                DataCriacao = DateTime.UtcNow
            });

            await _logService.RegistrarAsync(
                "plano_rejeitado", $"Solicitação do plano '{planoRejeitado}' do prestador '{user.NomeCompleto}' foi rejeitada",
                "web", user.Id.ToString(), user.NomeCompleto, user.Email);

            return Ok(new { message = "Solicitação rejeitada." });
        }

        [HttpGet("fotos-pendentes")]
        public async Task<IActionResult> FotosPendentes()
        {
            var context = new ContextMongodb();
            var filter = Builders<Foto>.Filter.Or(
                Builders<Foto>.Filter.Eq(f => f.Aprovado, false),
                Builders<Foto>.Filter.Exists(f => f.Aprovado, false)
            );
            var fotos = await context.Foto
                .Find(filter)
                .SortByDescending(f => f.DataUpload)
                .ToListAsync();

            var prestadorIds = fotos.Select(f => f.PrestadorId).Distinct().ToList();
            var prestadores = await context.ApplicationUsers
                .Find(u => prestadorIds.Contains(u.Id))
                .ToListAsync();
            var prestadorMap = prestadores.ToDictionary(u => u.Id);

            var agendamentoIds = fotos.Select(f => f.AgendamentoId).Distinct().ToList();
            var agendamentos = await context.Agendamento
                .Find(a => agendamentoIds.Contains(a.Id))
                .ToListAsync();
            var agendamentoMap = agendamentos.ToDictionary(a => a.Id);

            var servicoIds = agendamentos.Select(a => a.TipoServicoId).Distinct().ToList();
            var servicos = await context.TipoServico
                .Find(s => servicoIds.Contains(s.Id))
                .ToListAsync();
            var servicoMap = servicos.ToDictionary(s => s.Id);

            var result = fotos.Select(f =>
            {
                var ag = agendamentoMap.GetValueOrDefault(f.AgendamentoId);
                return new
                {
                    id = f.Id,
                    url = _blobService.GerarUrlPublica(f.Url),
                    descricao = f.Descricao,
                    publica = f.Publica,
                    dataUpload = f.DataUpload,
                    prestador = prestadorMap.GetValueOrDefault(f.PrestadorId)?.NomeCompleto ?? "Profissional",
                    servico = ag != null ? servicoMap.GetValueOrDefault(ag.TipoServicoId)?.Tipo : null
                };
            }).ToList();

            return Ok(result);
        }

        [HttpPut("fotos/{id}/aprovar")]
        public async Task<IActionResult> AprovarFoto(Guid id)
        {
            var context = new ContextMongodb();
            var foto = await context.Foto.Find(f => f.Id == id).FirstOrDefaultAsync();
            if (foto == null)
                return NotFound(new { message = "Foto não encontrada." });

            var update = Builders<Foto>.Update.Set(f => f.Aprovado, true);
            await context.Foto.UpdateOneAsync(f => f.Id == id, update);

            var notificacao = new Notificacao
            {
                Id = Guid.NewGuid(),
                UsuarioId = foto.PrestadorId,
                Titulo = "Foto aprovada",
                Mensagem = "Sua foto foi aprovada e já está visível no portfólio.",
                Tipo = "foto_aprovada",
                Lida = false,
                DataCriacao = DateTime.UtcNow
            };
            await context.Notificacao.InsertOneAsync(notificacao);

            var (servicoNome, prestadorNome) = await ObterServicoEPrestadorAsync(context, foto);
            var admin = await _userManager.GetUserAsync(User);

            await _logService.RegistrarAsync(
                "foto_aprovada", $"Foto de {servicoNome} ({prestadorNome}) aprovada pelo admin", "web",
                admin?.Id.ToString(), admin?.NomeCompleto, admin?.Email);

            return Ok(new { message = "Foto aprovada com sucesso." });
        }

        [HttpPut("fotos/{id}/rejeitar")]
        public async Task<IActionResult> RejeitarFoto(Guid id)
        {
            var context = new ContextMongodb();
            var foto = await context.Foto.Find(f => f.Id == id).FirstOrDefaultAsync();
            if (foto == null)
                return NotFound(new { message = "Foto não encontrada." });

            var (servicoNome, prestadorNome) = await ObterServicoEPrestadorAsync(context, foto);

            var notificacao = new Notificacao
            {
                Id = Guid.NewGuid(),
                UsuarioId = foto.PrestadorId,
                Titulo = "Foto rejeitada",
                Mensagem = "Sua foto foi rejeitada pelo administrador e removida do sistema.",
                Tipo = "foto_rejeitada",
                Lida = false,
                DataCriacao = DateTime.UtcNow
            };
            await context.Notificacao.InsertOneAsync(notificacao);

            await _blobService.DeleteAsync(foto.Url);
            await context.Foto.DeleteOneAsync(f => f.Id == id);

            var admin = await _userManager.GetUserAsync(User);

            await _logService.RegistrarAsync(
                "foto_rejeitada", $"Foto de {servicoNome} ({prestadorNome}) rejeitada pelo admin", "web",
                admin?.Id.ToString(), admin?.NomeCompleto, admin?.Email);

            return Ok(new { message = "Foto rejeitada e removida." });
        }

        private static async Task<(string servico, string prestador)> ObterServicoEPrestadorAsync(ContextMongodb context, Foto foto)
        {
            var agendamento = await context.Agendamento.Find(a => a.Id == foto.AgendamentoId).FirstOrDefaultAsync();
            var servicoNome = agendamento != null
                ? (await context.TipoServico.Find(s => s.Id == agendamento.TipoServicoId).FirstOrDefaultAsync())?.Tipo
                : null;
            var prestadorNome = (await context.ApplicationUsers.Find(u => u.Id == foto.PrestadorId).FirstOrDefaultAsync())?.NomeCompleto;

            return (servicoNome ?? "atendimento", prestadorNome ?? "prestador");
        }

        [HttpGet("notificacoes/{usuarioId}")]
        public async Task<IActionResult> Notificacoes(Guid usuarioId)
        {
            var context = new ContextMongodb();
            var notificacoes = await context.Notificacao
                .Find(n => n.UsuarioId == usuarioId)
                .SortByDescending(n => n.DataCriacao)
                .Limit(50)
                .ToListAsync();

            var result = notificacoes.Select(n => new
            {
                id = n.Id,
                titulo = n.Titulo,
                mensagem = n.Mensagem,
                tipo = n.Tipo,
                lida = n.Lida,
                dataCriacao = n.DataCriacao
            });

            return Ok(result);
        }

        [HttpPut("notificacoes/{id}/lida")]
        public async Task<IActionResult> MarcarLida(Guid id)
        {
            var context = new ContextMongodb();
            var update = Builders<Notificacao>.Update.Set(n => n.Lida, true);
            await context.Notificacao.UpdateOneAsync(n => n.Id == id, update);
            return Ok(new { message = "Notificação marcada como lida." });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("contas/encerrar")]
        public async Task<IActionResult> EncerrarConta([FromBody] EncerrarContaAdminDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Motivo))
                return BadRequest(new { message = "Informe o motivo do encerramento da conta." });

            var motivo = dto.Motivo.Trim();
            if (motivo.Length < 5)
                return BadRequest(new { message = "O motivo deve ter pelo menos 5 caracteres." });

            var tipo = (dto.Tipo ?? string.Empty).Trim().ToLowerInvariant();
            if (tipo != "cliente" && tipo != "prestador")
                return BadRequest(new { message = "Tipo de conta inválido." });

            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
                return Unauthorized(new { message = "Administrador não autenticado." });

            var context = new ContextMongodb();

            if (tipo == "prestador")
            {
                var user = await _userManager.FindByIdAsync(dto.Id.ToString());
                if (user == null)
                    return NotFound(new { message = "Prestador não encontrado." });

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                    return BadRequest(new { message = "Não é possível encerrar a conta de um administrador." });

                var nome = user.NomeCompleto;
                var email = user.Email;
                var prestadorId = user.Id;

                var afetados = await BuscarAgendamentosAfetadosAsync(context, a => a.UsuarioId == prestadorId, prestadorId);

                var fotos = await context.Foto
                    .Find(Builders<Foto>.Filter.Eq(f => f.PrestadorId, prestadorId))
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

                await context.Foto.DeleteManyAsync(Builders<Foto>.Filter.Eq(f => f.PrestadorId, prestadorId));
                var clientesDoPrestador = await ContaPrestadorHelper.ListarClientesAsync(context, prestadorId);

                await context.Cliente.DeleteManyAsync(Builders<Cliente>.Filter.Eq(c => c.UsuarioId, prestadorId));
                await context.TipoServico.DeleteManyAsync(Builders<TipoServico>.Filter.Eq(s => s.UsuarioId, prestadorId));
                await context.Agendamento.DeleteManyAsync(Builders<Agendamento>.Filter.Eq(a => a.UsuarioId, prestadorId));
                await context.Financeiro.DeleteManyAsync(Builders<Financeiro>.Filter.Eq(f => f.UsuarioId, prestadorId));
                await context.Notificacao.DeleteManyAsync(Builders<Notificacao>.Filter.Eq(n => n.UsuarioId, prestadorId));

                await ApagarLogsDoUsuarioAsync(context, prestadorId, nome, email);

                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                    return BadRequest(new { message = "Não foi possível encerrar a conta do prestador." });

                await AvisarClientesAfetadosAsync(context, afetados, nome);

                ContaPrestadorHelper.AvisarClientesPorEmail(_emailService, clientesDoPrestador, nome);

                await _logService.RegistrarAsync(
                    "conta_prestador_encerrada_admin",
                    $"Conta do prestador '{nome}' encerrada pelo administrador '{admin.NomeCompleto}'. Motivo: {motivo}",
                    "web", prestadorId.ToString(), nome, email);

                return Ok(new
                {
                    message = $"Conta do prestador '{nome}' encerrada.",
                    agendamentosAfetados = afetados.Count
                });
            }

            var cliente = await context.Cliente.Find(c => c.Id == dto.Id).FirstOrDefaultAsync();
            if (cliente == null)
                return NotFound(new { message = "Cliente não encontrado." });

            var clienteNome = cliente.Nome;
            var clienteEmail = cliente.Email;
            var clienteId = cliente.Id;

            var agendamentosAfetados = await ContaClienteHelper.EncerrarENotificarAsync(
                context, cliente, encerradaPeloAdmin: true);

            await ApagarLogsDoUsuarioAsync(context, clienteId, clienteNome, clienteEmail);

            await _logService.RegistrarAsync(
                "conta_cliente_encerrada_admin",
                $"Conta do cliente '{clienteNome}' encerrada pelo administrador '{admin.NomeCompleto}'. Motivo: {motivo}",
                "web", clienteId.ToString(), clienteNome, clienteEmail);

            return Ok(new
            {
                message = $"Conta do cliente '{clienteNome}' encerrada.",
                agendamentosAfetados
            });
        }

        private static async Task<List<Agendamento>> BuscarAgendamentosAfetadosAsync(
            ContextMongodb context,
            Expression<Func<Agendamento, bool>> condicao,
            Guid? prestadorId)
        {
            var filtro = Builders<Agendamento>.Filter.And(
                Builders<Agendamento>.Filter.Where(condicao),
                Builders<Agendamento>.Filter.Gte(a => a.DataHora, DateTime.UtcNow),
                Builders<Agendamento>.Filter.Nin(a => a.Status, new[] { StatusAgendamento.Cancelado, StatusAgendamento.Concluido }));

            return await context.Agendamento.Find(filtro).ToListAsync();
        }

        private static async Task ApagarLogsDoUsuarioAsync(
            ContextMongodb context,
            Guid usuarioId,
            string? nome,
            string? email)
        {
            var filtros = new List<FilterDefinition<Log>>
            {
                Builders<Log>.Filter.Eq(l => l.UsuarioId, usuarioId.ToString())
            };

            if (!string.IsNullOrWhiteSpace(nome))
                filtros.Add(Builders<Log>.Filter.Eq(l => l.UsuarioNome, nome));

            if (!string.IsNullOrWhiteSpace(email))
                filtros.Add(Builders<Log>.Filter.Eq(l => l.Email, email));

            await context.Log.DeleteManyAsync(Builders<Log>.Filter.Or(filtros));
        }

        private static async Task AvisarClientesAfetadosAsync(
            ContextMongodb context,
            List<Agendamento> agendamentos,
            string prestadorNome)
        {
            if (agendamentos.Count == 0)
                return;

            var porCliente = agendamentos
                .GroupBy(a => a.ClienteId)
                .Select(g => new { ClienteId = g.Key, Quantidade = g.Count() })
                .ToList();

            var clientes = await context.Cliente
                .Find(Builders<Cliente>.Filter.In(c => c.Id, porCliente.Select(x => x.ClienteId)))
                .ToListAsync();

            var lista = new List<Notificacao>();
            var agora = DateTime.UtcNow;

            foreach (var item in porCliente)
            {
                var cliente = clientes.FirstOrDefault(c => c.Id == item.ClienteId);
                if (cliente == null)
                    continue;

                var plural = item.Quantidade == 1 ? "atendimento" : "atendimentos";

                lista.Add(new Notificacao
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = cliente.UsuarioId,
                    Titulo = "Profissional encerrou a conta",
                    Mensagem = $"O profissional {prestadorNome} encerrou a conta no DayPlannio e você tinha {item.Quantidade} {plural} agendado(s). Entre em contato com o profissional para remarcar.",
                    Tipo = "prestador_encerrou_conta",
                    Lida = false,
                    DataCriacao = agora
                });
            }

            if (lista.Count > 0)
                await context.Notificacao.InsertManyAsync(lista);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var admin = await _userManager.GetUserAsync(User);

            await _logService.RegistrarAsync(
                "logout_admin", $"Administrador '{admin?.NomeCompleto}' fez logout",
                "web", admin?.Id.ToString(), admin?.NomeCompleto, admin?.Email);

            return Ok(new { message = "Logout realizado com sucesso." });
        }

        [HttpGet("notificacoes/{usuarioId}/nao-lidas")]
        public async Task<IActionResult> ContarNaoLidas(Guid usuarioId)
        {
            var context = new ContextMongodb();
            var count = await context.Notificacao.CountDocumentsAsync(n => n.UsuarioId == usuarioId && !n.Lida);
            return Ok(new { total = count });
        }


    }

    public class AdminLoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
    }

    public class EncerrarContaAdminDto
    {
        public string Tipo { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public string Motivo { get; set; } = string.Empty;
    }
}