namespace DayPlannio.Web.Models
{
    public class HistoricoClienteViewModel
    {
        public List<HistoricoItem> Historico { get; set; } = new();
        public string? ErroMensagem { get; set; }
    }
}
