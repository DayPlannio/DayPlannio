namespace DayPlannio.App.ViewModel
{
    public class DiaViewModel
    {
        public string DiaSemana { get; set; } = string.Empty;
        public string DiaNumero { get; set; } = string.Empty;
        public Color CorFundo { get; set; } = Colors.White;
        public Color CorTexto { get; set; } = Colors.Black;
        public DateTime Data { get; set; }
    }
}
