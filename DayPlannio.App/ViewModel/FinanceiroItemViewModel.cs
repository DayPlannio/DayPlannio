namespace DayPlannio.App.ViewModel;

public class FinanceiroItemViewModel
{
    public string Id { get; set; }
    public string Tipo { get; set; }
    public string Descricao { get; set; }
    public decimal Valor { get; set; }
    public DateTime Data { get; set; }

    public string ValorFormatado =>
        Tipo?.ToLower() == "entrada"
            ? $"+ R$ {Valor:N2}"
            : $"- R$ {Valor:N2}";

    public Color CorValor =>
        Tipo?.ToLower() == "entrada"
            ? Color.FromArgb("#2E7D32")
            : Color.FromArgb("#D91C1C");

    public string DataHoraFormatada =>
    Data.ToString("dd/MM/yyyy • HH:mm");

    public string HoraFormatada => Data.ToLocalTime().ToString("HH:mm");
}