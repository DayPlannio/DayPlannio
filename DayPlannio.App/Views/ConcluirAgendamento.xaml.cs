using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class ConcluirAgendamento : ContentPage
{
    public ConcluirAgendamentoViewModel ViewModel { get; }

    public ConcluirAgendamento(
        string agendamentoId,
        string prestadorId,
        string nomeCliente,
        string servico)
    {
        InitializeComponent();

        ViewModel =
            new ConcluirAgendamentoViewModel(
                Navigation,
                agendamentoId,
                prestadorId,
                nomeCliente,
                servico);

        BindingContext = ViewModel;
    }
}
