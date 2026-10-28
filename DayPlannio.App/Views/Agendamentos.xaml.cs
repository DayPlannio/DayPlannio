using DayPlannio.App.Services;
using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class Agendamentos : ContentPage, IExibeToast
{
    private readonly AgendamentosViewModel _viewModel;
    private IDispatcherTimer? _pollTimer;
    private IDispatcherTimer? _relogioTimer;

    public Agendamentos()
    {
        InitializeComponent();

        _viewModel = new AgendamentosViewModel(Navigation);

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

#if DEBUG && WINDOWS
        BarraTeste.IsVisible = true;
        BotaoSimular.IsVisible = true;
        BotaoDemo.IsVisible = true;
#endif

        NotificacaoMonitor.Iniciar();
        NotificacaoMonitor.NotificacaoRecebida += OnNotificacaoRecebida;

        await _viewModel.InicializarCommand.ExecuteAsync(null);

        _pollTimer ??= Dispatcher.CreateTimer();
        _pollTimer.Interval = TimeSpan.FromSeconds(10);
        _pollTimer.IsRepeating = true;
        _pollTimer.Tick -= OnPollTick;
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();

        _relogioTimer ??= Dispatcher.CreateTimer();
        _relogioTimer.Interval = TimeSpan.FromSeconds(1);
        _relogioTimer.IsRepeating = true;
        _relogioTimer.Tick -= OnRelogioTick;
        _relogioTimer.Tick += OnRelogioTick;
        _relogioTimer.Start();
    }

    private void OnRelogioTick(object? sender, EventArgs e) => _viewModel.AtualizarRelogios();

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        NotificacaoMonitor.NotificacaoRecebida -= OnNotificacaoRecebida;
        _pollTimer?.Stop();
        _relogioTimer?.Stop();
    }

    private async void OnPollTick(object? sender, EventArgs e)
    {
        AtualizarAvisoLocalizacao();
        await _viewModel.CarregarNotificacoesCommand.ExecuteAsync(null);
        await _viewModel.AtualizarSilenciosoAsync();
    }

    private async void OnNotificacaoRecebida(NotificacaoItem notificacao)
    {
        if (notificacao.Tipo is "plano_aprovado" or "cliente_encerrou_conta")
        {
            await _viewModel.InicializarCommand.ExecuteAsync(null);
            return;
        }

        await _viewModel.CarregarNotificacoesCommand.ExecuteAsync(null);
    }

    public Task MostrarToastAsync(string titulo, string mensagem, bool sucesso)
        => Toast.ShowAsync(titulo, mensagem, sucesso);

    private void AtualizarAvisoLocalizacao()
    {
        var problema = ConfiguracaoLocalizacao.Ativa ? LocalizacaoRastreador.Problema : null;

        BannerLocalizacao.IsVisible = !string.IsNullOrWhiteSpace(problema);
        LabelProblemaLocalizacao.Text = problema ?? string.Empty;

        AtualizarCardGeofence();
    }

    private void AtualizarCardGeofence()
    {
        var resposta = ConfiguracaoLocalizacao.Ativa ? EnviadorLocalizacao.UltimaResposta : null;

        var hoje = DateTime.Today;

        var leituras = resposta?.Leituras?
            .Where(l => l.DistanciaMetros.HasValue && l.RaioMetros.HasValue && EhRelevanteAgora(l, hoje))
            .OrderBy(l => l.DistanciaMetros)
            .Take(2)
            .ToList();

        ListaGeofence.Children.Clear();

        if (leituras == null || leituras.Count == 0)
        {
            CardGeofence.IsVisible = false;
            return;
        }

        foreach (var leitura in leituras)
            ListaGeofence.Children.Add(CriarLinhaGeofence(leitura));

        var origem = SimulacaoLocalizacao.Ativa ? "posição simulada" : "localização atual";
        var hora = EnviadorLocalizacao.UltimoEnvioEm?.ToString("HH:mm:ss");
        LabelOrigemGeofence.Text = hora == null ? origem : $"{origem} • {hora}";

        CardGeofence.IsVisible = true;
    }

    private static bool EhRelevanteAgora(LeituraResumo leitura, DateTime hoje)
    {
        if (leitura.Status is "EmAtendimento" or "EmPausa")
            return true;

        if (leitura.Status != "Agendado" || !leitura.DataHora.HasValue)
            return false;

        var data = leitura.DataHora.Value;
        if (data.Kind == DateTimeKind.Unspecified)
            data = DateTime.SpecifyKind(data, DateTimeKind.Utc);

        return data.ToLocalTime().Date == hoje;
    }

    private static View CriarLinhaGeofence(LeituraResumo leitura)
    {
        var distancia = leitura.DistanciaMetros!.Value;
        var raio = leitura.RaioMetros!.Value;
        var dentro = distancia <= raio;

        var cor = Color.FromArgb(dentro ? "#006260" : "#B25E09");

        var textoDistancia = dentro
            ? "no local"
            : distancia >= 1000 ? $"a {distancia / 1000:0.0} km" : $"a {distancia:0} m";

        var linha = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            RowSpacing = 4
        };

        linha.Add(new Label
        {
            Text = leitura.Cliente,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1A1C1C")
        }, 0, 0);

        linha.Add(new Label
        {
            Text = textoDistancia,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = cor
        }, 1, 0);

        var barra = new ProgressBar
        {
            Progress = dentro ? 1 : Math.Clamp(raio / distancia, 0.03, 0.97),
            ProgressColor = cor,
            BackgroundColor = Color.FromArgb("#E3ECEA")
        };
        linha.Add(barra, 0, 1);
        Grid.SetColumnSpan(barra, 2);

        var raioTexto = new Label
        {
            Text = $"raio de {raio} m",
            FontSize = 10,
            TextColor = Color.FromArgb("#9AA5A4")
        };
        linha.Add(raioTexto, 0, 2);
        Grid.SetColumnSpan(raioTexto, 2);

        return linha;
    }

    private async void OnSimularClicked(object? sender, EventArgs e)
    {
#if DEBUG
        await _viewModel.SimularPosicaoAsync(this);
        AtualizarAvisoLocalizacao();
#else
        await Task.CompletedTask;
#endif
    }

    private async void OnDemoClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new DemoGeofencing());
    }

    private async void OnNotificacoesClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new Notificacoes());
    }
}
