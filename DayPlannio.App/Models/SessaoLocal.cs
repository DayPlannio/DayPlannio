using SQLite;

namespace DayPlannio.App.Models;

public class SessaoLocal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string UserId { get; set; }
    public DateTime UltimoAcesso { get; set; }
}