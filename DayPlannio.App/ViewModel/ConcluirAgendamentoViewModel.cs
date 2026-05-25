using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DayPlannio.App.ViewModels;

public partial class ConcluirAgendamentoViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    public bool Confirmado { get; private set; }

    [ObservableProperty]
    private FormattedString mensagem;

    public ConcluirAgendamentoViewModel(
        INavigation navigation,
        string nomeCliente,
        string servico)
    {
        _navigation = navigation;

        Mensagem = new FormattedString
        {
            Spans =
            {
                new Span { Text = "Tem certeza que deseja concluir o agendamento de " },
                new Span { Text = nomeCliente, FontAttributes = FontAttributes.Bold },
                new Span { Text = " - " },
                new Span { Text = servico, FontAttributes = FontAttributes.Bold },
                new Span { Text = "?" }
            }
        };
    }

    [RelayCommand]
    private async Task Concluir()
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