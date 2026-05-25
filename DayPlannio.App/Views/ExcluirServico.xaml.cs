using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class ExcluirServico : ContentPage
{
    private readonly ExcluirServicoViewModel _viewModel;

    public bool Confirmado => _viewModel.Confirmado;

    public ExcluirServico(string nomeServico)
    {
        InitializeComponent();

        _viewModel = new ExcluirServicoViewModel(
            nomeServico,
            Navigation);

        BindingContext = _viewModel;
    }
}