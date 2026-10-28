using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;

using Location = Android.Locations.Location;

namespace DayPlannio.App
{
    [Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
    public class RastreamentoLocalizacaoService : Service
    {
        private const int NotificacaoId = 4721;
        private const string CanalId = "dayplannio_geofence";

        private static bool _ativo;

        private LocationManager? _locationManager;
        private OuvinteLocalizacao? _ouvinte;
        private int _intervaloSegundos = 30;
        private int _enviando;

        public static void Iniciar()
        {
            if (_ativo) return;

            var contexto = Android.App.Application.Context;
            var intent = new Intent(contexto, typeof(RastreamentoLocalizacaoService));

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                contexto.StartForegroundService(intent);
            else
                contexto.StartService(intent);

            _ativo = true;
        }

        public static void Parar()
        {
            var contexto = Android.App.Application.Context;
            contexto.StopService(new Intent(contexto, typeof(RastreamentoLocalizacaoService)));
            _ativo = false;
        }

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            _ativo = true;

            var notificacao = CriarNotificacao();

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                StartForeground(NotificacaoId, notificacao, ForegroundService.TypeLocation);
            else
                StartForeground(NotificacaoId, notificacao);

            RegistrarAtualizacoes();

            return StartCommandResult.Sticky;
        }

        public override void OnDestroy()
        {
            PararAtualizacoes();
            _ativo = false;
            base.OnDestroy();
        }

        private void RegistrarAtualizacoes()
        {
            PararAtualizacoes();

            _locationManager ??= (LocationManager?)GetSystemService(LocationService);
            if (_locationManager == null) { StopSelf(); return; }

            _ouvinte = new OuvinteLocalizacao(this);

            var tempoMs = _intervaloSegundos * 1000L;

            try
            {
                if (_locationManager.IsProviderEnabled(LocationManager.GpsProvider))
                    _locationManager.RequestLocationUpdates(LocationManager.GpsProvider, tempoMs, 0f, _ouvinte, Looper.MainLooper);
                else if (_locationManager.IsProviderEnabled(LocationManager.NetworkProvider))
                    _locationManager.RequestLocationUpdates(LocationManager.NetworkProvider, tempoMs, 0f, _ouvinte, Looper.MainLooper);
            }
            catch (Java.Lang.SecurityException)
            {
                StopSelf();
            }
        }

        private void PararAtualizacoes()
        {
            if (_locationManager != null && _ouvinte != null)
            {
                try { _locationManager.RemoveUpdates(_ouvinte); } catch { }
            }
            _ouvinte = null;
        }

        internal async void AoReceberLocalizacao(Location local)
        {
            if (Interlocked.Exchange(ref _enviando, 1) == 1) return;

            try
            {
                var resposta = await DayPlannio.App.Services.EnviadorLocalizacao.EnviarAsync(
                    local.Latitude, local.Longitude, local.HasAccuracy ? (double?)local.Accuracy : null);

                if (resposta == null) return;

                if (resposta.Parar)
                {
                    StopSelf();
                    return;
                }

                if (resposta.IntervaloSegundos is int segundos && segundos != _intervaloSegundos)
                {
                    _intervaloSegundos = segundos;
                    RegistrarAtualizacoes();
                }
            }
            catch
            {
            }
            finally
            {
                Interlocked.Exchange(ref _enviando, 0);
            }
        }

#pragma warning disable CA1422
        private Notification CriarNotificacao()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var canal = new NotificationChannel(CanalId, "Acompanhamento de atendimentos", NotificationImportance.Low)
                {
                    Description = "Usado para avisar o cliente que você está chegando e registrar início e fim do atendimento."
                };
                ((NotificationManager?)GetSystemService(NotificationService))?.CreateNotificationChannel(canal);
            }

            var abrirApp = PackageManager?.GetLaunchIntentForPackage(PackageName!);
            var pendente = PendingIntent.GetActivity(this, 0, abrirApp, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
                ? new Notification.Builder(this, CanalId)
                : new Notification.Builder(this);

            return builder
                .SetContentTitle("DayPlannio")
                .SetContentText("Acompanhando seus atendimentos de hoje")
                .SetSmallIcon(ApplicationInfo!.Icon)
                .SetOngoing(true)
                .SetContentIntent(pendente)
                .Build()!;
        }
#pragma warning restore CA1422

        private sealed class OuvinteLocalizacao : Java.Lang.Object, ILocationListener
        {
            private readonly RastreamentoLocalizacaoService _servico;

            public OuvinteLocalizacao(RastreamentoLocalizacaoService servico) => _servico = servico;

            public void OnLocationChanged(Location location) => _servico.AoReceberLocalizacao(location);
            public void OnProviderDisabled(string provider) { }
            public void OnProviderEnabled(string provider) { }
            public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }
        }
    }
}
