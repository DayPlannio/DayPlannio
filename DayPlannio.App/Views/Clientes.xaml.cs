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

        await _viewModel.CarregarClientes();
    }
}