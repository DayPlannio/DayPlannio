using DayPlannio.App.ViewModels;
using System.ComponentModel;

namespace DayPlannio.App.Views;

public partial class CadastrarAgendamento : ContentPage
{
    private readonly CadastrarAgendamentoViewModel _viewModel;

    public CadastrarAgendamento()
    {
        InitializeComponent();
        _viewModel = new CadastrarAgendamentoViewModel(Navigation);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CarregarDados();
    }

    private void OnDateSelected(object sender, DateChangedEventArgs e)
    {
        if (BindingContext is CadastrarAgendamentoViewModel vm)
            vm.DataAtendimento = e.NewDate;
    }

    private void OnTimeChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimePicker.Time) && sender is TimePicker tp)
            if (BindingContext is CadastrarAgendamentoViewModel vm)
                vm.Horario = tp.Time;
    }
}