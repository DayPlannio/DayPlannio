using System.Globalization;
using DayPlannio.App.Services;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Manipulations;
using Mapsui.Projections;
using Mapsui.Tiling;

namespace DayPlannio.App.Views;

public partial class SeletorLocalizacao : ContentView
{
    public static readonly BindableProperty EnderecoProperty =
        BindableProperty.Create(
            nameof(Endereco),
            typeof(string),
            typeof(SeletorLocalizacao),
            string.Empty,
            BindingMode.TwoWay,
            propertyChanged: (b, _, novo) =>
                ((SeletorLocalizacao)b).AoMudarEndereco(novo as string));

    public static readonly BindableProperty LatitudeProperty =
        BindableProperty.Create(
            nameof(Latitude),
            typeof(double?),
            typeof(SeletorLocalizacao),
            null,
            BindingMode.TwoWay,
            propertyChanged: (b, _, _) =>
                ((SeletorLocalizacao)b).AoMudarCoordenadas());

    public static readonly BindableProperty LongitudeProperty =
        BindableProperty.Create(
            nameof(Longitude),
            typeof(double?),
            typeof(SeletorLocalizacao),
            null,
            BindingMode.TwoWay,
            propertyChanged: (b, _, _) =>
                ((SeletorLocalizacao)b).AoMudarCoordenadas());

    public string Endereco
    {
        get => (string)GetValue(EnderecoProperty);
        set => SetValue(EnderecoProperty, value);
    }

    public double? Latitude
    {
        get => (double?)GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    public double? Longitude
    {
        get => (double?)GetValue(LongitudeProperty);
        set => SetValue(LongitudeProperty, value);
    }

    private const string UserAgent = "DayPlannio/1.0";

    private const int NivelZoomRua = 17;
    private const int NivelZoomCidade = 13;

    private readonly MemoryLayer _camadaMarcador;

    private bool _mapaPronto;
    private bool _definindoCoordenadas;
    private bool _definindoEndereco;
    private CancellationTokenSource? _cts;

    public SeletorLocalizacao()
    {
        InitializeComponent();

        Mapsui.Widgets.InfoWidgets.LoggingWidget.ShowLoggingInMap = Mapsui.Widgets.ActiveMode.No;

        foreach (var widget in mapControl.Map.Widgets)
        {
            if (widget is Mapsui.Widgets.InfoWidgets.LoggingWidget or Mapsui.Widgets.InfoWidgets.PerformanceWidget)
                widget.Enabled = false;
        }

        mapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer(UserAgent));

        _camadaMarcador = new MemoryLayer
        {
            Name = "Marcador",
            Features = Array.Empty<IFeature>(),
            Style = new Mapsui.Styles.SymbolStyle
            {
                SymbolType = Mapsui.Styles.SymbolType.Ellipse,
                Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#D32F2F")),
                Outline = new Mapsui.Styles.Pen(Mapsui.Styles.Color.White, 3),
                SymbolScale = 0.7
            }
        };

        mapControl.Map.Layers.Add(_camadaMarcador);
        mapControl.Map.Tapped += OnMapaTocado;
        mapControl.SizeChanged += (_, _) => AoRedimensionarMapa();

        Loaded += (_, _) => ConfigurarToqueAndroid();
        Unloaded += (_, _) => CancelarOperacao();
    }

    private void AoMudarEndereco(string? novo)
    {
        if (entryEndereco.Text == (novo ?? string.Empty))
            return;

        _definindoEndereco = true;
        entryEndereco.Text = novo ?? string.Empty;
        _definindoEndereco = false;
    }

    private void AoMudarCoordenadas()
    {
        if (_definindoCoordenadas)
            return;

        AtualizarTextosCoordenadas();

        try
        {
            if (Latitude is double lat && Longitude is double lon)
            {
                DefinirMarcador(lat, lon);
                CentralizarEm(lon, lat, ResolucaoDoNivel(NivelZoomRua));
            }
            else if (Latitude == null && Longitude == null)
            {
                LimparMarcador();
            }
        }
        catch
        {
        }
    }

