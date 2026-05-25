using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DayPlannio.App.ViewModels;

public partial class CancelarAgendamentoViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    public bool Confirmado { get; private set; }

    [ObservableProperty]
    private FormattedString mensagem;

    public CancelarAgendamentoViewModel(
        INavigation navigation,
        string nomeCliente,
        string servico)
    {
        _navigation = navigation;

        Mensagem = new FormattedString
        {
            Spans =
            {
                new Span { Text = "Tem certeza que deseja cancelar o agendamento de " },
                new Span { Text = nomeCliente, FontAttributes = FontAttributes.Bold },
                new Span { Text = " - " },
                new Span { Text = servico, FontAttributes = FontAttributes.Bold },
                new Span { Text = "? Esta ação não pode ser desfeita." }
            }
        };
    }

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