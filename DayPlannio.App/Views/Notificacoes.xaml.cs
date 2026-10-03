using DayPlannio.App.Services;
using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class Notificacoes : ContentPage
{
    private NotificacoesViewModel _viewModel;

    public Notificacoes()
    {
        InitializeComponent();
        _viewModel = new NotificacoesViewModel();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        NotificacaoMonitor.NotificacaoRecebida += OnNotificacaoRecebida;
        _ = _viewModel.CarregarNotificacoesCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        NotificacaoMonitor.NotificacaoRecebida -= OnNotificacaoRecebida;
    }

    private async void OnNotificacaoRecebida(NotificacaoItem notificacao)
    {
        await _viewModel.CarregarNotificacoesCommand.ExecuteAsync(null);
    }

    private async void OnVoltarClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
