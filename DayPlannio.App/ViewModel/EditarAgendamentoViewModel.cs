using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Models;
using DayPlannio.App.Services;
using DayPlannio.App.ViewModel;
using System.Collections.ObjectModel;

namespace DayPlannio.App.ViewModels;

public partial class EditarAgendamentoViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    private readonly string _agendamentoId;
    private readonly string _clienteId;
    private readonly string _tipoServicoId;
    private readonly string _userId;

    public ObservableCollection<Cliente> Clientes { get; set; } = new();
    public ObservableCollection<Servico> Servicos { get; set; } = new();

    [ObservableProperty] private Cliente clienteSelecionado;
    [ObservableProperty] private Servico servicoSelecionado;
    [ObservableProperty] private DateTime dataSelecionada;
    [ObservableProperty] private TimeSpan horarioSelecionado;
    [ObservableProperty] private string lblHorario;
    [ObservableProperty] private string valor;
    [ObservableProperty] private string custo;
    [ObservableProperty] private string observacoes;
    [ObservableProperty] private string erroMensagem;
    [ObservableProperty] private bool erroVisivel;
    public DateTime DataMinima { get; } = DateTime.Today;



    public EditarAgendamentoViewModel(
        AgendamentoItemViewModel agendamento,
        INavigation navigation)
    {
        _navigation = navigation;

        _userId = Preferences.Get("userId", string.Empty);
        _agendamentoId = agendamento.Id;
        _clienteId = agendamento.ClienteId;
        _tipoServicoId = agendamento.TipoServicoId;

        DataSelecionada = agendamento.DataHora.Date;

        HorarioSelecionado = agendamento.DataHora.TimeOfDay;
        AtualizarHorarioLabel();

        Valor = agendamento.ValorCobrado.ToString();
        Custo = agendamento.CustoMaterial.ToString();
        Observacoes = agendamento.Observacoes;
    }

    private void AtualizarHorarioLabel()
    {
        LblHorario =
            HorarioSelecionado.Hours.ToString("D2") + ":" +
            HorarioSelecionado.Minutes.ToString("D2");
    }

    partial void OnDataSelecionadaChanged(DateTime value)
    {
        if (value.Date < DateTime.Today)
        {
            DataSelecionada = DateTime.Today;
            return;
        }
    }

    partial void OnHorarioSelecionadoChanged(TimeSpan value)
    {
        AtualizarHorarioLabel();
    }

    [RelayCommand]
    public async Task CarregarDados()
    {
        try
        {
            var clientes = await ClienteService.GetClientes(_userId) ?? new();
            var servicos = await ServicoService.GetServicos(_userId) ?? new();

            Clientes.Clear();
            foreach (var c in clientes)
                Clientes.Add(c);

            Servicos.Clear();
            foreach (var s in servicos)
                Servicos.Add(s);

            ClienteSelecionado =
                Clientes.FirstOrDefault(c => c.Id == _clienteId)
                ?? Clientes.FirstOrDefault();

            ServicoSelecionado =
                Servicos.FirstOrDefault(s => s.Id == _tipoServicoId)
                ?? Servicos.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
    }

    [RelayCommand]
    private async Task Salvar()
    {
        try
        {
            ErroVisivel = false;
            ErroMensagem = string.Empty;

            if (ClienteSelecionado == null)
                throw new Exception("Selecione um cliente.");

            if (ServicoSelecionado == null)
                throw new Exception("Selecione um serviço.");

            var valorStr = Valor?.Replace(",", ".") ?? "0";
            if (!decimal.TryParse(valorStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal valorDecimal))
                throw new Exception("Valor inválido.");

            var custoStr = Custo?.Replace(",", ".") ?? "0";
            if (!decimal.TryParse(custoStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal custoDecimal))
                custoDecimal = 0;

            var agendamento = new
            {
                clienteId = ClienteSelecionado.Id,
                tipoServicoId = ServicoSelecionado.Id,
                dataHora = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(DataSelecionada.Date.Add(HorarioSelecionado), DateTimeKind.Unspecified),
                    TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time")
                ),
                valorCobrado = valorDecimal,
                custoMaterial = custoDecimal,
                observacoes = Observacoes ?? string.Empty
            };

            var (sucesso, erro) = await AgendamentoService.Edit(_agendamentoId, agendamento);

            if (sucesso)
            {
                await Application.Current.MainPage.DisplayAlertAsync("Sucesso", "Atualizado!", "OK");
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
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
    }

    [RelayCommand]
    private async Task Cancelar()
        => await _navigation.PopAsync();

    [RelayCommand]
    private async Task Voltar()
        => await _navigation.PopAsync();
}