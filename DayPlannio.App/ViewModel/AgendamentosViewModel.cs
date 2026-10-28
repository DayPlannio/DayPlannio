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
            await AtualizarRastreamentoAsync();
        }
        catch (Exception ex)
        {
            await (Application.Current?.Windows.FirstOrDefault()?.Page?.DisplayAlertAsync("Erro", ex.Message, "OK") ?? Task.CompletedTask);
        }
    }


#if DEBUG
    public async Task SimularPosicaoAsync(Page pagina)
    {
        if (!ConfiguracaoLocalizacao.Ativa)
        {
            await AvisarAsync(pagina, "Localização desligada",
                "Ligue a localização automática em Meu Perfil para fazer o teste.", false);
            return;
        }

        var opcao = await pagina.DisplayActionSheetAsync(
            "Teste do geofencing", "Cancelar", "Desativar simulação",
            "Simular posição em um cliente",
            "Usar localização atual");

        switch (opcao)
        {
            case "Simular posição em um cliente":
                await SimularEmClienteAsync(pagina);
                break;

            case "Usar localização atual":
                SimulacaoLocalizacao.Desativar();
                await LocalizacaoRastreador.IniciarAsync();
                await LocalizacaoRastreador.EnviarAgoraAsync();
                await InformarLocalizacaoAtualAsync(pagina);
                await AtualizarSilenciosoAsync();
                break;

            case "Desativar simulação":
                SimulacaoLocalizacao.Desativar();
                await AvisarAsync(pagina, "Simulação desligada",
                    "Voltando a usar a sua localização atual.", true);
                break;
        }
    }

    private static string RotuloStatus(string? status) => status?.ToLower() switch
    {
        "agendado" => "Agendado",
        "ematendimento" => "Em atendimento",
        "empausa" => "Em pausa",
        "concluido" => "Concluído",
        "cancelado" => "Cancelado",
        _ => status ?? "-"
    };

    private async Task SimularEmClienteAsync(Page pagina)
    {
        var candidatos = _todosAgendamentos
            .Where(a => a.Status?.ToLower() is "agendado" or "ematendimento" or "empausa")
            .Select(a => new { Agendamento = a, Cliente = _clientes.FirstOrDefault(c => c.Id == a.ClienteId) })
            .Where(x => x.Cliente != null &&
                        ((x.Agendamento.Latitude ?? x.Cliente.Latitude) != null) &&
                        ((x.Agendamento.Longitude ?? x.Cliente.Longitude) != null))
            .ToList();

        if (candidatos.Count == 0)
        {
            await AvisarAsync(pagina, "Nenhum agendamento",
                "Crie um agendamento com o endereço escolhido no mapa para fazer o teste.", false);
            return;
        }

        var nomes = candidatos
            .Select(x => $"{x.Cliente!.Nome} - {x.Agendamento.DataHora.ToLocalTime():dd/MM HH:mm} ({RotuloStatus(x.Agendamento.Status)})")
            .ToArray();

        var escolhido = await pagina.DisplayActionSheetAsync(
            "Simular posição em qual agendamento?", "Cancelar", null, nomes);

        var indice = escolhido == null ? -1 : Array.IndexOf(nomes, escolhido);
        if (indice < 0) return;

        var alvo = candidatos[indice];
        var cliente = alvo.Cliente!;

        var onde = await pagina.DisplayActionSheetAsync(
            $"Onde está o prestador em relação a {cliente.Nome}?", "Cancelar", null,
            "Dentro do raio (no local)", "Fora do raio (+300 m)");

        if (onde == null || onde == "Cancelar") return;

        var dentro = onde.StartsWith("Dentro");

        SimulacaoLocalizacao.DefinirPerto(
            (alvo.Agendamento.Latitude ?? cliente.Latitude)!.Value,
            (alvo.Agendamento.Longitude ?? cliente.Longitude)!.Value,
            dentro ? 0 : 300);

        await LocalizacaoRastreador.IniciarAsync();
        await LocalizacaoRastreador.EnviarAgoraAsync();
        await AtualizarSilenciosoAsync();

        if (EnviadorLocalizacao.UltimaResposta == null)
        {
            await AvisarAsync(pagina, "Não foi possível enviar",
                "Verifique a internet e se a API está rodando.", false);
            return;
        }

        if (dentro)
            await AvisarAsync(pagina, "Você chegou",
                $"Posição no local de {cliente.Nome}. Se continuar aí, o atendimento começa sozinho.", true);
        else
            await AvisarAsync(pagina, "Você saiu",
                $"Posição fora do local de {cliente.Nome}.", true);
    }

    private async Task InformarLocalizacaoAtualAsync(Page pagina)
    {
        if (LocalizacaoRastreador.Problema is string problema)
        {
            await AvisarAsync(pagina, "Localização indisponível", problema, false);
            return;
        }

        var resposta = EnviadorLocalizacao.UltimaResposta;
        if (resposta == null)
        {
            await AvisarAsync(pagina, "Não foi possível enviar",
                "Verifique a internet e se a API está rodando.", false);
            return;
        }

        var maisPerto = resposta.Leituras?
            .Where(l => l.DistanciaMetros.HasValue)
            .OrderBy(l => l.DistanciaMetros)
            .FirstOrDefault();

        if (maisPerto == null)
        {
            await AvisarAsync(pagina, "Localização enviada",
                "Não há atendimento próximo para acompanhar agora.", true);
            return;
        }

        var distancia = maisPerto.DistanciaMetros!.Value;
        var dentro = maisPerto.RaioMetros.HasValue && distancia <= maisPerto.RaioMetros.Value;

        await AvisarAsync(pagina, "Localização enviada",
            dentro
                ? $"Você está no local de {maisPerto.Cliente}."
                : $"Você está a {FormatarDistancia(distancia)} de {maisPerto.Cliente}.",
            true);
    }

    private static string FormatarDistancia(double metros) =>
        metros >= 1000 ? $"{metros / 1000:0.0} km" : $"{metros:0} m";

    private static async Task AvisarAsync(Page pagina, string titulo, string mensagem, bool sucesso)
    {
        if (pagina is IExibeToast toast)
            await toast.MostrarToastAsync(titulo, mensagem, sucesso);
        else
            await pagina.DisplayAlertAsync(titulo, mensagem, "OK");
    }
