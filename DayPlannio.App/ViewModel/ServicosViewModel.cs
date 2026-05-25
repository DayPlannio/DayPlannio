using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Models;
using DayPlannio.App.Services;
using DayPlannio.App.Views;
using System.Collections.ObjectModel;

namespace DayPlannio.App.ViewModels;

public partial class ServicosViewModel : ObservableObject
{
    private readonly Page _page;
    private readonly INavigation _navigation;
    private readonly string _userId;

    private List<Servico> _todosServicos = new();

    public ServicosViewModel(Page page, INavigation navigation)
    {
        _page = page;
        _navigation = navigation;
        _userId = Preferences.Get("userId", string.Empty);
    }

    [ObservableProperty]
    private ObservableCollection<Servico> servicos = new();

    [ObservableProperty]
    private string busca;

    partial void OnBuscaChanged(string value)
    {
        var termo = value?.ToLower() ?? "";

        var filtrados = _todosServicos.Where(c =>
            c.Tipo.ToLower().Contains(termo) ||
            (c.Descricao?.ToLower().Contains(termo) ?? false)
        ).ToList();

        Servicos = new ObservableCollection<Servico>(filtrados);
    }

    public async Task CarregarServicos()
    {
        try
        {
            _todosServicos = await ServicoService.GetServicos(_userId) ?? new();

            Servicos = new ObservableCollection<Servico>(_todosServicos);
        }
        catch (Exception ex)
        {
            await _page.DisplayAlertAsync(
                "Erro",
                ex.Message,
                "OK");
        }
    }

    [RelayCommand]
    private async Task NovoServico()
    {
        await _navigation.PushAsync(new Views.CadastrarServico());
    }

    [RelayCommand]
    private async Task Editar(Servico servico)
    {
        await _navigation.PushAsync(
            new Views.EditarServico(servico));
    }

    [RelayCommand]
    private async Task Deletar(Servico servico)
    {
        try
        {
            var popup = new Views.ExcluirServico(servico.Tipo);

            await _navigation.PushModalAsync(popup);

            await Task.Delay(100);

            while (_navigation.ModalStack.Contains(popup))
                await Task.Delay(100);

            if (popup.Confirmado)
            {
                var resultado = await ServicoService.Delete(servico.Id);

                if (resultado.sucesso)
                {
                    await _page.DisplayAlertAsync(
                        "Sucesso",
                        "Serviço deletado com sucesso!",
                        "OK");

                    await CarregarServicos();
                }
                else
                {
                    throw new Exception("Erro ao deletar serviço.");
                }
            }
        }
        catch (Exception ex)
        {
            await _page.DisplayAlertAsync(
                "Erro",
                ex.Message,
                "OK");
        }
    }
}