using DayPlannio.Api.Models;
using DayPlannio.Api.Settings;

namespace DayPlannio.Api.Services
{
    public enum EventoGeofence
    {
        Nenhum,
        Chegou,
        ChegadaDescartada,
        Iniciou,
        Pausou,
        Retomou,
        Concluiu
    }

    public static class GeofenceRegras
    {
        public const int RaioPadraoMetros = 100;
        public static readonly int[] RaiosPermitidos = { 20, 50, 100 };

        public static TimeSpan ConfirmacaoChegada { get; private set; } = TimeSpan.FromMinutes(5);
        public static double PrecisaoMaximaConfigurada { get; private set; } = 0;
        public static readonly TimeSpan LacunaDePing = TimeSpan.FromMinutes(3);

        public static double ToleranciaSaidaMetros { get; private set; } = 15;
        public static TimeSpan LimiteForaDoRaio { get; private set; } = TimeSpan.FromMinutes(30);
        public static TimeSpan JanelaAntes { get; private set; } = TimeSpan.FromHours(24);
        public static TimeSpan JanelaDepois { get; private set; } = TimeSpan.FromHours(24);

        public static void Configurar(GeofenceSettings cfg)
        {
            if (cfg.ConfirmacaoMinutos > 0)
                ConfirmacaoChegada = TimeSpan.FromMinutes(cfg.ConfirmacaoMinutos);

            PrecisaoMaximaConfigurada = Math.Max(0, cfg.PrecisaoMaximaMetros);

            if (cfg.ToleranciaSaidaMetros >= 0)
                ToleranciaSaidaMetros = cfg.ToleranciaSaidaMetros;

            if (cfg.ToleranciaPausaMinutos > 0)
                LimiteForaDoRaio = TimeSpan.FromMinutes(cfg.ToleranciaPausaMinutos);

            if (cfg.JanelaHoras > 0)
            {
                JanelaAntes = TimeSpan.FromHours(cfg.JanelaHoras);
                JanelaDepois = TimeSpan.FromHours(cfg.JanelaHoras);
            }
        }

        public static int NormalizarRaio(int raio) =>
            RaiosPermitidos.Contains(raio) ? raio : RaioPadraoMetros;

        public static double PrecisaoMaximaAceita(int raio) =>
            PrecisaoMaximaConfigurada > 0 ? PrecisaoMaximaConfigurada : Math.Clamp(raio, 25, 50);

        public static double DistanciaMetros(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000;
            static double Rad(double g) => g * Math.PI / 180.0;

            var dLat = Rad(lat2 - lat1);
            var dLon = Rad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        public static EventoGeofence Avaliar(
            Agendamento a, double distanciaM, int raio, double precisaoM, DateTime agora)
        {
            if (precisaoM > PrecisaoMaximaAceita(raio))
                return EventoGeofence.Nenhum;

            var dentro = distanciaM <= raio;
            var fora = distanciaM > raio + ToleranciaSaidaMetros;

            switch (a.Status)
            {
                case StatusAgendamento.Agendado:
                    return AvaliarAgendado(a, dentro, fora, agora);

                case StatusAgendamento.EmAtendimento:
                    if (!fora) return EventoGeofence.Nenhum;
                    a.Status = StatusAgendamento.EmPausa;
                    a.InicioPausa = agora;
                    return EventoGeofence.Pausou;

                case StatusAgendamento.EmPausa:
                    if (dentro)
                    {
                        if (a.InicioPausa.HasValue)
                            a.PausaAcumuladaMinutos += (agora - a.InicioPausa.Value).TotalMinutes;
                        a.InicioPausa = null;
                        a.Status = StatusAgendamento.EmAtendimento;
                        return EventoGeofence.Retomou;
                    }
                    if (a.InicioPausa.HasValue && agora - a.InicioPausa.Value >= LimiteForaDoRaio)
                    {
                        Concluir(a, a.InicioPausa.Value);
                        return EventoGeofence.Concluiu;
                    }
                    return EventoGeofence.Nenhum;

                default:
                    return EventoGeofence.Nenhum;
            }
        }

        private static EventoGeofence AvaliarAgendado(Agendamento a, bool dentro, bool fora, DateTime agora)
        {
            if (!a.ChegadaEm.HasValue)
            {
                if (!dentro) return EventoGeofence.Nenhum;
                a.ChegadaEm = agora;
                a.UltimoPingEm = agora;
                return EventoGeofence.Chegou;
            }

            if (fora)
            {
                a.ChegadaEm = null;
                a.UltimoPingEm = null;
                return EventoGeofence.ChegadaDescartada;
            }

            if (a.UltimoPingEm.HasValue && agora - a.UltimoPingEm.Value > LacunaDePing)
            {
                a.ChegadaEm = dentro ? agora : null;
                a.UltimoPingEm = dentro ? agora : null;
                return EventoGeofence.Nenhum;
            }

            a.UltimoPingEm = agora;

            if (agora - a.ChegadaEm.Value >= ConfirmacaoChegada)
            {
                a.Status = StatusAgendamento.EmAtendimento;
                a.Inicio = a.ChegadaEm;
                a.IniciadoPorGeofence = true;
                a.UltimoPingEm = null;
                return EventoGeofence.Iniciou;
            }

            return EventoGeofence.Nenhum;
        }

        public static void Concluir(Agendamento a, DateTime fim)
        {
            if (a.Inicio.HasValue && fim < a.Inicio.Value)
                fim = a.Inicio.Value;

            a.Status = StatusAgendamento.Concluido;
            a.Fim = fim;
            a.DataConclusao = fim;
            a.InicioPausa = null;

            if (a.Inicio.HasValue)
            {
                var liquido = (fim - a.Inicio.Value).TotalMinutes - a.PausaAcumuladaMinutos;
                a.DuracaoMinutos = Math.Round(Math.Max(0, liquido), 1);
            }
        }
    }
}
