using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DayPlannio.App.ViewModels;

public partial class ExcluirAgendamentoViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    public ExcluirAgendamentoViewModel(
        string nomeCliente,
        string servico,
        INavigation navigation)
    {
        _navigation = navigation;

        Mensagem = new FormattedString
        {
            Spans =
            {
                new Span { Text = "Tem certeza que deseja excluir o agendamento de " },
                new Span { Text = nomeCliente, FontAttributes = FontAttributes.Bold },
                new Span { Text = " - " },
                new Span { Text = servico, FontAttributes = FontAttributes.Bold },
                new Span { Text = "? Esta ação não pode ser desfeita." }
            }
        };
    }

    [ObservableProperty]
    private bool confirmado;

    [ObservableProperty]
    private FormattedString mensagem;

    [RelayCommand]
    private async Task Excluir()
    {
        Confirmado = true;
        await _navigation.PopModalAsync();
    }

    [RelayCommand]
    private async Task Cancelar()
    {
        Confirmado = false;
        await _navigation.PopModalAsync();
    }
}