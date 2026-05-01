using DayPlannio.Api.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace DayPlannio.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AgendamentosController : ControllerBase
    {
        private readonly ContextMongodb _context = new ContextMongodb();

        [HttpGet("{usuarioId}")]
        public async Task<IActionResult> GetAll(Guid usuarioId)
        {
            var agendamentos = await _context.Agendamento
                .Find(a => a.UsuarioId == usuarioId)
                .ToListAsync();

            return Ok(agendamentos);
        }

        [HttpGet("{usuarioId}/status/{status}")]
        public async Task<IActionResult> GetByStatus(Guid usuarioId, StatusAgendamento status)
        {
            var agendamentos = await _context.Agendamento
                .Find(a => a.UsuarioId == usuarioId && a.Status == status)
                .ToListAsync();

            return Ok(agendamentos);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] Agendamento agendamento)
        {
            var agendamentoExistente = await _context.Agendamento
                .Find(a => a.UsuarioId == agendamento.UsuarioId && a.DataHora == agendamento.DataHora && a.Status != StatusAgendamento.Cancelado)
                .FirstOrDefaultAsync();

            if (agendamentoExistente != null)
            {
                return BadRequest(new { message = "Já existe um agendamento para este horário." });
            }

            agendamento.Id = Guid.NewGuid();
            agendamento.CreatedAt = DateTime.UtcNow;
            agendamento.Status = StatusAgendamento.Agendado;

            await _context.Agendamento.InsertOneAsync(agendamento);
            return Ok(new { message = "Agendamento criado com sucesso.", id = agendamento.Id });
        }

        [HttpPut("edit/{id}")]
        public async Task<IActionResult> Edit(Guid id, [FromBody] Agendamento agendamento)
        {
            var existing = await _context.Agendamento.Find(a => a.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Agendamento não encontrado." });

            existing.ClienteId = agendamento.ClienteId;
            existing.TipoServicoId = agendamento.TipoServicoId;
            existing.DataHora = agendamento.DataHora;
            existing.Observacoes = agendamento.Observacoes;
            existing.ValorCobrado = agendamento.ValorCobrado;
            existing.CustoMaterial = agendamento.CustoMaterial;

            await _context.Agendamento.ReplaceOneAsync(a => a.Id == id, existing);
            return Ok(new { message = "Agendamento atualizado com sucesso." });
        }

        [HttpPut("cancelar/{id}")]
        public async Task<IActionResult> Cancelar(Guid id)
        {
            var existing = await _context.Agendamento.Find(a => a.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Agendamento não encontrado." });

            existing.Status = StatusAgendamento.Cancelado;

            await _context.Agendamento.ReplaceOneAsync(a => a.Id == id, existing);
            return Ok(new { message = "Agendamento cancelado com sucesso." });
        }

        [HttpPut("concluir/{id}")]
        public async Task<IActionResult> Concluir(Guid id)
        {
            var existing = await _context.Agendamento.Find(a => a.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Agendamento não encontrado." });

            existing.Status = StatusAgendamento.Concluido;
            existing.DataConclusao = DateTime.UtcNow;

            await _context.Agendamento.ReplaceOneAsync(a => a.Id == id, existing);
            return Ok(new { message = "Agendamento concluído com sucesso." });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await _context.Agendamento.Find(a => a.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Agendamento não encontrado." });

            await _context.Agendamento.DeleteOneAsync(a => a.Id == id);
            return Ok(new { message = "Agendamento deletado com sucesso." });
        }

        [HttpGet("lucro/{usuarioId}")]
        public async Task<IActionResult> CalcularLucro(Guid usuarioId, [FromQuery] string periodo = "mensal")
        {
            var agora = DateTime.UtcNow;
            DateTime dataInicio = periodo switch
            {
                "diario" => agora.Date,
                "semanal" => agora.Date.AddDays(-(int)agora.DayOfWeek),
                "mensal" => new DateTime(agora.Year, agora.Month, 1),
                _ => new DateTime(agora.Year, agora.Month, 1)
            };

            var agendamentos = await _context.Agendamento
                .Find(a => a.UsuarioId == usuarioId
                       && a.Status == StatusAgendamento.Concluido
                       && a.DataConclusao.HasValue
                       && a.DataConclusao.Value >= dataInicio
                       && a.DataConclusao.Value <= agora)
                .ToListAsync();

            var lucroBruto = agendamentos.Sum(a => a.ValorCobrado);
            var custoTotal = agendamentos.Sum(a => a.CustoMaterial);
            var lucroLiquido = lucroBruto - custoTotal;

            return Ok(new
            {
                periodo,
                dataInicio,
                dataFim = agora,
                lucroBruto,
                custoTotal,
                lucroLiquido,
                totalServicos = agendamentos.Count
            });
        }

        [HttpGet("agenda/{usuarioId}")]
        public async Task<IActionResult> GetAgenda(Guid usuarioId, [FromQuery] string periodo = "mensal")
        {
            var agora = DateTime.UtcNow;
            DateTime dataInicio;
            DateTime dataFim;

            switch (periodo)
            {
                case "diario":
                    dataInicio = agora.Date;
                    dataFim = agora.Date.AddDays(1);
                    break;
                case "semanal":
                    dataInicio = agora.Date;
                    dataFim = agora.Date.AddDays(7);
                    break;
                case "mensal":
                    dataInicio = agora.Date;
                    dataFim = agora.Date.AddMonths(1);
                    break;
                default:
                    dataInicio = agora.Date;
                    dataFim = agora.Date.AddMonths(1);
                    break;
            }

            var agendamentos = await _context.Agendamento
                .Find(a => a.UsuarioId == usuarioId
                       && a.DataHora >= dataInicio
                       && a.DataHora <= dataFim)
                .SortBy(a => a.DataHora)
                .ToListAsync();

            return Ok(agendamentos);
        }

        [HttpGet("historico/{clienteId}")]
        public async Task<IActionResult> Historico(Guid clienteId)
        {
            var agendamentos = await _context.Agendamento
                .Find(a => a.ClienteId == clienteId)
                .SortByDescending(a => a.DataHora)
                .ToListAsync();

            return Ok(agendamentos);
        }

    }
}