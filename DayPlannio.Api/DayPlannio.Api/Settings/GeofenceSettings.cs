namespace DayPlannio.Api.Settings
{
    public class GeofenceSettings
    {
        public double ToleranciaSaidaMetros { get; set; } = 15;

        public double ConfirmacaoMinutos { get; set; } = 5;

        public double ToleranciaPausaMinutos { get; set; } = 30;

        public double PrecisaoMaximaMetros { get; set; } = 0;

        public double JanelaHoras { get; set; } = 24;
    }
}
