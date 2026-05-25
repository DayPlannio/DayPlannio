using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class Financeiro : ContentPage
{
    private readonly FinanceiroViewModel _viewModel;

    public Financeiro()
    {
        InitializeComponent();

        _viewModel = new FinanceiroViewModel(Navigation);

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.InicializarCommand.ExecuteAsync(null);
    }
}