using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class Agendamentos : ContentPage
{
    private readonly AgendamentosViewModel _viewModel;

    public Agendamentos()
    {
        InitializeComponent();

        _viewModel = new AgendamentosViewModel(Navigation);

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.InicializarCommand.ExecuteAsync(null);
    }
}