    private void OnEnderecoTextChanged(object? sender, TextChangedEventArgs e)
    {
        var texto = e.NewTextValue ?? string.Empty;

        if (Endereco != texto)
            Endereco = texto;

        if (!_definindoEndereco)
            EsconderResultados();
    }

    private async void OnEnderecoCompleted(object? sender, EventArgs e) =>
        await BuscarAsync();

    private async void OnBuscarClicked(object? sender, EventArgs e) =>
        await BuscarAsync();

    private async Task BuscarAsync()
    {
        var texto = entryEndereco.Text?.Trim();

        if (string.IsNullOrWhiteSpace(texto))
        {
            MostrarStatus("Digite um endereço para buscar.", erro: true);
            return;
        }

        CancelarOperacao();
        var cts = _cts = new CancellationTokenSource();

        btnBuscar.IsEnabled = false;
        EsconderResultados();
        MostrarStatus("Buscando endereço...");

        try
        {
            var resultados = await GeocodificacaoService.Buscar(texto, cts.Token);

            if (cts.IsCancellationRequested)
                return;

            if (resultados.Count == 0)
            {
                MostrarStatus(
                    "Nenhum local encontrado. Tente incluir o número e a cidade.",
                    erro: true);
                return;
            }

            if (resultados.Count == 1)
            {
                SelecionarResultado(resultados[0], texto);
                return;
            }

            MostrarResultados(resultados, texto);
            MostrarStatus("Toque no endereço correto:");
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            MostrarStatus(
                "Não foi possível buscar o endereço. Verifique a internet e tente novamente.",
                erro: true);
        }
        finally
        {
            btnBuscar.IsEnabled = true;
        }
    }

    private void MostrarResultados(List<ResultadoEndereco> resultados, string textoDigitado)
    {
        listaResultados.Children.Clear();

        foreach (var resultado in resultados)
        {
            var item = new Border
            {
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(12, 10),
                Content = new Label
                {
                    Text = resultado.Descricao,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#181C1C")
                }
            };

            var toque = new TapGestureRecognizer();
            toque.Tapped += (_, _) => SelecionarResultado(resultado, textoDigitado);
            item.GestureRecognizers.Add(toque);

            listaResultados.Children.Add(item);
        }

        listaResultados.IsVisible = true;
    }

    private void SelecionarResultado(ResultadoEndereco resultado, string textoDigitado)
    {
        EsconderResultados();

        var digitouNumero = textoDigitado.Any(char.IsDigit);

        if (resultado.TemNumero || !digitouNumero)
            DefinirEndereco(resultado.Descricao);

        DefinirCoordenadas(resultado.Latitude, resultado.Longitude);
        CentralizarEm(resultado.Longitude, resultado.Latitude, ResolucaoDoNivel(NivelZoomRua));

        MostrarStatus("Local encontrado e marcado no mapa.");
    }

    private void OnMapaTocado(object? sender, MapEventArgs e)
    {
        if (e.GestureType != GestureType.SingleTap)
            return;

        e.Handled = true;

        var (lon, lat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);

        lon = ((lon + 180) % 360 + 360) % 360 - 180;

        MainThread.BeginInvokeOnMainThread(async () =>
            await MarcarPontoAsync(lat, lon));
    }

