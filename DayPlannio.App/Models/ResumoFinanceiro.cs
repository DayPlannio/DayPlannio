namespace DayPlannio.App.Models;

public class ResumoFinanceiro
{
    public string Periodo { get; set; }

    public DateTime DataInicio { get; set; }

    public DateTime DataFim { get; set; }

    public decimal ReceitaAgendamentos { get; set; }

    public decimal CustoAgendamentos { get; set; }

    public decimal EntradasAvulsas { get; set; }

    public decimal SaidasAvulsas { get; set; }

    public decimal LucroBruto { get; set; }

    public decimal LucroLiquido { get; set; }

    public int TotalServicos { get; set; }
}