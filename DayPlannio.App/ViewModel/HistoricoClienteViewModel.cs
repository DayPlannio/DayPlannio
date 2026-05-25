using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;
using System.Collections.ObjectModel;

namespace DayPlannio.App.ViewModels;

public partial class HistoricoClienteViewModel : ObservableObject
{
    private readonly string _clienteId;
    private readonly INavigation _navigation;
    private readonly Page _page;

    public HistoricoClienteViewModel(
        string clienteId,
        string nomeCliente,
        INavigation navigation,
        Page page)
    {
        _clienteId = clienteId;
        _navigation = navigation;
        _page = page;

        NomeCliente = nomeCliente;
    }

    [ObservableProperty]
    private string nomeCliente;

    [ObservableProperty]
    private ObservableCollection<object> historico = new();

    public async Task CarregarHistorico()
    {
        try
        {
            var historico = await AgendamentoService.GetHistorico(_clienteId) ?? new();

            var userId = Preferences.Get("userId", string.Empty);

            var servicos = await ServicoService.GetServicos(userId) ?? new();

            var lista = historico.Select(a =>
            {
                var servico = servicos.FirstOrDefault(s => s.Id == a.TipoServicoId);

                Color corStatus;
                Color corFundoStatus;
                string statusTexto;

                switch (a.Status?.Trim().ToLower())
                {
                    case "concluido":
                    case "concluído":

                        corStatus = Color.FromArgb("#2E7D32");
                        corFundoStatus = Color.FromArgb("#DFF3E3");
                        statusTexto = "CONCLUÍDO";

                        break;

                    case "cancelado":

                        corStatus = Color.FromArgb("#BA1A1A");
                        corFundoStatus = Color.FromArgb("#FFDAD6");
                        statusTexto = "CANCELADO";

                        break;

                    default:

                        corStatus = Color.FromArgb("#006260");
                        corFundoStatus = Color.FromArgb("#CFEDEC");
                        statusTexto = "AGENDADO";

                        break;
                }

                return new
                {
                    TipoServico = servico?.Tipo ?? "-",
                    DataHora = DateTime.SpecifyKind(a.DataHora, DateTimeKind.Utc).ToLocalTime(),
                    a.ValorCobrado,
                    a.CustoMaterial,
                    a.Observacoes,
                    StatusTexto = statusTexto,
                    CorStatus = corStatus,
                    CorFundoStatus = corFundoStatus
                };
            }).ToList();

            Historico = new ObservableCollection<object>(lista);
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
    private async Task Voltar()
    {
        await _navigation.PopAsync();
    }
}