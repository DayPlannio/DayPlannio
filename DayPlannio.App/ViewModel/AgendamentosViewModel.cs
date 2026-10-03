using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Models;
using DayPlannio.App.Services;
using DayPlannio.App.Views;
using DayPlannio.App.ViewModel;
using System.Collections.ObjectModel;

namespace DayPlannio.App.ViewModels;

public partial class AgendamentosViewModel : ObservableObject
{
    private readonly INavigation _navigation;
    private readonly string _userId;

    private List<Agendamento> _todosAgendamentos = new();
    private List<Cliente> _clientes = new();
    private List<Servico> _servicos = new();

    public ObservableCollection<DiaViewModel> Dias { get; set; } = new();
    public ObservableCollection<AgendamentoItemViewModel> Agendamentos { get; set; } = new();

    public AgendamentosViewModel(INavigation navigation)
    {
        _navigation = navigation;
        _userId = Preferences.Get("userId", string.Empty);

        DataSelecionada = DateTime.Today;

        CarregarDiasSemana();
    }

    [ObservableProperty] private string mes = string.Empty;
    [ObservableProperty] private string resumo = string.Empty;
    [ObservableProperty] private string tituloLista = "Compromissos de Hoje";
    [ObservableProperty] private string totalAgendados = "0 Agendados";
    [ObservableProperty] private DateTime dataSelecionada;
    [ObservableProperty] private int totalNaoLidas;
    [ObservableProperty] private bool temNotificacoes;

    [RelayCommand]
    public async Task Inicializar()
    {
        await CarregarDados();
        await CarregarNotificacoes();
    }

    [RelayCommand]
    private async Task CarregarNotificacoes()
    {
        var count = await NotificacaoService.ContarNaoLidas(_userId);
        TotalNaoLidas = count;
        TemNotificacoes = count > 0;
    }

    private void CarregarDiasSemana()
    {
        Dias.Clear();

        var hoje = DateTime.Today;

        for (int i = 0; i < 7; i++)
        {
            var dia = hoje.AddDays(i);
            bool selecionado = dia.Date == DataSelecionada.Date;

            Dias.Add(new DiaViewModel
            {
                DiaSemana = dia.ToString("ddd").ToUpper()[..3],
                DiaNumero = dia.Day.ToString(),
                Data = dia,
                CorFundo = selecionado ? Color.FromArgb("#006260") : Colors.White,
                CorTexto = selecionado ? Colors.White : Color.FromArgb("#181C1C")
            });
        }

        Mes = DataSelecionada.ToString("MMMM yyyy");
    }

    private async Task CarregarDados()
    {
        try
        {
            _clientes = await ClienteService.GetClientes(_userId) ?? new();
            _servicos = await ServicoService.GetServicos(_userId) ?? new();
            _todosAgendamentos = await AgendamentoService.GetAgenda(_userId, "proximos") ?? new();

            AtualizarLista();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Erro", ex.Message, "OK");
        }
    }


