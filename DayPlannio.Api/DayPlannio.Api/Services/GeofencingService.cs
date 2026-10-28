using DayPlannio.Api.Models;
using MongoDB.Driver;

namespace DayPlannio.Api.Services
{
    public class ResultadoLocalizacao
    {
        public bool Rastrear { get; set; }

        public int IntervaloSegundos { get; set; } = 60;

        public List<EventoAgendamento> Eventos { get; set; } = new();

        public List<LeituraAgendamento> Leituras { get; set; } = new();
    }

    public class LeituraAgendamento
    {
        public Guid AgendamentoId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime DataHora { get; set; }
        public double? DistanciaMetros { get; set; }
        public int? RaioMetros { get; set; }
        public bool Ignorada { get; set; }
        public string Observacao { get; set; } = string.Empty;
    }

    public class EventoAgendamento
    {
        public Guid AgendamentoId { get; set; }
        public string Evento { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class GeofencingService
    {
        private const int IntervaloPertoSegundos = 30;
        private const int IntervaloLongeSegundos = 300;

        private readonly ContextMongodb _context;
        private readonly LogService _log;

        public GeofencingService(ContextMongodb context, LogService log)
        {
            _context = context;
            _log = log;
        }

        public async Task<ResultadoLocalizacao> ProcessarAsync(
            Guid usuarioId, double latitude, double longitude, double precisaoM, DateTime agora)
        {
            var resultado = new ResultadoLocalizacao();

            var ativos = new[] { StatusAgendamento.EmAtendimento, StatusAgendamento.EmPausa };
            var fb = Builders<Agendamento>.Filter;

            var filtro = fb.Eq(a => a.UsuarioId, usuarioId) & (
                fb.In(a => a.Status, ativos) |
                (fb.Eq(a => a.Status, StatusAgendamento.Agendado)
                    & fb.Gte(a => a.DataHora, agora - GeofenceRegras.JanelaDepois)
                    & fb.Lte(a => a.DataHora, agora + GeofenceRegras.JanelaAntes)));

            var candidatos = await _context.Agendamento.Find(filtro).SortBy(a => a.DataHora).ToListAsync();

            if (candidatos.Count == 0)
            {
                resultado.Rastrear = false;
                return resultado;
            }

            var clienteIds = candidatos.Select(a => a.ClienteId).Distinct().ToList();
            var clientes = await _context.Cliente.Find(c => clienteIds.Contains(c.Id)).ToListAsync();
            var porId = clientes.ToDictionary(c => c.Id);

            var limiteJanela = agora + GeofenceRegras.JanelaAntes;
            var algumPerto = false;
            var rastreaveis = 0;

            var ordem = candidatos
                .OrderBy(a => a.Status == StatusAgendamento.Agendado ? 1 : 0)
                .ThenBy(a => a.DataHora)
                .ToList();

            var chegouEm = new List<Agendamento>();

            var disputando = new Dictionary<Guid, (double distancia, int raio)>();
            foreach (var ag in ordem.Where(a => a.Status == StatusAgendamento.Agendado && a.DataHora <= limiteJanela))
            {
                if (!porId.TryGetValue(ag.ClienteId, out var c)) continue;

                var lat = ag.Latitude ?? c.Latitude;
                var lon = ag.Longitude ?? c.Longitude;
                if (lat == null || lon == null) continue;

                var raioAg = GeofenceRegras.NormalizarRaio(c.RaioGeofenceMetros);
                var dist = GeofenceRegras.DistanciaMetros(latitude, longitude, lat.Value, lon.Value);

                if (ag.ChegadaEm.HasValue || dist <= raioAg)
                    disputando[ag.Id] = (dist, raioAg);
            }

            Guid? agendadoEscolhido = ordem
                .Where(a => disputando.ContainsKey(a.Id) && a.ChegadaEm.HasValue)
                .OrderBy(a => a.ChegadaEm)
                .Select(a => (Guid?)a.Id)
                .FirstOrDefault()
                ?? ordem
                    .Where(a => disputando.ContainsKey(a.Id))
                    .OrderBy(a => Math.Abs((a.DataHora - agora).Ticks))
                    .ThenBy(a => disputando[a.Id].distancia)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault();

            foreach (var ag in ordem)
            {
                porId.TryGetValue(ag.ClienteId, out var cli);

                var alvoLat = ag.Latitude ?? cli?.Latitude;
                var alvoLon = ag.Longitude ?? cli?.Longitude;
                var nomeCliente = cli?.Nome ?? "(cliente removido)";

                if (cli == null || alvoLat == null || alvoLon == null)
                {
                    resultado.Leituras.Add(new LeituraAgendamento
                    {
                        AgendamentoId = ag.Id,
                        Cliente = nomeCliente,
                        Status = ag.Status.ToString(),
                        DataHora = ag.DataHora,
                        Observacao = "sem localização (cadastre o endereço no mapa do cliente ou do agendamento)"
                    });
                    continue;
                }

                var emAndamento = ag.Status != StatusAgendamento.Agendado;
                var naJanela = emAndamento || ag.DataHora <= limiteJanela;

                rastreaveis++;
                if (!naJanela)
                {
                    resultado.Leituras.Add(new LeituraAgendamento
                    {
                        AgendamentoId = ag.Id,
                        Cliente = nomeCliente,
                        Status = ag.Status.ToString(),
                        DataHora = ag.DataHora,
                        Observacao = "fora da janela de horário configurada"
                    });
                    continue;
                }
                algumPerto = true;

                if (!emAndamento && candidatos.Any(o =>
                        o.Id != ag.Id && o.ClienteId == ag.ClienteId &&
                        o.Status is StatusAgendamento.EmAtendimento or StatusAgendamento.EmPausa))
                    continue;

                if (!emAndamento && disputando.TryGetValue(ag.Id, out var disputa))
                {
                    var ativoEmOutroCliente = candidatos.Any(o =>
                        o.Id != ag.Id && o.ClienteId != ag.ClienteId && o.Status == StatusAgendamento.EmAtendimento);

                    if (ativoEmOutroCliente || agendadoEscolhido != ag.Id)
                    {
                        resultado.Leituras.Add(new LeituraAgendamento
                        {
                            AgendamentoId = ag.Id,
                            Cliente = nomeCliente,
                            Status = ag.Status.ToString(),
                            DataHora = ag.DataHora,
                            DistanciaMetros = Math.Round(disputa.distancia),
                            RaioMetros = disputa.raio,
                            Observacao = ativoEmOutroCliente
                                ? "há outro atendimento em andamento neste local"
                                : "outro agendamento tem prioridade neste local (horário mais próximo)"
                        });
                        continue;
                    }
                }

                var raio = GeofenceRegras.NormalizarRaio(cli.RaioGeofenceMetros);
                var distancia = GeofenceRegras.DistanciaMetros(latitude, longitude, alvoLat.Value, alvoLon.Value);

                var statusAntes = ag.Status;
                var antes = (ag.ChegadaEm, ag.UltimoPingEm);

                var evento = GeofenceRegras.Avaliar(ag, distancia, raio, precisaoM, agora);

                var precisaoMax = GeofenceRegras.PrecisaoMaximaAceita(raio);
                resultado.Leituras.Add(new LeituraAgendamento
                {
                    AgendamentoId = ag.Id,
                    Cliente = nomeCliente,
                    Status = ag.Status.ToString(),
                    DataHora = ag.DataHora,
                    DistanciaMetros = Math.Round(distancia),
                    RaioMetros = raio,
                    Ignorada = precisaoM > precisaoMax,
                    Observacao = DescreverSituacao(ag, evento, distancia, raio, precisaoM, precisaoMax, agora)
                });

                var mudou = evento != EventoGeofence.Nenhum
                            || antes != (ag.ChegadaEm, ag.UltimoPingEm);

                if (!mudou) continue;

                if (evento == EventoGeofence.Chegou)
                    chegouEm.Add(ag);

                await SalvarAsync(ag, statusAntes);

                if (evento != EventoGeofence.Nenhum)
                {
                    resultado.Eventos.Add(new EventoAgendamento
                    {
                        AgendamentoId = ag.Id,
                        Evento = evento.ToString(),
                        Status = ag.Status.ToString()
                    });

                    await RegistrarLogAsync(ag, cli, evento, usuarioId);
                }
            }

            foreach (var novo in chegouEm)
            {
                var pausados = candidatos.Where(a =>
                    a.Status == StatusAgendamento.EmPausa && a.ClienteId != novo.ClienteId).ToList();

                foreach (var p in pausados)
                {
                    var statusAntes = p.Status;
                    GeofenceRegras.Concluir(p, p.InicioPausa ?? agora);
                    await SalvarAsync(p, statusAntes);

                    resultado.Eventos.Add(new EventoAgendamento
                    {
                        AgendamentoId = p.Id,
                        Evento = EventoGeofence.Concluiu.ToString(),
                        Status = p.Status.ToString()
                    });

                    if (porId.TryGetValue(p.ClienteId, out var cliP))
                        await RegistrarLogAsync(p, cliP, EventoGeofence.Concluiu, usuarioId, "chegou a outro cliente");
                }
            }

            var restantes = candidatos.Any(a => a.Status is StatusAgendamento.EmAtendimento or StatusAgendamento.EmPausa
                || a.Status == StatusAgendamento.Agendado);

            resultado.Rastrear = rastreaveis > 0 && restantes;
            resultado.IntervaloSegundos = algumPerto ? IntervaloPertoSegundos : IntervaloLongeSegundos;
            return resultado;
        }

        private static string DescreverSituacao(
            Agendamento ag, EventoGeofence evento, double distancia, int raio,
            double precisaoM, double precisaoMax, DateTime agora)
        {
            if (precisaoM > precisaoMax)
                return $"leitura ignorada: precisão de {precisaoM:0} m é pior que o máximo de {precisaoMax:0} m";

            switch (evento)
            {
                case EventoGeofence.Chegou:
                    return $"entrou no raio; confirma em {(int)GeofenceRegras.ConfirmacaoChegada.TotalSeconds} s se continuar lá";
                case EventoGeofence.ChegadaDescartada:
                    return "saiu do raio antes de confirmar a chegada";
                case EventoGeofence.Iniciou:
                    return "chegada confirmada: atendimento iniciado";
                case EventoGeofence.Pausou:
                    return "saiu do raio: em pausa";
                case EventoGeofence.Retomou:
                    return "voltou ao raio: atendimento retomado";
                case EventoGeofence.Concluiu:
                    return "atendimento concluído";
            }

            switch (ag.Status)
            {
                case StatusAgendamento.Agendado when ag.ChegadaEm.HasValue:
                    var falta = GeofenceRegras.ConfirmacaoChegada - (agora - ag.ChegadaEm.Value);
                    return $"aguardando confirmação da chegada ({Math.Max(0, (int)falta.TotalSeconds)} s)";
                case StatusAgendamento.Agendado:
                    return distancia <= raio ? "dentro do raio" : "ainda fora do raio";
                case StatusAgendamento.EmAtendimento:
                    return "em atendimento, dentro do raio";
                case StatusAgendamento.EmPausa when ag.InicioPausa.HasValue:
                    var resta = GeofenceRegras.LimiteForaDoRaio - (agora - ag.InicioPausa.Value);
                    return $"em pausa: conclui em {Math.Max(0, (int)resta.TotalSeconds)} s se não voltar ao raio";
                default:
                    return ag.Status.ToString();
            }
        }

        private Task SalvarAsync(Agendamento ag, StatusAgendamento statusAntes) =>
            _context.Agendamento.ReplaceOneAsync(a => a.Id == ag.Id && a.Status == statusAntes, ag);

        private Task RegistrarLogAsync(Agendamento ag, Cliente cli, EventoGeofence evento, Guid usuarioId, string? extra = null)
        {
            var texto = evento switch
            {
                EventoGeofence.Chegou => $"Prestador chegou ao raio de {cli.Nome}",
                EventoGeofence.ChegadaDescartada => $"Prestador saiu do raio de {cli.Nome} antes de confirmar o atendimento",
                EventoGeofence.Iniciou => $"Atendimento de {cli.Nome} iniciado automaticamente",
                EventoGeofence.Pausou => $"Atendimento de {cli.Nome} pausado (prestador fora do raio)",
                EventoGeofence.Retomou => $"Atendimento de {cli.Nome} retomado",
                EventoGeofence.Concluiu => $"Atendimento de {cli.Nome} concluído automaticamente" +
                                           (extra != null ? $" ({extra})" : " (30 min fora do raio)"),
                _ => string.Empty
            };

            return string.IsNullOrEmpty(texto)
                ? Task.CompletedTask
                : _log.RegistrarAsync($"geofence_{evento.ToString().ToLowerInvariant()}", texto, "mobile", usuarioId.ToString());
        }
    }
}
