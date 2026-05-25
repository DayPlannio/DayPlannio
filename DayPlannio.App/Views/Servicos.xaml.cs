using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class Servicos : ContentPage
{
    private readonly ServicosViewModel _viewModel;

    public Servicos()
    {
        InitializeComponent();

        _viewModel = new ServicosViewModel(this, Navigation);

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CarregarServicos();
    }
}