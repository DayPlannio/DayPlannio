using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class HistoricoCliente : ContentPage
{
    private readonly HistoricoClienteViewModel _viewModel;

    public HistoricoCliente(string clienteId, string nomeCliente)
    {
        InitializeComponent();

        _viewModel = new HistoricoClienteViewModel(
            clienteId,
            nomeCliente,
            Navigation,
            this);

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.CarregarHistorico();
    }
}