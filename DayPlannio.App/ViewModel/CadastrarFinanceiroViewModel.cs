using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;

namespace DayPlannio.App.ViewModels;

public partial class CadastrarFinanceiroViewModel : ObservableObject
{
    private readonly Page _page;
    private readonly INavigation _navigation;
    private readonly string _userId;

    public List<string> Tipos { get; } = new()
    {
        "Entrada",
        "Saída"
    };

    public CadastrarFinanceiroViewModel(Page page, INavigation navigation)
    {
        _page = page;
        _navigation = navigation;
        _userId = Preferences.Get("userId", string.Empty);
        TipoSelecionado = "Entrada";
    }

    [ObservableProperty] private string tipoSelecionado = "Entrada";
    [ObservableProperty] private string descricao = string.Empty;
    [ObservableProperty] private DateTime? dataSelecionada;
    [ObservableProperty] private string dataTexto = "Selecionar data";
    [ObservableProperty] private TimeSpan? horarioSelecionado;
    [ObservableProperty] private string horarioTexto = "Selecionar horário";
    [ObservableProperty] private string valor = string.Empty;
    [ObservableProperty] private string erro;
    [ObservableProperty] private bool erroVisivel;

    partial void OnDataSelecionadaChanged(DateTime? value)
    {
        if (value == null)
        {
            DataTexto = "Selecionar data";
            return;
        }

        DataTexto = value.Value.ToString("dd/MM/yyyy");
    }

    partial void OnHorarioSelecionadoChanged(TimeSpan? value)
    {
        if (value == null)
        {
            HorarioTexto = "Selecionar horário";
            return;
        }

        HorarioTexto = $"{value.Value.Hours:D2}:{value.Value.Minutes:D2}";
    }

    [RelayCommand] private async Task Voltar() => await _navigation.PopAsync();
    [RelayCommand] private async Task Cancelar() => await _navigation.PopAsync();

    [RelayCommand]
    private async Task Confirmar()
    {
        try
        {
            ErroVisivel = false;

            if (string.IsNullOrWhiteSpace(Descricao))
                throw new Exception("Informe a descrição.");

            if (string.IsNullOrWhiteSpace(Valor))
                throw new Exception("Informe o valor.");

            if (DataSelecionada == null)
                throw new Exception("Selecione uma data.");

            if (HorarioSelecionado == null)
                throw new Exception("Selecione um horário.");

            if (!decimal.TryParse(
                Valor.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal valorConvertido))
                throw new Exception("Valor inválido.");

            if (!Guid.TryParse(_userId, out Guid usuarioId))
                throw new Exception("Usuário não identificado.");

            var dataHoraLocal = DataSelecionada.Value.Date + HorarioSelecionado.Value;
            var dataHoraUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(dataHoraLocal, DateTimeKind.Unspecified),
                TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time")
            );

            var financeiro = new
            {
                UsuarioId = usuarioId,
                Tipo = TipoSelecionado == "Entrada" ? 0 : 1,
                Descricao,
                Valor = valorConvertido,
                Data = dataHoraUtc
            };

            var (sucesso, erroApi) = await FinanceiroService.Create(financeiro);

            if (sucesso)
            {
                await _page.DisplayAlertAsync("Sucesso", "Registro criado com sucesso.", "OK");
                await _navigation.PopAsync();
            }
            else
            {
                throw new Exception(erroApi);
            }
        }
        catch (Exception ex)
        {
            Erro = ex.Message;
            ErroVisivel = true;
        }
    }
}