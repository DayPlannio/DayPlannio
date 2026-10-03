using DayPlannio.App.Services;
using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class Clientes : ContentPage
{
    private readonly ClientesViewModel _viewModel;

    public Clientes()
    {
        InitializeComponent();

        _viewModel =
            new ClientesViewModel(
                this,
                Navigation);

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        NotificacaoMonitor.NotificacaoRecebida += OnNotificacaoRecebida;

        await _viewModel.CarregarClientes();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        NotificacaoMonitor.NotificacaoRecebida -= OnNotificacaoRecebida;
    }

    private async void OnNotificacaoRecebida(NotificacaoItem notificacao)
    {
        if (notificacao.Tipo == "cliente_encerrou_conta")
            await _viewModel.CarregarClientes();
    }
}