    private void AtualizarLista()
    {
        Agendamentos.Clear();

        var lista = _todosAgendamentos
            .Where(a => DateTime.SpecifyKind(a.DataHora, DateTimeKind.Utc)
                            .ToLocalTime().Date == DataSelecionada.Date)
            .OrderBy(a => a.DataHora)
            .ToList();

        foreach (var a in lista)
        {
            var dataLocal = DateTime.SpecifyKind(a.DataHora, DateTimeKind.Utc).ToLocalTime(); 

            var cliente = _clientes.FirstOrDefault(c => c.Id == a.ClienteId);
            var servico = _servicos.FirstOrDefault(s => s.Id == a.TipoServicoId);

            Color corStatus;
            Color corFundo;
            string status;

            switch (a.Status?.ToLower())
            {
                case "concluido":
                case "concluído":
                    corStatus = Color.FromArgb("#2E7D32");
                    corFundo = Color.FromArgb("#DFF3E3");
                    status = "CONCLUÍDO";
                    break;
                case "cancelado":
                    corStatus = Color.FromArgb("#BA1A1A");
                    corFundo = Color.FromArgb("#FFDAD6");
                    status = "CANCELADO";
                    break;
                case "ematendimento":
                case "em atendimento":
                    corStatus = Color.FromArgb("#B25E09");
                    corFundo = Color.FromArgb("#FFE9CC");
                    status = "EM ATENDIMENTO";
                    break;
                default:
                    corStatus = Color.FromArgb("#006260");
                    corFundo = Color.FromArgb("#CFEDEC");
                    status = "AGENDADO";
                    break;
            }

            Agendamentos.Add(new AgendamentoItemViewModel
            {
                Id = a.Id,
                Hora = dataLocal.ToString("HH:mm"),     
                Servico = servico?.Tipo ?? "-",
                Cliente = cliente?.Nome ?? (a.ClienteEncerradoEm.HasValue
                    ? $"Cliente encerrado em {DateTime.SpecifyKind(a.ClienteEncerradoEm.Value, DateTimeKind.Utc).ToLocalTime():dd/MM/yyyy}"
                    : "Cliente removido"),
                Endereco = a.EnderecoAtendimento ?? cliente?.Endereco ?? "-",
                Telefone = FormatarTelefone(cliente?.Telefone),
                StatusTexto = status,
                CorStatus = corStatus,
                CorFundoStatus = corFundo,
                ClienteId = a.ClienteId,
                TipoServicoId = a.TipoServicoId,
                DataHora = dataLocal,                   
                ValorCobrado = a.ValorCobrado,
                CustoMaterial = a.CustoMaterial,
                Observacoes = a.Observacoes,
                EnderecoAtendimento = a.EnderecoAtendimento,
                Inicio = a.Inicio,
                Fim = a.Fim,
                DuracaoMinutos = a.DuracaoMinutos,
                PodeCancelar = status == "AGENDADO",
                PodeConcluir = status == "AGENDADO" || status == "EM ATENDIMENTO"
            });
        }

        TituloLista = DataSelecionada.Date == DateTime.Today ? "Compromissos de Hoje"
                    : DataSelecionada.Date == DateTime.Today.AddDays(1) ? "Compromissos de Amanhã"
                    : $"Compromissos de {DataSelecionada:dd/MM}";

        TotalAgendados = $"{Agendamentos.Count} Agendados";
        Resumo = $"Você tem {Agendamentos.Count} compromissos em {DataSelecionada:dd/MM}";
    }

    [RelayCommand]
    private void SelecionarDia(DiaViewModel dia)
    {
        if (dia == null) return;

        DataSelecionada = dia.Data;
        CarregarDiasSemana();
        AtualizarLista();
    }

    [RelayCommand]
    private async Task NovoAgendamento()
        => await _navigation.PushAsync(new CadastrarAgendamento());

    [RelayCommand]
    private async Task AbrirMetricas()
    {
        if (!await PlanoAppService.ExigirPlanoAsync(PlanoAppService.Profissional))
            return;

        await _navigation.PushAsync(new MetricasPage());
    }

    [RelayCommand]
    private async Task Editar(AgendamentoItemViewModel agendamento)
        => await _navigation.PushAsync(new EditarAgendamento(agendamento));

    [RelayCommand]
    private async Task Excluir(AgendamentoItemViewModel agendamento)
    {
        var popup = new ExcluirAgendamento(agendamento.Cliente, agendamento.Servico);

        await _navigation.PushModalAsync(popup);

        while (_navigation.ModalStack.Contains(popup))
            await Task.Delay(100);

        if (!popup.Confirmado) return;

        await AgendamentoService.Delete(agendamento.Id);
        await CarregarDados();
    }

    [RelayCommand]
    private async Task Cancelar(AgendamentoItemViewModel agendamento)
    {
        var popup = new CancelarAgendamento(agendamento.Cliente, agendamento.Servico);

        await _navigation.PushModalAsync(popup);

        while (_navigation.ModalStack.Contains(popup))
            await Task.Delay(100);

        if (!popup.Confirmado) return;

        var (sucessoCancelamento, erroCancelamento) = await AgendamentoService.Cancelar(agendamento.Id, popup.Motivo);
        if (!sucessoCancelamento)
        {
            await Application.Current.MainPage.DisplayAlertAsync("Erro", erroCancelamento, "OK");
            return;
        }

        await CarregarDados();
    }

    [RelayCommand]
    private async Task Concluir(AgendamentoItemViewModel agendamento)
    {
        var userId = Preferences.Get("userId", string.Empty);
        var popup = new ConcluirAgendamento(agendamento.Id, userId, agendamento.Cliente, agendamento.Servico);

        await _navigation.PushModalAsync(popup);

        while (_navigation.ModalStack.Contains(popup))
            await Task.Delay(100);

        if (!popup.ViewModel.Confirmado) return;

        await AgendamentoService.Concluir(agendamento.Id);
        await CarregarDados();
    }

    private static string FormatarTelefone(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return "-";

        var digitos = new string(telefone.Where(char.IsDigit).ToArray());

        return digitos.Length switch
        {
            10 => $"({digitos[..2]}) {digitos[2..6]}-{digitos[6..]}",
            11 => $"({digitos[..2]}) {digitos[2..7]}-{digitos[7..]}",
            _ => telefone
        };
    }
}