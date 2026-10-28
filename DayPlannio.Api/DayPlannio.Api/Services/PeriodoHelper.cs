namespace DayPlannio.Api.Services
{
    public static class PeriodoHelper
    {
        private static readonly TimeZoneInfo Fuso = ObterFuso();

        private static TimeZoneInfo ObterFuso()
        {
            foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }

            return TimeZoneInfo.Local;
        }

        public static DateTime ParaBrasilia(DateTime data)
        {
            var utc = data.Kind == DateTimeKind.Local
                ? data.ToUniversalTime()
                : DateTime.SpecifyKind(data, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(utc, Fuso);
        }

        public static DateTime ParaUtc(DateTime local) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Fuso);

        public static (DateTime Inicio, DateTime Fim, DateTime InicioAnterior) Limites(string? periodo, DateTime agoraUtc)
        {
            var agoraLocal = ParaBrasilia(agoraUtc);
            DateTime inicio, fim, anterior;

            switch (periodo)
            {
                case "diario":
                    inicio = agoraLocal.Date;
                    fim = inicio.AddDays(1);
                    anterior = inicio.AddDays(-1);
                    break;

                case "semanal":
                    inicio = agoraLocal.Date.AddDays(-(int)agoraLocal.DayOfWeek);
                    fim = inicio.AddDays(7);
                    anterior = inicio.AddDays(-7);
                    break;

                default:
                    inicio = new DateTime(agoraLocal.Year, agoraLocal.Month, 1);
                    fim = inicio.AddMonths(1);
                    anterior = inicio.AddMonths(-1);
                    break;
            }

            return (ParaUtc(inicio), ParaUtc(fim), ParaUtc(anterior));
        }

        public static (DateTime Inicio, DateTime Fim) JanelaProximos(DateTime agoraUtc)
        {
            var hoje = ParaBrasilia(agoraUtc).Date;
            return (ParaUtc(hoje.AddDays(-1)), ParaUtc(hoje.AddDays(9)));
        }
    }
}
