using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;
using System.Collections.ObjectModel;

namespace DayPlannio.App.ViewModels;

public partial class NotificacoesViewModel : ObservableObject
{
    private readonly string _userId;

    public NotificacoesViewModel()
    {
        _userId = Preferences.Get("userId", string.Empty);
    }

    [ObservableProperty]
    private ObservableCollection<NotificacaoItem> notificacoes = new();

    [ObservableProperty]
    private bool temNotificacoes;

    [ObservableProperty]
    private bool carregando;

    [RelayCommand]
    private async Task CarregarNotificacoes()
    {
        Carregando = true;
        var lista = await NotificacaoService.GetNotificacoes(_userId);
        Notificacoes = new ObservableCollection<NotificacaoItem>(lista ?? new());
        TemNotificacoes = Notificacoes.Count > 0;
        Carregando = false;
    }

    [RelayCommand]
    private async Task MarcarLida(NotificacaoItem item)
    {
        if (item.Lida) return;
        await NotificacaoService.MarcarLida(item.Id);
        item.Lida = true;
    }
}
