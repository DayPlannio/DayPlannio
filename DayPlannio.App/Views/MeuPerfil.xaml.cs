using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class MeuPerfil : ContentPage
{
    private readonly MeuPerfilViewModel _viewModel;

    public MeuPerfil()
    {
        InitializeComponent();

        _viewModel = new MeuPerfilViewModel(this);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CarregarPerfil();
    }
}