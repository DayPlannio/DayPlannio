using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;
using System.Collections.ObjectModel;

public partial class EditarFinanceiroViewModel : ObservableObject
{
    private readonly string _financeiroId;

    public ObservableCollection<string> Tipos { get; set; } = new()
    {
        "Entrada",
        "Saída"
    };

    public EditarFinanceiroViewModel(
        string financeiroId,
        string tipo,
        string descricao,
        decimal valor,
        DateTime data)
    {
        _financeiroId = financeiroId;

        Tipo = tipo;
        Descricao = descricao;
        Valor = valor;
        DataSelecionada = data.Date;       
        HorarioSelecionado = data.TimeOfDay;
        AtualizarHorarioLabel();
    }

    [ObservableProperty] private string tipo;
    [ObservableProperty] private string descricao;
    [ObservableProperty] private decimal valor;
    [ObservableProperty] private DateTime dataSelecionada;   
    [ObservableProperty] private TimeSpan horarioSelecionado; 
    [ObservableProperty] private string lblHorario;           
    [ObservableProperty] private string erroMensagem;
    [ObservableProperty] private bool erroVisivel;

    private void AtualizarHorarioLabel()
    {
        LblHorario =
            HorarioSelecionado.Hours.ToString("D2") + ":" +
            HorarioSelecionado.Minutes.ToString("D2");
    }

    partial void OnHorarioSelecionadoChanged(TimeSpan value)
    {
        AtualizarHorarioLabel();
    }

    [RelayCommand]
    private async Task Salvar()
    {
        try
        {
            ErroVisivel = false;

            if (string.IsNullOrWhiteSpace(Descricao))
                throw new Exception("A descrição é obrigatória.");

            if (Valor <= 0)
                throw new Exception("Informe um valor válido.");

            var dataHoraLocal = DataSelecionada.Date + HorarioSelecionado;
            var dataHoraUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(dataHoraLocal, DateTimeKind.Unspecified),
                TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time")
            );

            var financeiro = new
            {
                Tipo = Tipo == "Entrada" ? 0 : 1,
                Descricao,
                Valor,
                Data = dataHoraUtc 
            };

            var (sucesso, erro) = await FinanceiroService.Edit(_financeiroId, financeiro);

            if (sucesso)
            {
                await Application.Current.Windows[0].Page.DisplayAlertAsync(
                    "Sucesso", "Registro atualizado com sucesso!", "OK");

                await Application.Current.Windows[0].Page.Navigation.PopAsync();
            }
            else
            {
                throw new Exception(erro);
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
        => await Application.Current.Windows[0].Page.Navigation.PopAsync();

    [RelayCommand]
    private async Task Voltar()
        => await Application.Current.Windows[0].Page.Navigation.PopAsync();
}