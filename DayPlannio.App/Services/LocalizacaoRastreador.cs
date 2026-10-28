namespace DayPlannio.App.Services;

public static class LocalizacaoRastreador
{
    private const string ChaveExplicado = "geofence_explicado";

    public static string? Problema { get; private set; }

    public static async Task IniciarAsync()
    {
        if (!ConfiguracaoLocalizacao.Ativa)
        {
            Parar();
            Problema = null;
            return;
        }

#if ANDROID
        try
        {
            if (!await GarantirPermissaoAsync())
            {
                Problema = "Permita o acesso à localização nas configurações do aparelho para o atendimento iniciar sozinho.";
                return;
            }

            Problema = null;
            global::DayPlannio.App.RastreamentoLocalizacaoService.Iniciar();
        }
        catch { }
#else
        IniciarLaco();
        await Task.CompletedTask;
#endif
    }

    public static void Parar()
    {
#if ANDROID
        try { global::DayPlannio.App.RastreamentoLocalizacaoService.Parar(); }
        catch { }
#else
        _cts?.Cancel();
        _cts = null;
#endif
    }

    public static async Task<string> EnviarAgoraAsync()
    {
        if (!ConfiguracaoLocalizacao.Ativa)
            return string.Empty;

#if !ANDROID
        await EnviarUmaLeituraAsync();
#else
        await Task.CompletedTask;
#endif
        return EnviadorLocalizacao.UltimoResumo;
    }

#if !ANDROID
    private const int IntervaloSegundos = 30;
    private static CancellationTokenSource? _cts;

    private static void IniciarLaco()
    {
        if (_cts != null) return;

        _cts = new CancellationTokenSource();
        _ = ExecutarLacoAsync(_cts.Token);
    }

    private static async Task ExecutarLacoAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (await EnviarUmaLeituraAsync())
                {
                    _cts = null;
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(IntervaloSegundos), ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task<bool> EnviarUmaLeituraAsync()
    {
        try
        {
            double latitude = 0, longitude = 0;
            double? precisao = null;

            if (!SimulacaoLocalizacao.Ativa)
            {
                var status = await MainThread.InvokeOnMainThreadAsync(
                    () => Permissions.RequestAsync<Permissions.LocationWhenInUse>());

                if (status != PermissionStatus.Granted)
                {
                    Problema = "Permita o acesso à localização do app no Windows (Configurações > Privacidade e segurança > Localização).";
                    return false;
                }

                var local = await MainThread.InvokeOnMainThreadAsync(() =>
                    Geolocation.Default.GetLocationAsync(
                        new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(15))));

                if (local == null)
                {
                    Problema = "Não consegui descobrir sua localização agora. Confira se a localização do Windows está ligada.";
                    return false;
                }

                latitude = local.Latitude;
                longitude = local.Longitude;
                precisao = local.Accuracy;
            }

            Problema = null;

            var resposta = await EnviadorLocalizacao.EnviarAsync(latitude, longitude, precisao);
            return resposta?.Parar == true;
        }
        catch (FeatureNotEnabledException)
        {
            Problema = "A localização do Windows está desligada. Ligue em Configurações > Privacidade e segurança > Localização.";
            return false;
        }
        catch (FeatureNotSupportedException)
        {
            Problema = "Este computador não tem serviço de localização.";
            return false;
        }
        catch (PermissionException)
        {
            Problema = "Permita o acesso à localização do app no Windows (Configurações > Privacidade e segurança > Localização).";
            return false;
        }
        catch (Exception)
        {
            Problema = "Não foi possível obter sua localização agora.";
            return false;
        }
    }
#endif

#if ANDROID
    private static async Task<bool> GarantirPermissaoAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted)
            return true;

        if (status == PermissionStatus.Denied && !Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>()
            && Preferences.Get(ChaveExplicado, false))
            return false;

        if (!Preferences.Get(ChaveExplicado, false))
        {
            var pagina = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (pagina != null)
            {
                await pagina.DisplayAlertAsync(
                    "Localização",
                    "Para avisar seus clientes que você está chegando e registrar sozinho o início e o fim dos atendimentos, " +
                    "o DayPlannio usa a sua localização enquanto você tem atendimentos agendados.",
                    "Entendi");
            }
            Preferences.Set(ChaveExplicado, true);
        }

        status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        return status == PermissionStatus.Granted;
    }
#endif
}
