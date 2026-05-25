using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Models;
using DayPlannio.App.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DayPlannio.App.ViewModels;

public partial class CadastrarAgendamentoViewModel : ObservableObject
{
    private readonly INavigation _navigation;
    private readonly string _userId;

    private List<Cliente> _clientesLista = new();
    private List<Servico> _servicosLista = new();

    public CadastrarAgendamentoViewModel(INavigation navigation)
    {
        _navigation = navigation;
        _userId = Preferences.Get("userId", string.Empty);

        DataMinima = DateTime.Today;
    }

    [ObservableProperty]
    private ObservableCollection<string> clientes = new();

    [ObservableProperty]
    private ObservableCollection<string> servicos = new();

    [ObservableProperty]
    private int clienteSelecionado = -1;

    [ObservableProperty]
    private int servicoSelecionado = -1;

    [ObservableProperty]
    private string clienteTexto = "Selecione um cliente";

    [ObservableProperty]
    private string servicoTexto = "Selecione um serviço";

    [ObservableProperty]
    private DateTime? dataAtendimento;

    [ObservableProperty]
    private DateTime dataMinima;

    [ObservableProperty]
    private string dataTexto = "Selecionar data";

    [ObservableProperty]
    private TimeSpan? horario;

    [ObservableProperty]
    private string horarioTexto = "Selecionar horário";

    [ObservableProperty]
    private string valor;

    [ObservableProperty]
    private string custo;

    [ObservableProperty]
    private string observacoes;

    [ObservableProperty]
    private string erroMensagem;

    [ObservableProperty]
    private bool erroVisivel;

    partial void OnClienteSelecionadoChanged(int value)
    {
        if (value >= 0 && value < _clientesLista.Count)
            ClienteTexto = _clientesLista[value].Nome;
    }

    partial void OnServicoSelecionadoChanged(int value)
    {
        if (value >= 0 && value < _servicosLista.Count)
            ServicoTexto = _servicosLista[value].Tipo;
    }

    partial void OnDataAtendimentoChanged(DateTime? value)
    {
        if (value == null)
        {
            DataTexto = "Selecionar data";
            return;
        }

        DataTexto = value.Value.ToString("dd/MM/yyyy");
    }

    partial void OnHorarioChanged(TimeSpan? value)
    {
        if (value == null)
        {
            HorarioTexto = "Selecionar horário";
            return;
        }

        HorarioTexto = $"{value.Value.Hours:D2}:{value.Value.Minutes:D2}";
    }

    public async Task CarregarDados()
    {
        try
        {
            _clientesLista = await ClienteService.GetClientes(_userId) ?? new();

            Clientes = new ObservableCollection<string>(
                _clientesLista.Select(c => c.Nome));

            _servicosLista = await ServicoService.GetServicos(_userId) ?? new();

            Servicos = new ObservableCollection<string>(
                _servicosLista.Select(s => s.Tipo));
        }
        catch (Exception ex)
        {
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
    }

    [RelayCommand]
    private async Task Confirmar()
    {
        ErroVisivel = false;
        ErroMensagem = string.Empty;
        try
        {
            if (ClienteSelecionado < 0)
                throw new Exception("Selecione um cliente.");

            if (ServicoSelecionado < 0)
                throw new Exception("Selecione um serviço.");

            if (DataAtendimento == null)
                throw new Exception("Selecione uma data.");

            if (Horario == null)
                throw new Exception("Selecione um horário.");

            var dataHoraLocal = DataAtendimento.Value.Date + Horario.Value;
            var dataHoraUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(dataHoraLocal, DateTimeKind.Unspecified),
                TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time")
            );

            if (!decimal.TryParse(
                Valor?.Replace(",", "."),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out decimal valorFinal))
                throw new Exception("Valor inválido.");

            if (!decimal.TryParse(
                Custo?.Replace(",", "."),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out decimal custoFinal))
                custoFinal = 0;

            var agendamento = new
            {
                usuarioId = _userId,
                clienteId = _clientesLista[ClienteSelecionado].Id,
                tipoServicoId = _servicosLista[ServicoSelecionado].Id,
                dataHora = dataHoraUtc,
                valorCobrado = valorFinal,
                custoMaterial = custoFinal,
                observacoes = Observacoes
            };

            var (sucesso, erro) = await AgendamentoService.Create(agendamento);

            if (sucesso)
            {
                await Application.Current.MainPage.DisplayAlertAsync("Sucesso", "Agendamento criado!", "OK");
                await _navigation.PopAsync();
            }
            else
            {
                ErroMensagem = erro;
                ErroVisivel = true;
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlertAsync("Erro", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task Voltar()
    {
        await _navigation.PopAsync();
    }

    [RelayCommand]
    private async Task Cancelar()
    {
        await _navigation.PopAsync();
    }
}