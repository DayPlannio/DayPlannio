using DayPlannio.App.ViewModels;
using System.ComponentModel;

namespace DayPlannio.App.Views;

public partial class CadastrarFinanceiro : ContentPage
{
    public CadastrarFinanceiro()
    {
        InitializeComponent();
        BindingContext = new CadastrarFinanceiroViewModel(this, Navigation);
    }

    private void OnDateSelected(object sender, DateChangedEventArgs e)
    {
        if (BindingContext is CadastrarFinanceiroViewModel vm)
            vm.DataSelecionada = e.NewDate;
    }

    private void OnTimeChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimePicker.Time) && sender is TimePicker tp)
            if (BindingContext is CadastrarFinanceiroViewModel vm)
                vm.HorarioSelecionado = tp.Time;
    }
}