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
    public class FotosController : ControllerBase
    {
        private readonly ContextMongodb _context = new ContextMongodb();
        private readonly AzureBlobStorageService _blobService;
        private readonly LogService _logService;

        public FotosController(AzureBlobStorageService blobService, LogService logService)
        {
            _blobService = blobService;
            _logService = logService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(
            IFormFile arquivo,
            [FromForm] Guid agendamentoId,
            [FromForm] Guid prestadorId,
            [FromForm] string? descricao,
            [FromForm] bool publica = false)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest(new { message = "Nenhum arquivo enviado." });

            if (!arquivo.ContentType.StartsWith("image/"))
                return BadRequest(new { message = "Apenas imagens são aceitas." });

            if (arquivo.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = "Arquivo muito grande (máximo 10MB)." });

            var acesso = await PlanoService.VerificarAsync(prestadorId, PlanoService.Full, _context);
            if (!acesso.autorizado)
                return StatusCode(403, new { message = acesso.mensagem });

            using var stream = arquivo.OpenReadStream();
            var url = await _blobService.UploadAsync(stream, arquivo.FileName);

            var foto = new Foto
            {
                Id = Guid.NewGuid(),
                AgendamentoId = agendamentoId,
                PrestadorId = prestadorId,
                Url = url,
                Descricao = descricao,
                Publica = publica,
                Aprovado = false,
                DataUpload = DateTime.UtcNow
            };

            await _context.Foto.InsertOneAsync(foto);

            var notificacao = new Notificacao
            {
                Id = Guid.NewGuid(),
                UsuarioId = prestadorId,
                Titulo = "Foto em análise",
                Mensagem = "Sua foto foi enviada e está em análise pelo administrador.",
                Tipo = "foto_enviada",
                Lida = false,
                DataCriacao = DateTime.UtcNow
            };
            await _context.Notificacao.InsertOneAsync(notificacao);

            await _logService.RegistrarAsync(
                "foto_upload", $"Foto enviada (pública={publica})", "mobile",
                prestadorId.ToString());

            return Ok(new
            {
                message = "Foto enviada com sucesso.",
                id = foto.Id,
                url = _blobService.GerarUrlPublica(foto.Url)
            });
        }

        [HttpGet("agendamento/{agendamentoId}")]
        public async Task<IActionResult> GetByAgendamento(Guid agendamentoId)
        {
            var fotos = await _context.Foto
                .Find(f => f.AgendamentoId == agendamentoId)
                .SortByDescending(f => f.DataUpload)
                .ToListAsync();

            var resultado = fotos.Select(f => new
            {
                id = f.Id,
                url = _blobService.GerarUrlPublica(f.Url),
                descricao = f.Descricao,
                publica = f.Publica,
                dataUpload = f.DataUpload
            }).ToList();

            return Ok(resultado);
        }

        [HttpGet("portfolio/{prestadorId}")]
        public async Task<IActionResult> GetPortfolio(Guid prestadorId)
        {
            var fotos = await _context.Foto
                .Find(f => f.PrestadorId == prestadorId && f.Publica && f.Aprovado)
                .SortByDescending(f => f.DataUpload)
                .ToListAsync();

            var agendamentoIds = fotos.Select(f => f.AgendamentoId).Distinct().ToList();
            var agendamentos = await _context.Agendamento
                .Find(a => agendamentoIds.Contains(a.Id))
                .ToListAsync();
            var agendamentoMap = agendamentos.ToDictionary(a => a.Id);

            var servicoIds = agendamentos.Select(a => a.TipoServicoId).Distinct().ToList();
            var servicos = await _context.TipoServico
                .Find(s => servicoIds.Contains(s.Id))
                .ToListAsync();
            var servicoMap = servicos.ToDictionary(s => s.Id);

            var clienteIds = agendamentos.Select(a => a.ClienteId).Distinct().ToList();
            var clientes = await _context.Cliente
                .Find(c => clienteIds.Contains(c.Id))
                .ToListAsync();
            var clienteMap = clientes.ToDictionary(c => c.Id);

            var resultado = fotos.Select(f =>
            {
                var ag = agendamentoMap.GetValueOrDefault(f.AgendamentoId);
                return new
                {
                    id = f.Id,
                    url = _blobService.GerarUrlPublica(f.Url),
                    descricao = f.Descricao,
                    dataUpload = f.DataUpload,
                    servico = ag != null ? servicoMap.GetValueOrDefault(ag.TipoServicoId)?.Tipo : null,
                    cliente = ag != null ? clienteMap.GetValueOrDefault(ag.ClienteId)?.Nome : null,
                    dataAtendimento = ag?.DataConclusao ?? ag?.DataHora
                };
            }).ToList();

            return Ok(resultado);
        }

        [HttpGet("portfolio-por-servico/{prestadorId}")]
        public async Task<IActionResult> GetPortfolioPorServico(Guid prestadorId)
        {
            var fotos = await _context.Foto
                .Find(f => f.PrestadorId == prestadorId && f.Publica && f.Aprovado)
                .SortByDescending(f => f.DataUpload)
                .ToListAsync();

            var agendamentoIds = fotos.Select(f => f.AgendamentoId).Distinct().ToList();
            var agendamentos = await _context.Agendamento
                .Find(a => agendamentoIds.Contains(a.Id))
                .ToListAsync();
            var agendamentoMap = agendamentos.ToDictionary(a => a.Id);

            var servicoIds = agendamentos.Select(a => a.TipoServicoId).Distinct().ToList();
            var servicos = await _context.TipoServico
                .Find(s => servicoIds.Contains(s.Id))
                .ToListAsync();
            var servicoMap = servicos.ToDictionary(s => s.Id);

            var porServico = servicos.Select(s => new
            {
                servico = s.Tipo,
                servicoId = s.Id,
                fotos = fotos.Where(f =>
                {
                    var ag = agendamentoMap.GetValueOrDefault(f.AgendamentoId);
                    return ag?.TipoServicoId == s.Id;
                }).Select(f => new
                {
                    id = f.Id,
                    url = _blobService.GerarUrlPublica(f.Url),
                    descricao = f.Descricao,
                    dataUpload = f.DataUpload
                }).ToList()
            }).Where(g => g.fotos.Any()).ToList();

            return Ok(porServico);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var foto = await _context.Foto
                .Find(f => f.Id == id)
                .FirstOrDefaultAsync();

            if (foto == null)
                return NotFound(new { message = "Foto não encontrada." });

            var agendamentoRel = await _context.Agendamento.Find(a => a.Id == foto.AgendamentoId).FirstOrDefaultAsync();
            var servicoNome = agendamentoRel != null
                ? (await _context.TipoServico.Find(s => s.Id == agendamentoRel.TipoServicoId).FirstOrDefaultAsync())?.Tipo
                : null;

            await _blobService.DeleteAsync(foto.Url);
            await _context.Foto.DeleteOneAsync(f => f.Id == id);

            await _logService.RegistrarAsync(
                "foto_delete", $"Foto de {servicoNome ?? "atendimento"} excluida", "mobile",
                foto.PrestadorId.ToString());

            return Ok(new { message = "Foto removida com sucesso." });
        }

        [HttpGet("portfolio-publico")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPortfolioPublico()
        {
            var fotos = await _context.Foto
                .Find(f => f.Publica && f.Aprovado)
                .SortByDescending(f => f.DataUpload)
                .Limit(100)
                .ToListAsync();

            var prestadorIds = fotos.Select(f => f.PrestadorId).Distinct().ToList();
            var prestadores = await _context.ApplicationUsers
                .Find(u => prestadorIds.Contains(u.Id))
                .ToListAsync();

            var prestadoresFull = prestadores
                .Where(u => PlanoService.ObterPlanoEfetivo(u) == PlanoService.Full)
                .Select(u => u.Id)
                .ToHashSet();

            fotos = fotos.Where(f => prestadoresFull.Contains(f.PrestadorId)).ToList();

            var agendamentoIds = fotos.Select(f => f.AgendamentoId).Distinct().ToList();
            var agendamentos = await _context.Agendamento
                .Find(a => agendamentoIds.Contains(a.Id))
                .ToListAsync();
            var agendamentoMap = agendamentos.ToDictionary(a => a.Id);

            var servicoIds = agendamentos.Select(a => a.TipoServicoId).Distinct().ToList();
            var servicos = await _context.TipoServico
                .Find(s => servicoIds.Contains(s.Id))
                .ToListAsync();
            var servicoMap = servicos.ToDictionary(s => s.Id);

            var prestadorMap = prestadores.ToDictionary(u => u.Id);

            var resultado = fotos.Select(f =>
            {
                var ag = agendamentoMap.GetValueOrDefault(f.AgendamentoId);
                return new
                {
                    id = f.Id,
                    url = _blobService.GerarUrlPublica(f.Url),
                    descricao = f.Descricao,
                    dataUpload = f.DataUpload,
                    servico = ag != null ? servicoMap.GetValueOrDefault(ag.TipoServicoId)?.Tipo : null,
                    prestador = prestadorMap.GetValueOrDefault(f.PrestadorId)?.NomeCompleto ?? "Profissional",
                    dataAtendimento = ag?.DataConclusao ?? ag?.DataHora
                };
            }).ToList();

            return Ok(resultado);
        }

        [HttpGet("portfolio-stats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPortfolioStats()
        {
            var fotos = await _context.Foto.Find(f => f.Publica && f.Aprovado).ToListAsync();

            var prestadorIds = fotos.Select(f => f.PrestadorId).Distinct().ToList();
            var prestadores = await _context.ApplicationUsers
                .Find(u => prestadorIds.Contains(u.Id))
                .ToListAsync();

            var prestadoresFull = prestadores
                .Where(u => PlanoService.ObterPlanoEfetivo(u) == PlanoService.Full)
                .Select(u => u.Id)
                .ToHashSet();

            fotos = fotos.Where(f => prestadoresFull.Contains(f.PrestadorId)).ToList();
            prestadores = prestadores.Where(u => prestadoresFull.Contains(u.Id)).ToList();

            var agendamentoIds = fotos.Select(f => f.AgendamentoId).Distinct().ToList();
            var agendamentos = await _context.Agendamento
                .Find(a => agendamentoIds.Contains(a.Id))
                .ToListAsync();

            var servicoIds = agendamentos.Select(a => a.TipoServicoId).Distinct().ToList();
            var servicos = await _context.TipoServico
                .Find(s => servicoIds.Contains(s.Id))
                .ToListAsync();

            return Ok(new
            {
                totalPrestadores = prestadores.Count,
                totalFotos = fotos.Count,
                totalServicos = servicos.Count,
                cidades = prestadores.Where(p => p.CidadeVisivel && !string.IsNullOrEmpty(p.Cidade))
                    .Select(p => p.Cidade!)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList(),
                prestadores = prestadores.Select(p => new
                {
                    nome = p.NomeCompleto ?? "Profissional",
                    cidade = p.CidadeVisivel ? p.Cidade : null,
                    telefone = p.TelefoneVisivel ? p.Telefone : null,
                    cidadeVisivel = p.CidadeVisivel,
                    telefoneVisivel = p.TelefoneVisivel,
                    fotos = fotos.Count(f => f.PrestadorId == p.Id),
                    servicos = agendamentos
                        .Where(a => fotos.Any(f => f.AgendamentoId == a.Id && f.PrestadorId == p.Id))
                        .Select(a => a.TipoServicoId)
                        .Distinct()
                        .Count()
                }).OrderByDescending(p => p.fotos).ToList(),
                servicos = servicos.Select(s => new
                {
                    nome = s.Tipo,
                    fotos = fotos.Count(f =>
                    {
                        var ag = agendamentos.FirstOrDefault(a => a.Id == f.AgendamentoId);
                        return ag?.TipoServicoId == s.Id;
                    })
                }).Where(s => s.fotos > 0).OrderByDescending(s => s.fotos).ToList()
            });
        }
    }
}