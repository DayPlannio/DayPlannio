using DayPlannio.App.ViewModel;
using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class EditarAgendamento : ContentPage
{
    private readonly EditarAgendamentoViewModel _viewModel;

    public EditarAgendamento(AgendamentoItemViewModel agendamento)
    {
        InitializeComponent();

        _viewModel = new EditarAgendamentoViewModel(agendamento, Navigation);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CarregarDadosCommand.ExecuteAsync(null);
    }
}