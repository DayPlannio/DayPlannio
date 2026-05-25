using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class CancelarAgendamento : ContentPage
{
    public CancelarAgendamentoViewModel ViewModel =>
        (CancelarAgendamentoViewModel)BindingContext;

    public bool Confirmado => ViewModel.Confirmado;

    public CancelarAgendamento(
        string nomeCliente,
        string servico)
    {
        InitializeComponent();

        BindingContext =
            new CancelarAgendamentoViewModel(
                Navigation,
                nomeCliente,
                servico);
    }
}