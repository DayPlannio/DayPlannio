using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DayPlannio.App.ViewModels;

public partial class CancelarAgendamentoViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    public const string OpcaoOutro = "Outro motivo";

    public IReadOnlyList<string> Motivos { get; } = new List<string>
    {
        "Cliente desmarcou",
        "Cliente não compareceu",
        "Falta de material",
        "Chuva / imprevisto",
        "Erro no agendamento",
        OpcaoOutro
    };

    public bool Confirmado { get; private set; }

    [ObservableProperty]
    private FormattedString mensagem;

    [ObservableProperty]
    private string? motivoSelecionado;

    [ObservableProperty]
    private string motivoOutro = string.Empty;

    public bool MostrarBoxOutro => MotivoSelecionado == OpcaoOutro;

    public bool PodeExcluir => !string.IsNullOrWhiteSpace(MotivoFinal);

    public string MotivoFinal
    {
        get
        {
            if (MotivoSelecionado == OpcaoOutro)
                return MotivoOutro.Trim();

            return string.IsNullOrWhiteSpace(MotivoSelecionado)
                ? string.Empty
                : MotivoSelecionado.Trim();
        }
    }

    public bool TentouExcluir { get; private set; }

    public string AvisoMotivo =>
        MotivoSelecionado == OpcaoOutro
            ? "Escreva o motivo do cancelamento."
            : "Selecione um motivo para o cancelamento.";

    public bool MostrarAvisoMotivo => TentouExcluir && !PodeExcluir;

    public CancelarAgendamentoViewModel(
        INavigation navigation,
        string nomeCliente,
        string servico)
    {
        _navigation = navigation;

        Mensagem = new FormattedString
        {
            Spans =
            {
                new Span { Text = "Tem certeza que deseja cancelar o agendamento de " },
                new Span { Text = nomeCliente, FontAttributes = FontAttributes.Bold },
                new Span { Text = " - " },
                new Span { Text = servico, FontAttributes = FontAttributes.Bold },
                new Span { Text = "⚠️ Selecione o motivo do cancelamento." }
            }
        };
    }

    partial void OnMotivoSelecionadoChanged(string? value)
    {
        ExcluirCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PodeExcluir));
        OnPropertyChanged(nameof(MostrarBoxOutro));
        OnPropertyChanged(nameof(AvisoMotivo));
        OnPropertyChanged(nameof(MostrarAvisoMotivo));
    }

    partial void OnMotivoOutroChanged(string value)
    {
        ExcluirCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PodeExcluir));
        OnPropertyChanged(nameof(MostrarAvisoMotivo));
    }

    [RelayCommand]
    private async Task Excluir()
    {
        TentouExcluir = true;

        if (!PodeExcluir)
        {
            OnPropertyChanged(nameof(MostrarAvisoMotivo));
            return;
        }

        Confirmado = true;

        await _navigation.PopModalAsync();
    }

    [RelayCommand]
    private async Task Cancelar()
    {
        Confirmado = false;

        await _navigation.PopModalAsync();
    }
}