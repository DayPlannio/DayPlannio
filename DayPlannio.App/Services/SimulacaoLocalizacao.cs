namespace DayPlannio.App.Services;

public static class SimulacaoLocalizacao
{
    public static bool Ativa { get; private set; }
    public static double Latitude { get; private set; }
    public static double Longitude { get; private set; }
    public static double Precisao => 5;

    public static void DefinirPerto(double latitude, double longitude, double metrosAoNorte)
    {
        Latitude = latitude + metrosAoNorte / 111_320.0;
        Longitude = longitude;
        Ativa = true;
    }

    public static void Desativar() => Ativa = false;
}
