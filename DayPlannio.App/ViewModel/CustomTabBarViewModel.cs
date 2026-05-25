using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Views;
using Microsoft.Maui.Controls.Shapes;

namespace DayPlannio.App.ViewModels;

public partial class CustomTabBarViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    public static string AbaAtual = "";

    [ObservableProperty]
    private Color agendaBackground = Colors.Transparent;

    [ObservableProperty]
    private Color agendaTextColor = Color.FromArgb("#6E7978");

    [ObservableProperty]
    private Color servicosBackground = Colors.Transparent;

    [ObservableProperty]
    private Color servicosTextColor = Color.FromArgb("#6E7978");

    [ObservableProperty]
    private Color clientesBackground = Colors.Transparent;

    [ObservableProperty]
    private Color clientesTextColor = Color.FromArgb("#6E7978");

    [ObservableProperty]
    private Color financeiroBackground = Colors.Transparent;

    [ObservableProperty]
    private Color financeiroTextColor = Color.FromArgb("#6E7978");

    [ObservableProperty]
    private Color perfilBackground = Colors.Transparent;

    [ObservableProperty]
    private Color perfilTextColor = Color.FromArgb("#6E7978");

    public CustomTabBarViewModel(INavigation navigation)
    {
        _navigation = navigation;

        AtualizarUI();
    }

    private void AtualizarUI()
    {
        SetTab(
            "agenda",
            ref agendaBackground,
            ref agendaTextColor);

        SetTab(
            "servicos",
            ref servicosBackground,
            ref servicosTextColor);

        SetTab(
            "clientes",
            ref clientesBackground,
            ref clientesTextColor);

        SetTab(
            "financeiro",
            ref financeiroBackground,
            ref financeiroTextColor);

        SetTab(
            "perfil",
            ref perfilBackground,
            ref perfilTextColor);

        OnPropertyChanged(nameof(AgendaBackground));
        OnPropertyChanged(nameof(AgendaTextColor));

        OnPropertyChanged(nameof(ServicosBackground));
        OnPropertyChanged(nameof(ServicosTextColor));

        OnPropertyChanged(nameof(ClientesBackground));
        OnPropertyChanged(nameof(ClientesTextColor));

        OnPropertyChanged(nameof(FinanceiroBackground));
        OnPropertyChanged(nameof(FinanceiroTextColor));

        OnPropertyChanged(nameof(PerfilBackground));
        OnPropertyChanged(nameof(PerfilTextColor));
    }

    private void SetTab(
        string aba,
        ref Color background,
        ref Color textColor)
    {
        bool ativo = AbaAtual == aba;

        background = ativo
            ? Color.FromArgb("#81D5D2")
            : Colors.Transparent;

        textColor = ativo
            ? Color.FromArgb("#00504E")
            : Color.FromArgb("#6E7978");
    }

    [RelayCommand]
    private async Task Agenda()
    {
        AbaAtual = "agenda";

        AtualizarUI();

        await _navigation.PushAsync(
            new Agendamentos());
    }

    [RelayCommand]
    private async Task Servicos()
    {
        AbaAtual = "servicos";

        AtualizarUI();

        await _navigation.PushAsync(
            new Servicos());
    }

    [RelayCommand]
    private async Task Clientes()
    {
        AbaAtual = "clientes";

        AtualizarUI();

        await _navigation.PushAsync(
            new Clientes());
    }

    [RelayCommand]
    private async Task Financeiro()
    {
        AbaAtual = "financeiro";

        AtualizarUI();

        await _navigation.PushAsync(
            new Financeiro());
    }

    [RelayCommand]
    private async Task Perfil()
    {
        AbaAtual = "perfil";

        AtualizarUI();

        await _navigation.PushAsync(
            new MeuPerfil());
    }
}