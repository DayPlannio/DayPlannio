using DayPlannio.App.Services;
using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class Planos : ContentPage
{
    private readonly PlanosViewModel _viewModel;

    public Planos()
    {
        InitializeComponent();

        _viewModel = new PlanosViewModel(this);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        NotificacaoMonitor.NotificacaoRecebida += OnNotificacaoRecebida;

        await _viewModel.Atualizar();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        NotificacaoMonitor.NotificacaoRecebida -= OnNotificacaoRecebida;
    }

    private async void OnNotificacaoRecebida(NotificacaoItem notificacao)
    {
        if (notificacao.Tipo is "plano_aprovado" or "plano_rejeitado")
            await _viewModel.Atualizar();
    }
}