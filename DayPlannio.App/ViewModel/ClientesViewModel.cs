using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Models;
using DayPlannio.App.Services;
using DayPlannio.App.Views;
using System.Collections.ObjectModel;

namespace DayPlannio.App.ViewModels;

public partial class ClientesViewModel : ObservableObject
{
    private readonly Page _page;
    private readonly INavigation _navigation;
    private readonly string _userId;

    private List<Cliente> _todosClientes = new();

    public ClientesViewModel(
        Page page,
        INavigation navigation)
    {
        _page = page;
        _navigation = navigation;

        _userId =
            Preferences.Get(
                "userId",
                string.Empty);
    }

    [ObservableProperty]
    private ObservableCollection<Cliente> clientes = new();

    [ObservableProperty]
    private string busca;

    partial void OnBuscaChanged(string value)
    {
        var termo = value?.ToLower() ?? "";

        var filtrados = _todosClientes.Where(c =>
            c.Nome.ToLower().Contains(termo) ||
            (c.Telefone?.ToLower().Contains(termo) ?? false)
        ).ToList();

        Clientes =
            new ObservableCollection<Cliente>(
                filtrados);
    }

    public async Task CarregarClientes()
    {
        try
        {
            _todosClientes =
                await ClienteService.GetClientes(_userId)
                ?? new();

            foreach (var cliente in _todosClientes)
            {
                var historico =
                    await AgendamentoService.GetHistorico(cliente.Id)
                    ?? new();

                var ultimaVisita = historico
                    .Where(a =>
                        a.Status?.Trim().ToLower() is "concluido" or "concluído")
                    .OrderByDescending(a => a.DataHora)
                    .FirstOrDefault();

                cliente.UltimaVisitaTexto =
                     ultimaVisita != null
                         ? $"Última Visita: {DateTime.SpecifyKind(ultimaVisita.DataHora, DateTimeKind.Utc).ToLocalTime():dd/MM/yyyy}"
                         : "Nenhuma visita concluída";
            }

            Clientes =
                new ObservableCollection<Cliente>(
                    _todosClientes);
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
    private async Task NovoCliente()
    {
        await _navigation.PushAsync(
            new CadastrarCliente());
    }

    [RelayCommand]
    private async Task Editar(Cliente cliente)
    {
        await _navigation.PushAsync(
            new EditarCliente(
                cliente.Id,
                cliente.Nome,
                cliente.Telefone ?? "",
                cliente.Endereco ?? "",
                cliente.Observacoes ?? ""));
    }

    [RelayCommand]
    private async Task Deletar(Cliente cliente)
    {
        try
        {
            var popup =
                new ExcluirCliente(cliente.Nome);

            await _navigation.PushModalAsync(popup);

            await Task.Delay(100);

            while (_navigation.ModalStack.Contains(popup))
                await Task.Delay(100);

            if (popup.Confirmado)
            {
                var resultado = await ClienteService.Delete(cliente.Id);

                if (resultado.sucesso)
                {
                    await _page.DisplayAlertAsync(
                        "Sucesso",
                        "Cliente deletado com sucesso!",
                        "OK");

                    await CarregarClientes();
                }
                else
                {
                    throw new Exception("Erro ao deletar cliente.");
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

    [RelayCommand]
    private async Task Historico(Cliente cliente)
    {
        await _navigation.PushAsync(
            new HistoricoCliente(
                cliente.Id,
                cliente.Nome));
    }
}