#endif

    public void AtualizarRelogios()
    {
        foreach (var item in Agendamentos)
        {
            if (item.PrecisaRelogio)
                item.AtualizarRelogio();
        }
    }

    public async Task AtualizarSilenciosoAsync()
    {
        try
        {
            var agenda = await AgendamentoService.GetAgenda(_userId, "proximos");
            if (agenda == null) return;

            _todosAgendamentos = agenda;
            AtualizarLista();
            await AtualizarRastreamentoAsync();
        }
        catch { }
    }

    private async Task AtualizarRastreamentoAsync()
    {
        var agora = DateTime.UtcNow;

        bool PrecisaRastrear(Agendamento a)
        {
            var s = a.Status?.ToLower();
            var cliente = _clientes.FirstOrDefault(c => c.Id == a.ClienteId);
            if ((a.Latitude ?? cliente?.Latitude) == null || (a.Longitude ?? cliente?.Longitude) == null) return false;

            if (s is "ematendimento" or "empausa") return true;

            var dataUtc = DateTime.SpecifyKind(a.DataHora, DateTimeKind.Utc);
            return s == "agendado"
                && dataUtc >= agora.AddHours(-24)
                && dataUtc <= agora.AddHours(24);
        }

        if (_todosAgendamentos.Any(PrecisaRastrear))
            await LocalizacaoRastreador.IniciarAsync();
        else
            LocalizacaoRastreador.Parar();
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
                case "empausa":
                case "em pausa":
                    corStatus = Color.FromArgb("#6E7978");
                    corFundo = Color.FromArgb("#E7E9E8");
                    status = "EM PAUSA";
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
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                Inicio = a.Inicio,
                Fim = a.Fim,
                InicioPausa = a.InicioPausa,
                ConfirmacaoChegadaEm = a.ConfirmacaoChegadaEm,
                PausaConcluiEm = a.PausaConcluiEm,
                PausaAcumuladaMinutos = a.PausaAcumuladaMinutos,
                DuracaoMinutos = a.DuracaoMinutos,
                PodeCancelar = status == "AGENDADO",
                PodeConcluir = status == "AGENDADO" || status == "EM ATENDIMENTO" || status == "EM PAUSA",
                TrabalhadoSegundos = a.TrabalhadoSegundos,
                NoLocalSegundos = a.NoLocalSegundos,
                ConfirmaEmSegundos = a.ConfirmaEmSegundos,
                DesdeRecebido = a.DesdeRecebido
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