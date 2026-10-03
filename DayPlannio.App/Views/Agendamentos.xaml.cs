using DayPlannio.App.Services;
using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class Agendamentos : ContentPage, IExibeToast
{
    private readonly AgendamentosViewModel _viewModel;
    private IDispatcherTimer? _pollTimer;

    public Agendamentos()
    {
        InitializeComponent();

        _viewModel = new AgendamentosViewModel(Navigation);

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        NotificacaoMonitor.Iniciar();
        NotificacaoMonitor.NotificacaoRecebida += OnNotificacaoRecebida;

        await _viewModel.InicializarCommand.ExecuteAsync(null);

        _pollTimer ??= Dispatcher.CreateTimer();
        _pollTimer.Interval = TimeSpan.FromSeconds(20);
        _pollTimer.IsRepeating = true;
        _pollTimer.Tick -= OnPollTick;
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        NotificacaoMonitor.NotificacaoRecebida -= OnNotificacaoRecebida;
        _pollTimer?.Stop();
    }

    private async void OnPollTick(object? sender, EventArgs e)
    {
        await _viewModel.CarregarNotificacoesCommand.ExecuteAsync(null);
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

    private async void OnNotificacoesClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new Notificacoes());
    }
}
