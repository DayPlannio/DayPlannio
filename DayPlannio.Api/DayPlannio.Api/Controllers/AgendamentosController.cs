using DayPlannio.Api.Models;
using DayPlannio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Globalization;
using System.Text;

namespace DayPlannio.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AgendamentosController : ControllerBase
    {
        private readonly ContextMongodb _context = new ContextMongodb();
        private readonly LogService _logService;

        public AgendamentosController(LogService logService)
        {
            _logService = logService;
        }

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
                .Find(a => a.UsuarioId == agendamento.UsuarioId && a.DataHora == agendamento.DataHora && a.Status != StatusAgendamento.Cancelado && a.Status != StatusAgendamento.Concluido)
                .FirstOrDefaultAsync();

            if (agendamentoExistente != null)
            {
                return BadRequest(new { message = "Já existe um agendamento para este horário." });
            }

            agendamento.Id = Guid.NewGuid();
            agendamento.CreatedAt = DateTime.UtcNow;
            agendamento.Status = StatusAgendamento.Agendado;

            await _context.Agendamento.InsertOneAsync(agendamento);

            var (clienteNomeCriado, servicoNomeCriado) = await ObterNomesAsync(agendamento.ClienteId, agendamento.TipoServicoId);

            await _logService.RegistrarAsync(
                "agendamento_criado", $"{servicoNomeCriado} para {clienteNomeCriado} criado para {agendamento.DataHora:dd/MM/yyyy HH:mm}",
                "mobile", agendamento.UsuarioId.ToString());

            return Ok(new { message = "Agendamento criado com sucesso.", id = agendamento.Id });
        }

        [HttpPut("edit/{id}")]
        public async Task<IActionResult> Edit(Guid id, [FromBody] Agendamento agendamento)
        {
            var existing = await _context.Agendamento.Find(a => a.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Agendamento não encontrado." });

            var conflito = await _context.Agendamento
                .Find(a => a.UsuarioId == existing.UsuarioId
                        && a.DataHora == agendamento.DataHora
                        && a.Status != StatusAgendamento.Cancelado
                        && a.Id != id)
                .FirstOrDefaultAsync();

            if (conflito != null)
                return BadRequest(new { message = "Já existe um agendamento para este horário." });

            existing.ClienteId = agendamento.ClienteId;
            existing.TipoServicoId = agendamento.TipoServicoId;
            existing.DataHora = agendamento.DataHora;
            existing.Observacoes = agendamento.Observacoes;
            existing.ValorCobrado = agendamento.ValorCobrado;
            existing.CustoMaterial = agendamento.CustoMaterial;

            existing.EnderecoAtendimento = agendamento.EnderecoAtendimento;

            await _context.Agendamento.ReplaceOneAsync(a => a.Id == id, existing);

            var (clienteNomeEditado, servicoNomeEditado) = await ObterNomesAsync(existing.ClienteId, existing.TipoServicoId);

            await _logService.RegistrarAsync(
                "agendamento_editado", $"{servicoNomeEditado} para {clienteNomeEditado} atualizado",
                "mobile", existing.UsuarioId.ToString());

            return Ok(new { message = "Agendamento atualizado com sucesso." });
        }

        [HttpPut("cancelar/{id}")]
        public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarAgendamentoRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Motivo))
                return BadRequest(new { message = "Informe o motivo do cancelamento." });

            var existing = await _context.Agendamento.Find(a => a.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Agendamento não encontrado." });

            if (existing.Status == StatusAgendamento.Cancelado)
                return BadRequest(new { message = "Este agendamento já foi cancelado." });

            existing.Status = StatusAgendamento.Cancelado;
            existing.MotivoCancelamento = request.Motivo.Trim();
            existing.DataCancelamento = DateTime.UtcNow;

            await _context.Agendamento.ReplaceOneAsync(a => a.Id == id, existing);

            var (clienteNomeCancelado, servicoNomeCancelado) = await ObterNomesAsync(existing.ClienteId, existing.TipoServicoId);

            await _logService.RegistrarAsync(
                "agendamento_cancelado", $"{servicoNomeCancelado} para {clienteNomeCancelado} cancelado. Motivo: {existing.MotivoCancelamento}",
                "mobile", existing.UsuarioId.ToString());

            return Ok(new { message = "Agendamento cancelado com sucesso." });
        }

        [HttpPut("concluir/{id}")]
        public async Task<IActionResult> Concluir(Guid id)
        {
            var existing = await _context.Agendamento.Find(a => a.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Agendamento não encontrado." });

            existing.Status = StatusAgendamento.Concluido;
            existing.DataConclusao = DateTime.UtcNow;

            if (existing.Inicio.HasValue && !existing.Fim.HasValue)
            {
                existing.Fim = existing.DataConclusao;
                existing.DuracaoMinutos = Math.Round((existing.Fim.Value - existing.Inicio.Value).TotalMinutes, 1);
            }

            await _context.Agendamento.ReplaceOneAsync(a => a.Id == id, existing);

            var (clienteNomeConcluido, servicoNomeConcluido) = await ObterNomesAsync(existing.ClienteId, existing.TipoServicoId);
            var duracaoTexto = FormatarDuracao(existing.DuracaoMinutos);

            await _logService.RegistrarAsync(
                "agendamento_concluido", $"{servicoNomeConcluido} para {clienteNomeConcluido} concluído com duração de {duracaoTexto}",
                "mobile", existing.UsuarioId.ToString());

            return Ok(new { message = "Agendamento concluído com sucesso." });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await _context.Agendamento.Find(a => a.Id == id).FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { message = "Agendamento não encontrado." });

            await _context.Agendamento.DeleteOneAsync(a => a.Id == id);

            var (clienteNomeDeletado, servicoNomeDeletado) = await ObterNomesAsync(existing.ClienteId, existing.TipoServicoId);

            await _logService.RegistrarAsync(
                "agendamento_deletado", $"{servicoNomeDeletado} para {clienteNomeDeletado} deletado",
                "mobile", existing.UsuarioId.ToString());

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
                    dataInicio = agora.Date.AddDays(-(int)agora.DayOfWeek);
                    dataFim = dataInicio.AddDays(7);
                    break;

                case "mensal":
                    dataInicio = new DateTime(agora.Year, agora.Month, 1);
                    dataFim = dataInicio.AddMonths(1);
                    break;

                case "proximos":
                    dataInicio = agora.Date.AddDays(-1);
                    dataFim = agora.Date.AddDays(9);
                    break;

                default:
                    dataInicio = new DateTime(agora.Year, agora.Month, 1);
                    dataFim = dataInicio.AddMonths(1);
                    break;
            }

            var agendamentos = await _context.Agendamento
                .Find(a => a.UsuarioId == usuarioId
                       && a.DataHora >= dataInicio
                       && a.DataHora < dataFim)
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

        [HttpGet("historico-completo/{clienteId}")]
        public async Task<IActionResult> HistoricoCompleto(Guid clienteId, Guid usuarioId)
        {
            var prestadorCadastrado = await _context.ApplicationUsers
                .Find(u => u.Id == usuarioId)
                .AnyAsync();

            if (!prestadorCadastrado)
                return StatusCode(410, new { message = PlanoService.MensagemPrestadorRemovido });

            var filtro = Builders<Agendamento>.Filter.And(
                Builders<Agendamento>.Filter.Eq(a => a.ClienteId, clienteId),
                Builders<Agendamento>.Filter.Eq(a => a.UsuarioId, usuarioId));

            var agendamentos = await _context.Agendamento
                .Find(filtro)
                .SortByDescending(a => a.DataHora)
                .ToListAsync();

            var nomesServicos = await _context.TipoServico
                .Find(s => s.UsuarioId == usuarioId)
                .ToListAsync();

            var nomePorId = nomesServicos.ToDictionary(s => s.Id, s => s.Tipo);

            var nomePrestador = (await _context.ApplicationUsers
                .Find(u => u.Id == usuarioId)
                .FirstOrDefaultAsync())?.NomeCompleto ?? "Profissional";

            return Ok(agendamentos.Select(a => new
            {
                servico = nomePorId.GetValueOrDefault(a.TipoServicoId, "Serviço"),
                data = a.DataConclusao ?? a.DataHora,
                profissional = nomePrestador,
                inicio = a.Inicio,
                fim = a.Fim,
                duracaoMinutos = a.DuracaoMinutos,
                valor = a.ValorCobrado,
                status = a.Status.ToString()
            }));
        }

        [HttpGet("metricas/{usuarioId}")]
        public async Task<IActionResult> Metricas(Guid usuarioId, [FromQuery] string periodo = "mensal", [FromQuery] bool mock = false)
        {
            if (mock && IsDevelopment())
                return Ok(GerarMetricasMock(periodo));

            var accessoMetricas = await PlanoService.VerificarAsync(usuarioId, PlanoService.Profissional, _context);
            if (!accessoMetricas.autorizado)
                return StatusCode(403, new { message = accessoMetricas.mensagem });

            var agora = DateTime.UtcNow;

            DateTime dataInicio;
            DateTime dataFim;

            switch (periodo)
            {
                case "diario":
                    dataInicio = agora.Date;
                    dataFim = dataInicio.AddDays(1);
                    break;

                case "semanal":
                    dataInicio = agora.Date.AddDays(-(int)agora.DayOfWeek);
                    dataFim = dataInicio.AddDays(7);
                    break;

                case "mensal":
                default:
                    dataInicio = new DateTime(agora.Year, agora.Month, 1);
                    dataFim = dataInicio.AddMonths(1);
                    break;
            }

            var duracaoPeriodo = dataFim - dataInicio;
            var dataInicioAnterior = dataInicio - duracaoPeriodo;

            var filtro = Builders<Agendamento>.Filter.And(
                Builders<Agendamento>.Filter.Eq(a => a.UsuarioId, usuarioId),
                Builders<Agendamento>.Filter.Eq(a => a.Status, StatusAgendamento.Concluido));

            var concluidos = await _context.Agendamento
                .Find(filtro)
                .ToListAsync();

            var atuais = concluidos
                .Where(a => a.DataConclusao.HasValue && a.DataConclusao.Value >= dataInicio && a.DataConclusao.Value < dataFim)
                .ToList();

            var anteriores = concluidos
                .Where(a => a.DataConclusao.HasValue && a.DataConclusao.Value >= dataInicioAnterior && a.DataConclusao.Value < dataInicio)
                .ToList();

            var atendimentos = atuais.Count;
            var clientes = atuais.Select(a => a.ClienteId).Distinct().Count();
            var minutosTrabalhados = atuais.Where(a => a.DuracaoMinutos.HasValue).Sum(a => a.DuracaoMinutos!.Value);
            var faturamento = atuais.Sum(a => a.ValorCobrado);

            var atendimentosAnterior = anteriores.Count;
            var clientesAnterior = anteriores.Select(a => a.ClienteId).Distinct().Count();
            var minutosAnterior = anteriores.Where(a => a.DuracaoMinutos.HasValue).Sum(a => a.DuracaoMinutos!.Value);
            var faturamentoAnterior = anteriores.Sum(a => a.ValorCobrado);

            var nomesServicos = await _context.TipoServico
                .Find(s => s.UsuarioId == usuarioId)
                .ToListAsync();

            var nomePorId = nomesServicos.ToDictionary(s => s.Id, s => s.Tipo);

            var porServico = atuais
                .GroupBy(a => a.TipoServicoId)
                .OrderByDescending(g => g.Count())
                .Select(g => new
                {
                    servico = nomePorId.GetValueOrDefault(g.Key, "Serviço removido"),
                    quantidade = g.Count(),
                    percentual = atendimentos > 0 ? Math.Round(g.Count() * 100.0 / atendimentos, 1) : 0,
                    faturamento = g.Sum(a => a.ValorCobrado),
                    tempoMedioMinutos = g.Any(a => a.DuracaoMinutos.HasValue)
                        ? Math.Round(g.Where(a => a.DuracaoMinutos.HasValue).Average(a => a.DuracaoMinutos.Value), 1)
                        : (double?)null
                })
                .ToList();

            var evolucao = new List<object>();
            for (var dia = dataInicio.ToLocalTime().Date; dia < dataFim.ToLocalTime().Date; dia = dia.AddDays(1))
            {
                var quantidade = atuais.Count(a => a.DataConclusao.HasValue && a.DataConclusao.Value.ToLocalTime().Date == dia);
                evolucao.Add(new { data = dia, quantidade });
            }

            var maioresAtendimentos = atuais
                .Where(a => a.DuracaoMinutos.HasValue)
                .OrderByDescending(a => a.DuracaoMinutos.Value)
                .Take(5)
                .Select(a => new
                {
                    servico = nomePorId.GetValueOrDefault(a.TipoServicoId, "Serviço"),
                    duracaoMinutos = a.DuracaoMinutos.Value
                })
                .ToList();

            var comDuracao = atuais.Where(a => a.DuracaoMinutos.HasValue).ToList();
            var resumoDeTempo = new
            {
                maior = comDuracao.Any() ? comDuracao.Max(a => a.DuracaoMinutos.Value) : (double?)null,
                menor = comDuracao.Any() ? comDuracao.Min(a => a.DuracaoMinutos.Value) : (double?)null,
                medio = comDuracao.Any() ? Math.Round(comDuracao.Average(a => a.DuracaoMinutos.Value), 1) : (double?)null,
                totalMinutos = minutosTrabalhados
            };

            var cancelados = await _context.Agendamento
                .Find(a => a.UsuarioId == usuarioId && a.Status == StatusAgendamento.Cancelado)
                .ToListAsync();

            var canceladosNoPeriodo = cancelados
                .Where(a => a.DataCancelamento.HasValue
                            && a.DataCancelamento.Value >= dataInicio
                            && a.DataCancelamento.Value < dataFim)
                .ToList();

            var canceladosComMotivo = canceladosNoPeriodo
                .Where(a => !string.IsNullOrWhiteSpace(a.MotivoCancelamento))
                .ToList();

            var motivosCancelamento = AnalisarMotivosCancelamento(canceladosComMotivo);

            var totalFinalizados = atendimentos + canceladosNoPeriodo.Count;

            return Ok(new
            {
                periodo,
                dataInicio,
                dataFim,
                atendimentos,
                clientes,
                horasTrabalhadas = Math.Round(minutosTrabalhados / 60.0, 1),
                faturamento,
                cancelamentos = canceladosNoPeriodo.Count,
                totalFinalizados,
                taxaCancelamento = totalFinalizados > 0
                    ? Math.Round(canceladosNoPeriodo.Count * 100.0 / totalFinalizados, 1)
                    : 0,
                cancelamentosComMotivo = canceladosComMotivo.Count,
                motivosCancelamento,
                principalMotivoCancelamento = motivosCancelamento.Length > 0 ? motivosCancelamento[0] : null,
                variacoes = new
                {
                    atendimentos = CalcularVariacao(atendimentos, atendimentosAnterior),
                    clientes = CalcularVariacao(clientes, clientesAnterior),
                    horasTrabalhadas = CalcularVariacao(minutosTrabalhados, minutosAnterior),
                    faturamento = CalcularVariacao((double)faturamento, (double)faturamentoAnterior)
                },
                porServico,
                evolucao,
                maioresAtendimentos,
                resumoDeTempo
            });
        }

        private static double CalcularVariacao(double atual, double anterior)
        {
            if (anterior <= 0)
                return atual > 0 ? 100 : 0;

            return Math.Round((atual - anterior) * 100.0 / anterior, 1);
        }

        private static readonly HashSet<string> StopwordsCancelamento = new(StringComparer.OrdinalIgnoreCase)
        {
            "o","a","os","as","um","uma","uns","umas",
            "de","da","do","das","dos",
            "em","no","na","nos","nas",
            "e","com","por","para","que","se",
            "ao","aos","mas","mais","ou","como","porque","nao","muito",
            "meu","minha","meus","minhas",
            "eu","ele","ela","eles","elas","voce",
            "ser","foi","era","tive","teve","esta","estava","fiquei"
        };

        private static object[] AnalisarMotivosCancelamento(List<Agendamento> cancelados)
        {
            var contagemPalavras = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var agendamento in cancelados)
            {
                var texto = RemoverAcentos(agendamento.MotivoCancelamento!.ToLowerInvariant());

                foreach (var palavra in texto.Split(
                    new[] { ' ', '\t', '\n', '\r', ',', '.', ';', ':', '-', '_', '/', '(', ')', '!', '?' },
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    if (palavra.Length < 3 || StopwordsCancelamento.Contains(palavra))
                        continue;

                    contagemPalavras[palavra] = contagemPalavras.GetValueOrDefault(palavra) + 1;
                }
            }

            return contagemPalavras
                .OrderByDescending(par => par.Value)
                .ThenBy(par => par.Key)
                .Take(10)
                .Select(par => new
                {
                    palavra = par.Key,
                    quantidade = par.Value,
                    percentual = cancelados.Count > 0
                        ? Math.Round(par.Value * 100.0 / cancelados.Count, 1)
                        : 0
                })
                .ToArray();
        }

        private static string RemoverAcentos(string texto)
        {
            var normalizado = texto.Normalize(NormalizationForm.FormD);
            var semAcentos = new StringBuilder();

            foreach (var caractere in normalizado)
            {
                var categoria = CharUnicodeInfo.GetUnicodeCategory(caractere);
                if (categoria != UnicodeCategory.NonSpacingMark)
                    semAcentos.Append(caractere);
            }

            return semAcentos.ToString();
        }

        private static bool IsDevelopment()
            => string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                "Development",
                StringComparison.OrdinalIgnoreCase);

        private static object GerarMetricasMock(string periodo)
        {
            var agora = DateTime.UtcNow;

            var porServico = new[]
            {
                new { servico = "Corte de cabelo", quantidade = 12, percentual = 40.0, faturamento = 360.0m, tempoMedioMinutos = 45.0 },
                new { servico = "Manicure", quantidade = 9, percentual = 30.0, faturamento = 270.0m, tempoMedioMinutos = 60.0 },
                new { servico = "Maquiagem", quantidade = 6, percentual = 20.0, faturamento = 300.0m, tempoMedioMinutos = 90.0 },
                new { servico = "Design de sobrancelha", quantidade = 3, percentual = 10.0, faturamento = 90.0m, tempoMedioMinutos = 30.0 }
            };

            var evolucao = new List<object>();
            for (var dia = agora.Date.AddDays(-6); dia <= agora.Date; dia = dia.AddDays(1))
                evolucao.Add(new { data = dia, quantidade = (dia.Day % 3) + 1 });

            var maioresAtendimentos = new[]
            {
                new { servico = "Maquiagem", duracaoMinutos = 120.0 },
                new { servico = "Manicure", duracaoMinutos = 75.0 },
                new { servico = "Corte de cabelo", duracaoMinutos = 50.0 }
            };

            return new
            {
                periodo,
                dataInicio = agora.Date.AddDays(-30),
                dataFim = agora,
                atendimentos = 30,
                clientes = 18,
                horasTrabalhadas = 24.5,
                faturamento = 1020.0m,
                variacoes = new
                {
                    atendimentos = 15.0,
                    clientes = 20.0,
                    horasTrabalhadas = 10.0,
                    faturamento = 25.0
                },
                porServico,
                evolucao,
                maioresAtendimentos,
                resumoDeTempo = new
                {
                    maior = 120.0,
                    menor = 30.0,
                    medio = 60.0,
                    totalMinutos = 1470.0
                }
            };
        }

        [HttpGet("metricas-cliente/{clienteId}")]
        public async Task<IActionResult> MetricasCliente(
            Guid clienteId,
            Guid usuarioId,
            [FromQuery] string periodo = "mensal")
        {
            var prestadorExiste = await _context.ApplicationUsers
                .Find(u => u.Id == usuarioId)
                .AnyAsync();

            if (!prestadorExiste)
                return StatusCode(410, new { message = PlanoService.MensagemPrestadorRemovido });

            var acessoMetricasCliente = await PlanoService.VerificarAsync(usuarioId, PlanoService.Full, _context);
            if (!acessoMetricasCliente.autorizado)
            {
                var mensagem = acessoMetricasCliente.plano == PlanoService.SemPlano
                    ? $"Este recurso exige o plano {PlanoService.NomeExibicao(PlanoService.Full)}. O profissional não possui uma assinatura ativa no momento."
                    : $"Este recurso exige o plano {PlanoService.NomeExibicao(PlanoService.Full)}. O plano atual do profissional é {PlanoService.NomeExibicao(acessoMetricasCliente.plano)}.";
                return StatusCode(403, new { message = mensagem });
            }

            var agora = DateTime.UtcNow;

            DateTime dataInicio;
            DateTime dataFim;

            switch (periodo)
            {
                case "diario":
                    dataInicio = agora.Date;
                    dataFim = dataInicio.AddDays(1);
                    break;

                case "semanal":
                    dataInicio = agora.Date.AddDays(-(int)agora.DayOfWeek);
                    dataFim = dataInicio.AddDays(7);
                    break;

                case "mensal":
                default:
                    dataInicio = new DateTime(agora.Year, agora.Month, 1);
                    dataFim = dataInicio.AddMonths(1);
                    break;
            }

            var duracaoPeriodo = dataFim - dataInicio;
            var dataInicioAnterior = dataInicio - duracaoPeriodo;

            var filtro = Builders<Agendamento>.Filter.And(
                Builders<Agendamento>.Filter.Eq(a => a.ClienteId, clienteId),
                Builders<Agendamento>.Filter.Eq(a => a.UsuarioId, usuarioId),
                Builders<Agendamento>.Filter.Eq(a => a.Status, StatusAgendamento.Concluido));

            var concluidos = await _context.Agendamento
                .Find(filtro)
                .ToListAsync();

            var atuais = concluidos
                .Where(a => a.DataConclusao.HasValue
                            && a.DataConclusao.Value >= dataInicio
                            && a.DataConclusao.Value < dataFim)
                .ToList();

            var anteriores = concluidos
                .Where(a => a.DataConclusao.HasValue
                            && a.DataConclusao.Value >= dataInicioAnterior
                            && a.DataConclusao.Value < dataInicio)
                .ToList();

            var atendimentos = atuais.Count;
            var minutosTrabalhados = atuais.Where(a => a.DuracaoMinutos.HasValue).Sum(a => a.DuracaoMinutos!.Value);
            var valorTotal = atuais.Sum(a => a.ValorCobrado);

            var atendimentosAnterior = anteriores.Count;
            var minutosAnterior = anteriores.Where(a => a.DuracaoMinutos.HasValue).Sum(a => a.DuracaoMinutos!.Value);
            var valorAnterior = anteriores.Sum(a => a.ValorCobrado);

            var nomesServicos = await _context.TipoServico
                .Find(s => s.UsuarioId == usuarioId)
                .ToListAsync();

            var nomePorId = nomesServicos.ToDictionary(s => s.Id, s => s.Tipo);

            var nomePrestador = (await _context.ApplicationUsers
                .Find(u => u.Id == usuarioId)
                .FirstOrDefaultAsync())?.NomeCompleto ?? "Profissional";

            var porServico = atuais
                .GroupBy(a => a.TipoServicoId)
                .OrderByDescending(g => g.Count())
                .Select(g => new
                {
                    categoria = nomePorId.GetValueOrDefault(g.Key, "Serviço removido"),
                    quantidade = g.Count(),
                    percentual = atendimentos > 0 ? Math.Round(g.Count() * 100.0 / atendimentos, 1) : 0,
                    valor = g.Sum(a => a.ValorCobrado),
                    tempoMedioMinutos = g.Any(a => a.DuracaoMinutos.HasValue)
                        ? Math.Round(g.Where(a => a.DuracaoMinutos.HasValue).Average(a => a.DuracaoMinutos.Value), 1)
                        : (double?)null
                })
                .ToList();

            var historico = atuais
                .OrderByDescending(a => a.DataConclusao ?? a.DataHora)
                .Select(a => new
                {
                    servico = nomePorId.GetValueOrDefault(a.TipoServicoId, "Serviço"),
                    data = a.DataConclusao ?? a.DataHora,
                    profissional = nomePrestador,
                    inicio = a.Inicio,
                    fim = a.Fim,
                    duracaoMinutos = a.DuracaoMinutos,
                    valor = a.ValorCobrado,
                    status = a.Status.ToString()
                })
                .ToList();

            var evolucao = new List<object>();
            for (var dia = dataInicio.ToLocalTime().Date; dia < dataFim.ToLocalTime().Date; dia = dia.AddDays(1))
            {
                var quantidade = atuais.Count(a =>
                    a.DataConclusao.HasValue && a.DataConclusao.Value.ToLocalTime().Date == dia);
                evolucao.Add(new { data = dia, quantidade });
            }

            var comDuracao = atuais.Where(a => a.DuracaoMinutos.HasValue).ToList();
            var resumoTempo = new
            {
                maior = comDuracao.Any() ? comDuracao.Max(a => a.DuracaoMinutos.Value) : (double?)null,
                menor = comDuracao.Any() ? comDuracao.Min(a => a.DuracaoMinutos.Value) : (double?)null,
                medio = comDuracao.Any() ? Math.Round(comDuracao.Average(a => a.DuracaoMinutos.Value), 1) : (double?)null,
                totalMinutos = minutosTrabalhados
            };

            var tempoMedioPorServico = porServico
                .Select(p => new
                {
                    categoria = p.categoria,
                    quantidade = p.quantidade,
                    percentual = p.percentual,
                    valor = p.valor,
                    tempoMedioMinutos = p.tempoMedioMinutos
                })
                .ToList();

            var investimentoPorServico = porServico
                .Select(p => new
                {
                    categoria = p.categoria,
                    quantidade = p.quantidade,
                    percentual = p.percentual,
                    valor = p.valor,
                    tempoMedioMinutos = p.tempoMedioMinutos
                })
                .ToList();

            return Ok(new
            {
                periodo,
                resumoGeral = new
                {
                    totalServicos = atendimentos,
                    tempoTotalMinutos = minutosTrabalhados,
                    tempoMedioMinutos = resumoTempo.medio,
                    valorTotal
                },
                variacoes = new
                {
                    atendimentos = CalcularVariacao(atendimentos, atendimentosAnterior),
                    tempo = CalcularVariacao(minutosTrabalhados, minutosAnterior),
                    valor = CalcularVariacao((double)valorTotal, (double)valorAnterior)
                },
                servicosPorCategoria = porServico,
                historico,
                tempoMedioPorServico,
                investimentoPorServico,
                evolucao,
                resumoTempo
            });
        }

        private async Task<(string cliente, string servico)> ObterNomesAsync(Guid clienteId, Guid tipoServicoId)
        {
            var cliente = await _context.Cliente.Find(c => c.Id == clienteId).FirstOrDefaultAsync();
            var servico = await _context.TipoServico.Find(s => s.Id == tipoServicoId).FirstOrDefaultAsync();
            return (cliente?.Nome ?? "cliente", servico?.Tipo ?? "serviço");
        }

        private static string FormatarDuracao(double? minutos)
        {
            if (!minutos.HasValue) return "duração não registrada";

            var total = (int)Math.Round(minutos.Value);
            var horas = total / 60;
            var min = total % 60;

            if (horas > 0 && min > 0) return $"{horas}h{min:00}min";
            if (horas > 0) return $"{horas}h";
            return $"{min}min";
        }

    }

    public class CancelarAgendamentoRequest
    {
        public string? Motivo { get; set; }
    }
}