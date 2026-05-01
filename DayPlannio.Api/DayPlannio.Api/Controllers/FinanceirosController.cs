using DayPlannio.Api.Models;
using DayPlannio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace DayPlannio.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FinanceirosController : ControllerBase
    {
        private readonly ContextMongodb _context = new ContextMongodb();
        private readonly RelatorioPdfService _relatorioPdfService;

        public FinanceirosController(RelatorioPdfService relatorioPdfService)
        {
            _relatorioPdfService = relatorioPdfService;
        }

        [HttpGet("{usuarioId}")]
        public async Task<IActionResult> GetAll(Guid usuarioId)
        {
            var registros = await _context.Financeiro
                .Find(f => f.UsuarioId == usuarioId)
                .SortByDescending(f => f.Data)
                .ToListAsync();

            return Ok(registros);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] Financeiro financeiro)
        {
            financeiro.Id = Guid.NewGuid();
            financeiro.CreatedAt = DateTime.UtcNow;

            await _context.Financeiro.InsertOneAsync(financeiro);
            return Ok(new { message = "Registro financeiro criado com sucesso.", id = financeiro.Id });
        }

        [HttpPut("edit/{id}")]
        public async Task<IActionResult> Edit(Guid id, [FromBody] Financeiro financeiro)
        {
            var existing = await _context.Financeiro.Find(f => f.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Registro não encontrado." });

            existing.Tipo = financeiro.Tipo;
            existing.Descricao = financeiro.Descricao;
            existing.Valor = financeiro.Valor;
            existing.Data = financeiro.Data;
            existing.Categoria = financeiro.Categoria;

            await _context.Financeiro.ReplaceOneAsync(f => f.Id == id, existing);
            return Ok(new { message = "Registro financeiro atualizado com sucesso." });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await _context.Financeiro.Find(f => f.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Registro não encontrado." });

            await _context.Financeiro.DeleteOneAsync(f => f.Id == id);
            return Ok(new { message = "Registro financeiro deletado com sucesso." });
        }

        [HttpGet("resumo/{usuarioId}")]
        public async Task<IActionResult> Resumo(Guid usuarioId, [FromQuery] string periodo = "mensal")
        {
            var agora = DateTime.UtcNow;
            DateTime dataInicio = periodo switch
            {
                "diario" => agora.Date,
                "semanal" => agora.Date.AddDays(-(int)agora.DayOfWeek),
                "mensal" => new DateTime(agora.Year, agora.Month, 1),
                _ => new DateTime(agora.Year, agora.Month, 1)
            };

            var registros = await _context.Financeiro
                .Find(f => f.UsuarioId == usuarioId
                       && f.Data >= dataInicio
                       && f.Data <= agora)
                .ToListAsync();

            var totalEntradas = registros.Where(f => f.Tipo == TipoFinanceiro.Entrada).Sum(f => f.Valor);
            var totalSaidas = registros.Where(f => f.Tipo == TipoFinanceiro.Saida).Sum(f => f.Valor);
            var saldo = totalEntradas - totalSaidas;

            return Ok(new
            {
                periodo,
                dataInicio,
                dataFim = agora,
                totalEntradas,
                totalSaidas,
                saldo
            });
        }

        [HttpGet("resumo-geral/{usuarioId}")]
        public async Task<IActionResult> ResumoGeral(Guid usuarioId, [FromQuery] string periodo = "mensal")
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

            var receitaAgendamentos = agendamentos.Sum(a => a.ValorCobrado);
            var custoAgendamentos = agendamentos.Sum(a => a.CustoMaterial);

            var registros = await _context.Financeiro
                .Find(f => f.UsuarioId == usuarioId
                       && f.Data >= dataInicio
                       && f.Data <= agora)
                .ToListAsync();

            var entradasAvulsas = registros.Where(f => f.Tipo == TipoFinanceiro.Entrada).Sum(f => f.Valor);
            var saidasAvulsas = registros.Where(f => f.Tipo == TipoFinanceiro.Saida).Sum(f => f.Valor);

            var lucroBruto = receitaAgendamentos + entradasAvulsas;
            var lucroLiquido = lucroBruto - custoAgendamentos - saidasAvulsas;

            return Ok(new
            {
                periodo,
                dataInicio,
                dataFim = agora,
                receitaAgendamentos,
                custoAgendamentos,
                entradasAvulsas,
                saidasAvulsas,
                lucroBruto,
                lucroLiquido,
                totalServicos = agendamentos.Count
            });
        }

        [HttpGet("relatorio/{usuarioId}")]
        public async Task<IActionResult> GerarRelatorio(Guid usuarioId, [FromQuery] string periodo = "mensal")
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

            var registros = await _context.Financeiro
                .Find(f => f.UsuarioId == usuarioId
                       && f.Data >= dataInicio
                       && f.Data <= agora)
                .ToListAsync();

            var clienteIds = agendamentos.Select(a => a.ClienteId).Distinct().ToList();
            var tipoIds = agendamentos.Select(a => a.TipoServicoId).Distinct().ToList();

            var clientes = await _context.Cliente
                .Find(c => clienteIds.Contains(c.Id))
                .ToListAsync();

            var tiposServico = await _context.TipoServico
                .Find(t => tipoIds.Contains(t.Id))
                .ToListAsync();

            var receitaAgendamentos = agendamentos.Sum(a => a.ValorCobrado);
            var custoAgendamentos = agendamentos.Sum(a => a.CustoMaterial);
            var entradasAvulsas = registros.Where(f => f.Tipo == TipoFinanceiro.Entrada).Sum(f => f.Valor);
            var saidasAvulsas = registros.Where(f => f.Tipo == TipoFinanceiro.Saida).Sum(f => f.Valor);
            var lucroBruto = receitaAgendamentos + entradasAvulsas;
            var lucroLiquido = lucroBruto - custoAgendamentos - saidasAvulsas;

            var pdf = _relatorioPdfService.GerarRelatorioFinanceiro(
                periodo, dataInicio, agora,
                receitaAgendamentos, custoAgendamentos,
                entradasAvulsas, saidasAvulsas,
                lucroBruto, lucroLiquido,
                agendamentos.Count, agendamentos, registros,
                clientes, tiposServico);

            return File(pdf, "application/pdf", $"relatorio-{periodo}-{agora:yyyyMMdd}.pdf");
        }
    }
}