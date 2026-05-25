using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;
using DayPlannio.App.ViewModel;
using DayPlannio.App.Views;
using System.Collections.ObjectModel;

namespace DayPlannio.App.ViewModels;

public partial class FinanceiroViewModel : ObservableObject
{
    private readonly INavigation _navigation;
    private readonly string _userId;

    public ObservableCollection<FinanceiroItemViewModel> Registros { get; } = new();

    [ObservableProperty] private string periodoSelecionado = "diario";
    [ObservableProperty] private string saldoServicos = "R$ 0,00";
    [ObservableProperty] private string valorCobrado = "R$ 0,00";
    [ObservableProperty] private string custoServicos = "R$ 0,00";
    [ObservableProperty] private string lucroBruto = "R$ 0,00";
    [ObservableProperty] private string lucroLiquido = "R$ 0,00";
    [ObservableProperty] private string gastosTotais = "R$ 0,00";
    [ObservableProperty] private bool abaMovimentacoesVisivel = true;
    [ObservableProperty] private bool abaResumoVisivel = false;
    [ObservableProperty] private Color corFundoDiario = Color.FromArgb("#FFFFFF");
    [ObservableProperty] private Color corTextoDiario = Color.FromArgb("#006260");
    [ObservableProperty] private Color corFundoSemanal = Colors.Transparent;
    [ObservableProperty] private Color corTextoSemanal = Color.FromArgb("#4F4F4F");
    [ObservableProperty] private Color corFundoMensal = Colors.Transparent;
    [ObservableProperty] private Color corTextoMensal = Color.FromArgb("#4F4F4F");
    [ObservableProperty] private Color corFundoMovimentacoes = Color.FromArgb("#FFFFFF");
    [ObservableProperty] private Color corTextoMovimentacoes = Color.FromArgb("#006260");
    [ObservableProperty] private Color corFundoResumo = Colors.Transparent;
    [ObservableProperty] private Color corTextoResumo = Color.FromArgb("#4F4F4F");

    public FinanceiroViewModel(INavigation navigation)
    {
        _navigation = navigation;
        _userId = Preferences.Get("userId", string.Empty);
    }

    [RelayCommand]
    public async Task Inicializar()
    {
        await CarregarDados();
    }

    [RelayCommand]
    public void SelecionarPeriodo(string periodo)
    {
        PeriodoSelecionado = periodo;

        CorFundoDiario = periodo == "diario" ? Color.FromArgb("#FFFFFF") : Colors.Transparent;
        CorTextoDiario = periodo == "diario" ? Color.FromArgb("#006260") : Color.FromArgb("#4F4F4F");
        CorFundoSemanal = periodo == "semanal" ? Color.FromArgb("#FFFFFF") : Colors.Transparent;
        CorTextoSemanal = periodo == "semanal" ? Color.FromArgb("#006260") : Color.FromArgb("#4F4F4F");
        CorFundoMensal = periodo == "mensal" ? Color.FromArgb("#FFFFFF") : Colors.Transparent;
        CorTextoMensal = periodo == "mensal" ? Color.FromArgb("#006260") : Color.FromArgb("#4F4F4F");

        _ = CarregarDados();
    }

    [RelayCommand]
    public void MostrarMovimentacoes()
    {
        AbaMovimentacoesVisivel = true;
        AbaResumoVisivel = false;

        CorFundoMovimentacoes = Color.FromArgb("#FFFFFF");
        CorTextoMovimentacoes = Color.FromArgb("#006260");
        CorFundoResumo = Colors.Transparent;
        CorTextoResumo = Color.FromArgb("#4F4F4F");
    }

    [RelayCommand]
    public void MostrarResumo()
    {
        AbaMovimentacoesVisivel = false;
        AbaResumoVisivel = true;

        CorFundoMovimentacoes = Colors.Transparent;
        CorTextoMovimentacoes = Color.FromArgb("#4F4F4F");
        CorFundoResumo = Color.FromArgb("#FFFFFF");
        CorTextoResumo = Color.FromArgb("#006260");
    }

    [RelayCommand]
    private async Task Adicionar()
        => await _navigation.PushAsync(new CadastrarFinanceiro());

    [RelayCommand]
    private async Task Editar(FinanceiroItemViewModel item)
    {
        await _navigation.PushAsync(
            new EditarFinanceiro(
                new EditarFinanceiroViewModel(
                    item.Id,
                    item.Tipo,
                    item.Descricao,
                    item.Valor,
                    item.Data)
            ));
    }

    [RelayCommand]
    private async Task Excluir(FinanceiroItemViewModel item)
    {
        await _navigation.PushModalAsync(
            new ExcluirFinanceiro(
                new ExcluirFinanceiroViewModel(_navigation, item)
            )
        );
    }

    [RelayCommand]
    private async Task GerarPdf()
    {
        var bytes = await FinanceiroService.GetRelatorioPdf(_userId, PeriodoSelecionado);
        if (bytes == null)
        {
            await Application.Current.MainPage.DisplayAlertAsync("Erro", "Não foi possível gerar o PDF.", "OK");
            return;
        }

        var path = Path.Combine(FileSystem.CacheDirectory, $"relatorio-{PeriodoSelecionado}.pdf");
        await File.WriteAllBytesAsync(path, bytes);
        await Launcher.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(path)
        });
    }

    private async Task CarregarDados()
    {
        try
        {
            var registros = await FinanceiroService.GetByPeriodo(_userId, PeriodoSelecionado) ?? new();
            var resumo = await FinanceiroService.GetResumoGeral(_userId, PeriodoSelecionado);

            Registros.Clear();
            foreach (var r in registros)
            {
                Registros.Add(new FinanceiroItemViewModel
                {
                    Id = r.Id.ToString(),
                    Tipo = r.Tipo,
                    Descricao = r.Descricao,
                    Valor = r.Valor,
                    Data = DateTime.SpecifyKind(r.Data, DateTimeKind.Utc).ToLocalTime()
                });
            }

            if (resumo != null)
            {
                var saldoServicos = resumo.ReceitaAgendamentos - resumo.CustoAgendamentos;
                SaldoServicos = $"R$ {saldoServicos:N2}";
                ValorCobrado = $"+ R$ {resumo.ReceitaAgendamentos:N2}";
                CustoServicos = $"- R$ {resumo.CustoAgendamentos:N2}";
                LucroBruto = $"R$ {resumo.LucroBruto:N2}";
                LucroLiquido = $"R$ {resumo.LucroLiquido:N2}";
                var gastos = resumo.CustoAgendamentos + resumo.SaidasAvulsas;
                GastosTotais = $"R$ {gastos:N2}";
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlertAsync("Erro", ex.Message, "OK");
        }
    }
}