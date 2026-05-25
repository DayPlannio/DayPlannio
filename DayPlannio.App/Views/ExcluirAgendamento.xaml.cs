using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class ExcluirAgendamento : ContentPage
{
    private readonly ExcluirAgendamentoViewModel _viewModel;

    public bool Confirmado => _viewModel.Confirmado;

    public ExcluirAgendamento(string nomeCliente, string servico)
    {
        InitializeComponent();

        _viewModel = new ExcluirAgendamentoViewModel(
            nomeCliente,
            servico,
            Navigation);

        BindingContext = _viewModel;
    }
}