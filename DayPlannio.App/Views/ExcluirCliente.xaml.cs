using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class ExcluirCliente : ContentPage
{
    private readonly ExcluirClienteViewModel _viewModel;

    public bool Confirmado => _viewModel.Confirmado;

    public ExcluirCliente(string nomeCliente)
    {
        InitializeComponent();

        _viewModel = new ExcluirClienteViewModel(
            nomeCliente,
            Navigation);

        BindingContext = _viewModel;
    }
}