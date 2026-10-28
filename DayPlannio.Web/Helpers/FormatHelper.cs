using System.Text.RegularExpressions;

namespace DayPlannio.Web;

public static class FormatHelper
{
    public static string FormatarTelefone(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone))
            return "-";

        var digitos = Regex.Replace(telefone, @"\D", "");

        if (digitos.Length == 10)
            return $"({digitos.Substring(0, 2)}) {digitos.Substring(2, 4)}-{digitos.Substring(6, 4)}";

        if (digitos.Length == 11)
            return $"({digitos.Substring(0, 2)}) {digitos.Substring(2, 5)}-{digitos.Substring(7, 4)}";

        return telefone;
    }

    public static string FormatarDataLocal(DateTime data)
    {
        return data.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }

    public static string FormatarDataLocalCurta(DateTime data)
    {
        return data.ToLocalTime().ToString("dd/MM/yyyy");
    }

    private static readonly TimeZoneInfo _fusoBrasilia = ObterFusoBrasilia();

    private static TimeZoneInfo ObterFusoBrasilia()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.Local;
    }

    private static DateTime ParaBrasilia(DateTime data)
    {
        var utc = data.Kind == DateTimeKind.Local
            ? data.ToUniversalTime()
            : DateTime.SpecifyKind(data, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utc, _fusoBrasilia);
    }

    public static string FormatarHora(DateTime? data) =>
        data.HasValue ? ParaBrasilia(data.Value).ToString("HH:mm") : "-";

    public static string FormatarData(DateTime data) =>
        ParaBrasilia(data).ToString("dd/MM/yyyy");

    public static string FormatarDuracao(double? minutos)
    {
        if (!minutos.HasValue) return "-";

        var total = (int)Math.Round(minutos.Value);
        if (total < 1) return "menos de 1 min";
        if (total < 60) return $"{total}min";

        var horas = total / 60;
        var resto = total % 60;
        return resto > 0 ? $"{horas}h {resto}min" : $"{horas}h";
    }

    public static string StatusAmigavel(string? status) => (status ?? string.Empty).ToLowerInvariant() switch
    {
        "agendado" => "Agendado",
        "ematendimento" => "Em atendimento",
        "empausa" => "Em pausa",
        "concluido" => "Concluído",
        "cancelado" => "Cancelado",
        _ => status ?? "-"
    };
}
