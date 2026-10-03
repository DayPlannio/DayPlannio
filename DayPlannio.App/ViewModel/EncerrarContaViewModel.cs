using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DayPlannio.App.ViewModels;

public partial class EncerrarContaViewModel : ObservableObject
{
    private readonly INavigation _navigation;
    private readonly Action<bool> _aoDecidir;
    private readonly bool _somenteConfirmacao;

    public EncerrarContaViewModel(
        INavigation navigation,
        Action<bool> aoDecidir,
        bool somenteConfirmacao = false)
    {
        _navigation = navigation;
        _aoDecidir = aoDecidir;
        _somenteConfirmacao = somenteConfirmacao;

        Mensagem = _somenteConfirmacao
            ? new FormattedString
            {
                Spans =
                {
                    new Span { Text = "Tem certeza que deseja encerrar sua conta?" },
                    new Span { Text = Environment.NewLine + Environment.NewLine },
                    new Span { Text = "Todos os seus dados (prestador, clientes, serviços, agendamentos, financeiro e notificações) serão removidos permanentemente. Esta ação não pode ser desfeita." }
                }
            }
            : new FormattedString
            {
                Spans =
                {
                    new Span { Text = "Tem certeza que deseja encerrar sua conta?" },
                    new Span { Text = Environment.NewLine + Environment.NewLine },
                    new Span { Text = "Todos os seus dados serão removidos permanentemente. Esta ação não pode ser desfeita." }
                }
            };
    }

    [ObservableProperty]
    private FormattedString mensagem;

    [RelayCommand]
    private async Task Confirmar()
    {
        _aoDecidir?.Invoke(true);
        await _navigation.PopModalAsync();
    }

    [RelayCommand]
    private async Task Cancelar()
    {
        _aoDecidir?.Invoke(false);
        await _navigation.PopModalAsync();
    }
}
