namespace DayPlannio.App.Services;

public static class ConfiguracaoLocalizacao
{
    private const string Chave = "geofence_ativo";

    public static bool Ativa
    {
        get => Preferences.Get(Chave, true);
        set => Preferences.Set(Chave, value);
    }
}
