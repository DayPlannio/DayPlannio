using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;
using DayPlannio.App.Views;
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

            var lista = new List<object>();

            foreach (var a in historico)
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

                    case "ematendimento":
                    case "em atendimento":
                        corStatus = Color.FromArgb("#B25E09");
                        corFundoStatus = Color.FromArgb("#FFE9CC");
                        statusTexto = "EM ATENDIMENTO";
                        break;

                    case "empausa":
                    case "em pausa":
                        corStatus = Color.FromArgb("#6E7978");
                        corFundoStatus = Color.FromArgb("#E7E9E8");
                        statusTexto = "EM PAUSA";
                        break;

                    default:
                        corStatus = Color.FromArgb("#006260");
                        corFundoStatus = Color.FromArgb("#CFEDEC");
                        statusTexto = "AGENDADO";
                        break;
                }

                List<FotoItem> fotos = new();
                if (statusTexto == "CONCLUÍDO")
                {
                    fotos = await FotoService.GetFotosAgendamento(a.Id) ?? new();
                }

                var inicioReal = a.Inicio.HasValue
                    ? DateTime.SpecifyKind(a.Inicio.Value, DateTimeKind.Utc).ToLocalTime()
                    : (DateTime?)null;

                var fimReal = a.Fim.HasValue
                    ? DateTime.SpecifyKind(a.Fim.Value, DateTimeKind.Utc).ToLocalTime()
                    : (DateTime?)null;

                lista.Add(new
                {
                    Id = a.Id,
                    TipoServico = servico?.Tipo ?? "-",
                    DataHora = DateTime.SpecifyKind(a.DataHora, DateTimeKind.Utc).ToLocalTime(),
                    a.ValorCobrado,
                    a.CustoMaterial,
                    a.Observacoes,
                    TemTempoReal = inicioReal.HasValue,
                    InicioTexto = inicioReal?.ToString("HH:mm") ?? "-",
                    FimTexto = fimReal?.ToString("HH:mm") ?? (inicioReal.HasValue ? "em andamento" : "-"),
                    DuracaoTexto = a.DuracaoMinutos.HasValue ? FormatarDuracao(a.DuracaoMinutos.Value) : "-",
                    StatusTexto = statusTexto,
                    CorStatus = corStatus,
                    CorFundoStatus = corFundoStatus,
                    PodeAdicionarFoto = statusTexto == "CONCLUÍDO",
                    Fotos = new ObservableCollection<FotoItem>(fotos),
                    TemFoto = fotos.Count > 0,
                    QuantidadeFotos = fotos.Count
                });
            }

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

    private static string FormatarDuracao(double minutos)
    {
        var total = (int)Math.Round(minutos);

        if (total < 1) return "menos de 1 min";
        if (total < 60) return $"{total}min";

        var horas = total / 60;
        var resto = total % 60;
        return resto > 0 ? $"{horas}h {resto}min" : $"{horas}h";
    }

    [RelayCommand]
    private async Task AdicionarFoto(object item)
    {
        if (!await PlanoAppService.ExigirPlanoAsync(PlanoAppService.Full))
            return;

        var dict = item.GetType().GetProperties()
            .ToDictionary(p => p.Name, p => p.GetValue(item));

        var agendamentoId = dict.GetValueOrDefault("Id")?.ToString();
        var tipoServico = dict.GetValueOrDefault("TipoServico")?.ToString();
        if (string.IsNullOrEmpty(agendamentoId)) return;

        await _navigation.PushModalAsync(new AdicionarFoto(agendamentoId, tipoServico ?? ""));

        await CarregarHistorico();
    }

    [RelayCommand]
    private async Task ExcluirFoto(object item)
    {
        try
        {
            var dict = item.GetType().GetProperties()
                .ToDictionary(p => p.Name, p => p.GetValue(item));

            var fotoId = dict.GetValueOrDefault("Id")?.ToString();
            if (string.IsNullOrEmpty(fotoId)) return;

            var confirmar = await _page.DisplayAlertAsync(
                "Excluir foto",
                "Tem certeza que deseja excluir esta foto?",
                "Sim", "Não");

            if (!confirmar) return;

            var sucesso = await FotoService.DeleteFoto(fotoId);

            if (sucesso)
            {
                await _page.DisplayAlertAsync("Sucesso", "Foto excluida!", "OK");
                await CarregarHistorico();
            }
            else
            {
                await _page.DisplayAlertAsync("Erro", "Falha ao excluir foto.", "OK");
            }
        }
        catch (Exception ex)
        {
            await _page.DisplayAlertAsync("Erro", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task Voltar()
    {
        await _navigation.PopAsync();
    }
}