    private async Task MarcarPontoAsync(double lat, double lon)
    {
        CancelarOperacao();
        var cts = _cts = new CancellationTokenSource();

        EsconderResultados();
        DefinirCoordenadas(lat, lon);
        MostrarStatus("Buscando endereço do local...");

        try
        {
            var endereco = await GeocodificacaoService.BuscarEndereco(lat, lon, cts.Token);

            if (cts.IsCancellationRequested)
                return;

            if (endereco != null)
            {
                DefinirEndereco(endereco);
                MostrarStatus("Local marcado e endereço preenchido.");
            }
            else
            {
                MostrarStatus(
                    "Local marcado, mas não foi possível descobrir o endereço. Digite-o manualmente.",
                    erro: true);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            MostrarStatus(
                "Local marcado, mas não foi possível buscar o endereço. Verifique a internet.",
                erro: true);
        }
    }

    private void DefinirEndereco(string texto)
    {
        _definindoEndereco = true;
        entryEndereco.Text = texto;
        Endereco = texto;
        _definindoEndereco = false;
    }

    private void DefinirCoordenadas(double lat, double lon)
    {
        _definindoCoordenadas = true;
        Latitude = lat;
        Longitude = lon;
        _definindoCoordenadas = false;

        AtualizarTextosCoordenadas();
        DefinirMarcador(lat, lon);
    }

    private void AtualizarTextosCoordenadas()
    {
        entryLatitude.Text = Latitude?.ToString("0.0000000", CultureInfo.InvariantCulture) ?? string.Empty;
        entryLongitude.Text = Longitude?.ToString("0.0000000", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private void DefinirMarcador(double lat, double lon)
    {
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);

        _camadaMarcador.Features = new IFeature[] { new PointFeature(x, y) };
        _camadaMarcador.DataHasChanged();
        mapControl.Refresh();
    }

    private void LimparMarcador()
    {
        _camadaMarcador.Features = Array.Empty<IFeature>();
        _camadaMarcador.DataHasChanged();
        mapControl.Refresh();
    }

    private static double ResolucaoDoNivel(int nivel) =>
        156543.03392804097 / Math.Pow(2, nivel);

    private void CentralizarEm(double lon, double lat, double resolucao)
    {
        if (!_mapaPronto)
            return;

        CentralizarAgora(lon, lat, resolucao);
    }

    private void CentralizarAgora(double lon, double lat, double resolucao)
    {
        try
        {
            var (x, y) = SphericalMercator.FromLonLat(lon, lat);
            mapControl.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), resolucao);
            mapControl.Refresh();
        }
        catch
        {
        }
    }

    private void AoRedimensionarMapa()
    {
        if (_mapaPronto || mapControl.Width <= 0 || mapControl.Height <= 0)
            return;

        _mapaPronto = true;

        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), PosicionarMapaInicial);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(900), PosicionarMapaInicial);
    }

    private void PosicionarMapaInicial()
    {
        try
        {
            if (Latitude is double lat && Longitude is double lon)
            {
                DefinirMarcador(lat, lon);
                CentralizarAgora(lon, lat, ResolucaoDoNivel(NivelZoomRua));
            }
            else
            {
                CentralizarAgora(-48.5578, -22.2964, ResolucaoDoNivel(NivelZoomCidade));
            }
        }
        catch
        {
        }
    }

    private void MostrarStatus(string texto, bool erro = false)
    {
        lblStatus.Text = texto;
        lblStatus.TextColor = Color.FromArgb(erro ? "#BA1A1A" : "#3E4948");
        lblStatus.IsVisible = true;
    }

    private void EsconderResultados()
    {
        listaResultados.Children.Clear();
        listaResultados.IsVisible = false;
    }

    private void CancelarOperacao()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private void ConfigurarToqueAndroid()
    {
#if ANDROID
        if (mapControl.Content?.Handler?.PlatformView is Android.Views.View view)
        {
            view.Touch -= OnToqueAndroid;
            view.Touch += OnToqueAndroid;
        }
#endif
    }

#if ANDROID
    private static void OnToqueAndroid(object? sender, Android.Views.View.TouchEventArgs e)
    {
        var acao = e.Event?.ActionMasked;

        if (sender is Android.Views.View view &&
            (acao == Android.Views.MotionEventActions.Down ||
             acao == Android.Views.MotionEventActions.PointerDown ||
             acao == Android.Views.MotionEventActions.Move))
        {
            view.Parent?.RequestDisallowInterceptTouchEvent(true);
        }
    }
#endif
}