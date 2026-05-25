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
        private static readonly TimeZoneInfo _fuso =
            TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");

        public FinanceirosController(RelatorioPdfService relatorioPdfService)
        {
            _relatorioPdfService = relatorioPdfService;
        }

        private static (DateTime inicioUtc, DateTime fimUtc, DateTime agoraLocal) ObterIntervalo(string periodo)
        {
            var agoraLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _fuso);

            DateTime dataInicio;
            DateTime dataFim;

            switch (periodo.ToLower())
            {
                case "diario":
                    dataInicio = agoraLocal.Date;
                    dataFim = dataInicio.AddDays(1);
                    break;

                case "semanal":
                    dataInicio = agoraLocal.Date.AddDays(
                        agoraLocal.DayOfWeek == DayOfWeek.Sunday
                            ? -6
                            : -(int)agoraLocal.DayOfWeek + 1);
                    dataFim = dataInicio.AddDays(7);
                    break;

                case "mensal":
                default:
                    dataInicio = new DateTime(agoraLocal.Year, agoraLocal.Month, 1);
                    dataFim = dataInicio.AddMonths(1);
                    break;
            }

            var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(dataInicio, DateTimeKind.Unspecified), _fuso);

            var fimUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(dataFim, DateTimeKind.Unspecified), _fuso);

            return (inicioUtc, fimUtc, agoraLocal);
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
        public async Task<IActionResult> Resumo(
            Guid usuarioId,
            [FromQuery] string periodo = "mensal")
        {
            var (inicioUtc, fimUtc, _) = ObterIntervalo(periodo);

            var registros = await _context.Financeiro
                .Find(f =>
                    f.UsuarioId == usuarioId &&
                    f.Data >= inicioUtc &&
                    f.Data < fimUtc)
                .ToListAsync();

            var totalEntradas = registros
                .Where(f => f.Tipo == TipoFinanceiro.Entrada)
                .Sum(f => f.Valor);

            var totalSaidas = registros
                .Where(f => f.Tipo == TipoFinanceiro.Saida)
                .Sum(f => f.Valor);

            var saldo = totalEntradas - totalSaidas;

            return Ok(new
            {
                periodo,
                dataInicio = inicioUtc,
                dataFim = fimUtc,
                totalEntradas,
                totalSaidas,
                saldo
            });
        }

        [HttpGet("resumo-geral/{usuarioId}")]
        public async Task<IActionResult> ResumoGeral(
            Guid usuarioId,
            [FromQuery] string periodo = "mensal")
        {
            var (inicioUtc, fimUtc, _) = ObterIntervalo(periodo);

            var agendamentos = await _context.Agendamento
                .Find(a =>
                    a.UsuarioId == usuarioId &&
                    a.Status == StatusAgendamento.Concluido &&
                    a.DataConclusao.HasValue &&
                    a.DataConclusao.Value >= inicioUtc &&
                    a.DataConclusao.Value < fimUtc)
                .ToListAsync();

            var receitaAgendamentos = agendamentos.Sum(a => a.ValorCobrado);
            var custoAgendamentos = agendamentos.Sum(a => a.CustoMaterial);

            var registros = await _context.Financeiro
                .Find(f =>
                    f.UsuarioId == usuarioId &&
                    f.Data >= inicioUtc &&
                    f.Data < fimUtc)
                .ToListAsync();

            var entradasAvulsas = registros
                .Where(f => f.Tipo == TipoFinanceiro.Entrada)
                .Sum(f => f.Valor);

            var saidasAvulsas = registros
                .Where(f => f.Tipo == TipoFinanceiro.Saida)
                .Sum(f => f.Valor);

            var lucroBruto = receitaAgendamentos + entradasAvulsas;
            var lucroLiquido = lucroBruto - custoAgendamentos - saidasAvulsas;

            return Ok(new
            {
                periodo,
                dataInicio = inicioUtc,
                dataFim = fimUtc,
                receitaAgendamentos,
                custoAgendamentos,
                entradasAvulsas,
                saidasAvulsas,
                lucroBruto,
                lucroLiquido,
                totalServicos = agendamentos.Count
            });
        }

        [HttpGet("{usuarioId}/periodo")]
        public async Task<IActionResult> GetByPeriodo(Guid usuarioId, [FromQuery] string periodo = "diario")
        {
            var (inicioUtc, fimUtc, _) = ObterIntervalo(periodo);

            var registros = await _context.Financeiro
                .Find(f =>
                    f.UsuarioId == usuarioId &&
                    f.Data >= inicioUtc &&
                    f.Data < fimUtc)
                .SortByDescending(f => f.Data)
                .ToListAsync();

            return Ok(registros);
        }

        [HttpGet("relatorio/{usuarioId}")]
        public async Task<IActionResult> GerarRelatorio(
            Guid usuarioId,
            [FromQuery] string periodo = "mensal")
        {
            var (inicioUtc, fimUtc, agoraLocal) = ObterIntervalo(periodo);

            var agendamentos = await _context.Agendamento
                .Find(a =>
                    a.UsuarioId == usuarioId &&
                    a.Status == StatusAgendamento.Concluido &&
                    a.DataConclusao.HasValue &&
                    a.DataConclusao.Value >= inicioUtc &&
                    a.DataConclusao.Value <= fimUtc)
                .ToListAsync();

            var registros = await _context.Financeiro
                .Find(f =>
                    f.UsuarioId == usuarioId &&
                    f.Data >= inicioUtc &&
                    f.Data <= fimUtc)
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
                periodo, inicioUtc, agoraLocal,
                receitaAgendamentos, custoAgendamentos,
                entradasAvulsas, saidasAvulsas,
                lucroBruto, lucroLiquido,
                agendamentos.Count, agendamentos, registros,
                clientes, tiposServico);

            return File(pdf, "application/pdf", $"relatorio-{periodo}-{agoraLocal:yyyyMMdd}.pdf");
        }
    }
}