namespace DayPlannio.App.Models;

public class Financeiro
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public string Tipo { get; set; }

    public string Descricao { get; set; }

    public decimal Valor { get; set; }

    public DateTime Data { get; set; }

    public DateTime CreatedAt { get; set; }
}