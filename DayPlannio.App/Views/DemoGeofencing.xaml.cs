using DayPlannio.App.Helpers;
using DayPlannio.App.Services;

namespace DayPlannio.App.Views;

public partial class DemoGeofencing : ContentPage
{
    private enum Estado { Agendado, EmAtendimento, EmPausa, Concluido }

    private const double ToleranciaSaidaMetros = 15;

    private const int PausaDemoSegundos = 30;

    private Estado _estado = Estado.Agendado;
    private ResultadoEndereco? _cliente;
    private DateTime? _inicio;
    private DateTime? _fim;
    private DateTime? _inicioPausa;
    private TimeSpan _pausaAcumulada = TimeSpan.Zero;
    private double? _ultimaDistancia;
    private bool _ocupado;
    private IDispatcherTimer? _timer;

    public DemoGeofencing()
    {
        InitializeComponent();

        PickerRaio.ItemsSource = RaioGeofence.Opcoes.ToList();
        PickerRaio.SelectedIndex = RaioGeofence.Opcoes.ToList().IndexOf(RaioGeofence.ParaTexto(RaioGeofence.Padrao));

        Reiniciar();
    }

    private int Raio => RaioGeofence.ParaMetros(PickerRaio.SelectedItem as string);

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => AtualizarTempo();
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
        _timer = null;
    }

    private async void OnVoltarClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private void OnReiniciarClicked(object? sender, EventArgs e) => Reiniciar();

    private async void OnDefinirClienteClicked(object? sender, EventArgs e)
    {
        if (SeletorCliente.Latitude is not double latCli || SeletorCliente.Longitude is not double lonCli)
        {
            await DisplayAlertAsync("Demonstração",
                "Escolha o endereço do cliente: busque e selecione o resultado ou toque no mapa.", "OK");
            return;
        }

        var resultado = new ResultadoEndereco
        {
            Descricao = string.IsNullOrWhiteSpace(SeletorCliente.Endereco) ? "local escolhido no mapa" : SeletorCliente.Endereco,
            Latitude = latCli,
            Longitude = lonCli
        };

        Reiniciar(manterCliente: false);

        _cliente = resultado;
        LabelClienteResolvido.Text = $"Localizado: {resultado.Descricao}";
        LabelClienteResolvido.IsVisible = true;

        BotaoMover.IsEnabled = BotaoNoCliente.IsEnabled = BotaoLonge.IsEnabled = true;

        Log($"Cliente fictício definido em: {resultado.Descricao} (raio {Raio} m)");
        AtualizarTela();
    }

    private async void OnMoverClicked(object? sender, EventArgs e)
    {
        if (_cliente == null) return;

        if (SeletorPosicao.Latitude is not double lat || SeletorPosicao.Longitude is not double lon)
        {
            await DisplayAlertAsync("Demonstração",
                "Escolha onde você está: busque e selecione o resultado ou toque no mapa.", "OK");
            return;
        }

        var descricao = string.IsNullOrWhiteSpace(SeletorPosicao.Endereco) ? "local escolhido no mapa" : SeletorPosicao.Endereco;

        var distancia = GeofenceDistancia(lat, lon);
        await AplicarPosicaoAsync(distancia, descricao);
    }

    private async void OnNoClienteClicked(object? sender, EventArgs e) =>
        await AplicarPosicaoAsync(0, "no endereço do cliente");

    private async void OnLongeClicked(object? sender, EventArgs e) =>
        await AplicarPosicaoAsync(1000, "a cerca de 1 km do cliente");

    private async Task AplicarPosicaoAsync(double distancia, string onde)
    {
        if (_cliente == null || _ocupado || _estado == Estado.Concluido) return;

        _ocupado = true;
        try
        {
            _ultimaDistancia = distancia;
            Log($"Posição: {onde} ({Formatar(distancia)} do cliente)");

            switch (_estado)
            {
                case Estado.Agendado:
                    if (distancia <= Raio)
                    {
                        _inicio = DateTime.Now;
                        _estado = Estado.EmAtendimento;

                        Log($"Entrou no raio de {Raio} m. (No uso real, o app espera alguns minutos dentro do raio para confirmar.)");
                        Log("Atendimento iniciado: EM ATENDIMENTO");
                    }
                    else
                    {
                        Log($"Ainda fora do raio de {Raio} m: segue AGENDADO.");
                    }
                    break;

                case Estado.EmAtendimento:
                    if (distancia > Raio + ToleranciaSaidaMetros)
                    {
                        _estado = Estado.EmPausa;
                        _inicioPausa = DateTime.Now;
                        Log($"Saiu do raio (+{ToleranciaSaidaMetros:0} m de tolerância): EM PAUSA");
                        Log($"Se não voltar ao raio em {PausaDemoSegundos} s, o atendimento é concluído. (No uso real, esse tempo é maior.)");
                    }
                    else
                    {
                        Log("Continua dentro do raio: atendimento segue EM ANDAMENTO.");
                    }
                    break;

                case Estado.EmPausa:
                    if (distancia <= Raio)
                    {
                        _pausaAcumulada += DateTime.Now - _inicioPausa!.Value;
                        _inicioPausa = null;
                        _estado = Estado.EmAtendimento;
                        Log("Voltou ao raio: atendimento retomado, EM ATENDIMENTO.");
                    }
                    else
                    {
                        Log("Continua fora do raio: segue EM PAUSA.");
                    }
                    break;
            }

            AtualizarTela();
        }
        finally
        {
            _ocupado = false;
        }
    }

    private double GeofenceDistancia(double latitude, double longitude)
    {
        const double R = 6371000;
        static double Rad(double g) => g * Math.PI / 180.0;

        var dLat = Rad(latitude - _cliente!.Latitude);
        var dLon = Rad(longitude - _cliente.Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(_cliente.Latitude)) * Math.Cos(Rad(latitude)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private void Reiniciar(bool manterCliente = true)
    {
        _estado = Estado.Agendado;
        _inicio = null;
        _fim = null;
        _inicioPausa = null;
        _pausaAcumulada = TimeSpan.Zero;
        _ultimaDistancia = null;

        if (!manterCliente || _cliente == null)
        {
            _cliente = null;
            LabelClienteResolvido.IsVisible = false;
            BotaoMover.IsEnabled = BotaoNoCliente.IsEnabled = BotaoLonge.IsEnabled = false;
        }

        ListaEventos.Children.Clear();
        LabelHorario.Text = $"Hoje, {DateTime.Now:HH:mm} • Maria Souza";

        if (_cliente != null)
            Log("Demonstração reiniciada (mesmo cliente).");

        AtualizarTela();
    }

    private void AtualizarTela()
    {
        var (texto, cor, fundo) = _estado switch
        {
            Estado.EmAtendimento => ("EM ATENDIMENTO", "#B25E09", "#FFE9CC"),
            Estado.EmPausa => ("EM PAUSA", "#6E7978", "#E7E9E8"),
            Estado.Concluido => ("CONCLUÍDO", "#2E7D32", "#DFF3E0"),
            _ => ("AGENDADO", "#006260", "#E0F2F1")
        };

        LabelStatus.Text = texto;
        LabelStatus.TextColor = Color.FromArgb(cor);
        BadgeStatus.BackgroundColor = Color.FromArgb(fundo);

        var destaque = _estado == Estado.EmAtendimento;
        CardAtendimento.BackgroundColor = Color.FromArgb(destaque ? "#FFF6E8" : "#FAFAFA");
        CardAtendimento.Stroke = Color.FromArgb(cor);
        CardAtendimento.StrokeThickness = destaque ? 1.5 : 0;

        LabelDistancia.Text = _cliente == null
            ? "Defina o endereço do cliente para começar."
            : _ultimaDistancia is double d
                ? $"Distância até o cliente: {Formatar(d)} • raio {Raio} m"
                : $"Raio de {Raio} m • ainda sem posição informada";

        AtualizarTempo();
    }

    private void AtualizarTempo()
    {
        if (_estado == Estado.EmPausa && _inicioPausa.HasValue &&
            (DateTime.Now - _inicioPausa.Value).TotalSeconds >= PausaDemoSegundos)
        {
            _fim = _inicioPausa;
            _estado = Estado.Concluido;
            Log($"{PausaDemoSegundos} s fora do raio: concluído automaticamente. (No uso real, esse tempo é maior; também conclui ao chegar a outro cliente.)");
            Log($"Duração do atendimento: {FormatarDuracao(DuracaoLiquida())} (sem contar as pausas)");
            AtualizarTela();
            return;
        }

        LabelTempo.Text = _estado switch
        {
            Estado.EmAtendimento when _inicio.HasValue =>
                $"Início {_inicio:HH:mm:ss} • em andamento há {FormatarDuracao(DateTime.Now - _inicio.Value - _pausaAcumulada)}",
            Estado.EmPausa when _inicio.HasValue && _inicioPausa.HasValue =>
                $"Início {_inicio:HH:mm:ss} • em pausa • conclui em {Math.Max(0, PausaDemoSegundos - (int)(DateTime.Now - _inicioPausa.Value).TotalSeconds)} s se não voltar ao raio",
            Estado.Concluido when _inicio.HasValue && _fim.HasValue =>
                $"Início {_inicio:HH:mm:ss} • fim {_fim:HH:mm:ss} • duração {FormatarDuracao(DuracaoLiquida())}",
            _ => "Aguardando o prestador chegar"
        };
    }

    private TimeSpan DuracaoLiquida()
    {
        if (!_inicio.HasValue || !_fim.HasValue) return TimeSpan.Zero;

        var liquida = _fim.Value - _inicio.Value - _pausaAcumulada;
        return liquida < TimeSpan.Zero ? TimeSpan.Zero : liquida;
    }

    private void Log(string mensagem)
    {
        ListaEventos.Children.Insert(0, new Label
        {
            Text = $"{DateTime.Now:HH:mm:ss}  {mensagem}",
            FontSize = 12,
            TextColor = Color.FromArgb("#3E4948")
        });
    }

    private static string Formatar(double metros) =>
        metros >= 1000 ? $"{metros / 1000:0.0} km" : $"{metros:0} m";

    private static string FormatarDuracao(TimeSpan t) =>
        t.TotalMinutes < 1 ? $"{(int)t.TotalSeconds} s" : $"{(int)t.TotalMinutes} min {t.Seconds} s";
}
