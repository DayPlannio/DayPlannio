using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;
using DayPlannio.App.ViewModel;

namespace DayPlannio.App.ViewModels;

public partial class ExcluirFinanceiroViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    public string Id { get; set; }

    [ObservableProperty]
    private string mensagem;

    public ExcluirFinanceiroViewModel(
        INavigation navigation,
        FinanceiroItemViewModel item)
    {
        _navigation = navigation;

        Id = item.Id;

        Mensagem = $"Deseja excluir \"{item.Descricao}\"?";
    }

    [RelayCommand]
    private async Task Excluir()
    {
        await FinanceiroService.Delete(Id);

        await _navigation.PopModalAsync();
    }

    [RelayCommand]
    private async Task Cancelar()
    {
        await _navigation.PopModalAsync();
    }
}