using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DayPlannio.App.ViewModels;

public partial class ExcluirServicoViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    public ExcluirServicoViewModel(
        string nomeServico,
        INavigation navigation)
    {
        _navigation = navigation;

        Mensagem = new FormattedString
        {
            Spans =
            {
                new Span { Text = "Tem certeza que quer excluir " },
                new Span { Text = nomeServico, FontAttributes = FontAttributes.Bold },
                new Span { Text = "? Todos os agendamentos vinculados serão afetados. Esta ação não pode ser desfeita." }
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