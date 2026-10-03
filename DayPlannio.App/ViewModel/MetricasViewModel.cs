using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Models;
using DayPlannio.App.Services;
using System.Collections.ObjectModel;

namespace DayPlannio.App.ViewModels
{
    public partial class MetricasViewModel : ObservableObject
    {
        private readonly INavigation _navigation;
        private readonly string _userId;

        private static readonly string[] ChavesPeriodo = { "mensal", "semanal", "diario" };
        private static readonly string[] Rotulos = { "vs mês anterior", "vs semana anterior", "vs ontem" };

        private static readonly Color[] Paleta =
        {
            Color.FromArgb("#006260"),
            Color.FromArgb("#1E7C7A"),
            Color.FromArgb("#4DB6AC"),
            Color.FromArgb("#80CBC4"),
            Color.FromArgb("#FF8A65"),
            Color.FromArgb("#FFB74D"),
            Color.FromArgb("#9575CD")
        };

        [ObservableProperty]
        private ObservableCollection<string> periodos = new()
        {
            "Este mês",
            "Esta semana",
            "Hoje"
        };

        [ObservableProperty]
        private int periodoIndice;

        [ObservableProperty]
        private string atendimentos = "0";

        [ObservableProperty]
        private string clientes = "0";

        [ObservableProperty]
        private string tempoTrabalhado = "0h";

        [ObservableProperty]
        private string faturamento = "R$ 0,00";

        [ObservableProperty]
        private string variacaoAtendimentos = string.Empty;

        [ObservableProperty]
        private string variacaoClientes = string.Empty;

        [ObservableProperty]
        private string variacaoTempo = string.Empty;

        [ObservableProperty]
        private string variacaoFaturamento = string.Empty;

        [ObservableProperty]
        private Color corVariacaoAtendimentos = Colors.Gray;

        [ObservableProperty]
        private Color corVariacaoClientes = Colors.Gray;

        [ObservableProperty]
        private Color corVariacaoTempo = Colors.Gray;

        [ObservableProperty]
        private Color corVariacaoFaturamento = Colors.Gray;

        [ObservableProperty]
        private ObservableCollection<ServicoMetricaItem> porServico = new();

        [ObservableProperty]
        private ObservableCollection<EvolucaoDiaItem> evolucao = new();

        [ObservableProperty]
        private ObservableCollection<MaiorAtendimentoItem> maioresAtendimentos = new();

        [ObservableProperty]
        private string resumoMaior = "-";

        [ObservableProperty]
        private string resumoMenor = "-";

        [ObservableProperty]
        private string resumoMedio = "-";

        [ObservableProperty]
        private string resumoTotal = "-";

    [ObservableProperty]
    private string cancelamentos = "0";

    [ObservableProperty]
    private string taxaCancelamento = "0%";

    [ObservableProperty]
    private string totalFinalizados = "0";

    [ObservableProperty]
    private string principalMotivoCancelamento = "—";

    [ObservableProperty]
    private bool semCancelamentos;

    [ObservableProperty]
    private bool semMotivos;

    [ObservableProperty]
    private ObservableCollection<MotivoCancelamentoMetrica> motivosCancelamento = new();

        [ObservableProperty]
        private bool temDados;

        [ObservableProperty]
        private bool carregando;

        public event Action? DadosAtualizados;

        public MetricasViewModel(INavigation navigation)
        {
            _navigation = navigation;
            _userId = Preferences.Get("userId", string.Empty);
        }

        partial void OnPeriodoIndiceChanged(int value)
        {
            if (value >= 0 && value < ChavesPeriodo.Length)
                _ = CarregarAsync(ChavesPeriodo[value]);
        }

        [RelayCommand]
        private void SelecionarPeriodo(object parameter)
        {
            if (parameter is int i)
            {
                PeriodoIndice = i;
            }
            else if (parameter is string s && int.TryParse(s, out var v))
            {
                PeriodoIndice = v;
            }
        }

        [RelayCommand]
        public async Task Inicializar()
        {
            await CarregarAsync("mensal");
        }

        private async Task CarregarAsync(string periodo)
        {
            try
            {
                Carregando = true;

                var metricas = await MetricasService.GetMetricas(_userId, periodo);

                if (metricas == null)
                {
                    TemDados = false;
                    return;
                }

                Atendimentos = metricas.Atendimentos.ToString();
                Clientes = metricas.Clientes.ToString();

                var horas = metricas.HorasTrabalhadas;
                TempoTrabalhado = horas >= 1
                    ? $"{Math.Round(horas, horas < 10 ? 1 : 0):0.#}h"
                    : $"{(int)Math.Round(metricas.HorasTrabalhadas * 60)}min";

                Faturamento = $"R$ {metricas.Faturamento:N2}";

                var indicePeriodo = Array.IndexOf(ChavesPeriodo, periodo);
                var rotulo = indicePeriodo >= 0 ? Rotulos[indicePeriodo] : Rotulos[0];

                VariacaoAtendimentos = FormatrarVariacao(metricas.Variacoes.Atendimentos, rotulo);
                VariacaoClientes = FormatrarVariacao(metricas.Variacoes.Clientes, rotulo);
                VariacaoTempo = FormatrarVariacao(metricas.Variacoes.HorasTrabalhadas, rotulo);
                VariacaoFaturamento = FormatrarVariacao(metricas.Variacoes.Faturamento, rotulo);

                CorVariacaoAtendimentos = CorVariacao(metricas.Variacoes.Atendimentos);
                CorVariacaoClientes = CorVariacao(metricas.Variacoes.Clientes);
                CorVariacaoTempo = CorVariacao(metricas.Variacoes.HorasTrabalhadas);
                CorVariacaoFaturamento = CorVariacao(metricas.Variacoes.Faturamento);

                PorServico.Clear();
                int corIndice = 0;
                foreach (var item in metricas.PorServico)
                {
                    var cor = Paleta[corIndice % Paleta.Length];
                    corIndice++;

                    PorServico.Add(new ServicoMetricaItem
                    {
                        Servico = item.Servico,
                        Quantidade = item.Quantidade,
                        Percentual = item.Percentual,
                        Progresso = Math.Clamp(item.Percentual / 100.0, 0, 1),
                        Cor = cor,
                        FaturamentoTexto = $"R$ {item.Faturamento:N2}",
                        TempoMedioTexto = item.TempoMedioMinutos.HasValue ? FormatarDuracao(item.TempoMedioMinutos.Value) : "-"
                    });
                }

                Evolucao.Clear();
                foreach (var dia in metricas.Evolucao)
                    Evolucao.Add(new EvolucaoDiaItem { Data = dia.Data, Quantidade = dia.Quantidade });

                MaioresAtendimentos.Clear();
                foreach (var m in metricas.MaioresAtendimentos)
                    MaioresAtendimentos.Add(new MaiorAtendimentoItem
                    {
                        Servico = m.Servico,
                        DuracaoTexto = FormatarDuracao(m.DuracaoMinutos)
                    });

                ResumoMaior = metricas.ResumoDeTempo.Maior.HasValue ? FormatarDuracao(metricas.ResumoDeTempo.Maior.Value) : "-";
                ResumoMenor = metricas.ResumoDeTempo.Menor.HasValue ? FormatarDuracao(metricas.ResumoDeTempo.Menor.Value) : "-";
                ResumoMedio = metricas.ResumoDeTempo.Medio.HasValue ? FormatarDuracao(metricas.ResumoDeTempo.Medio.Value) : "-";
                ResumoTotal = $"{Math.Round(metricas.ResumoDeTempo.TotalMinutos / 60.0, 1):0.#}h";

                Cancelamentos = metricas.Cancelamentos.ToString();
                TotalFinalizados = metricas.TotalFinalizados.ToString();
                TaxaCancelamento = $"{metricas.TaxaCancelamento:0.#}%";
                SemCancelamentos = metricas.Cancelamentos == 0;
                SemMotivos = metricas.MotivosCancelamento.Count == 0;

                PrincipalMotivoCancelamento = metricas.PrincipalMotivoCancelamento != null
                    ? metricas.PrincipalMotivoCancelamento.Palavra
                    : "—";

                MotivosCancelamento.Clear();
                foreach (var motivo in metricas.MotivosCancelamento)
                    MotivosCancelamento.Add(motivo);

                TemDados = metricas.Atendimentos > 0 || metricas.PorServico.Count > 0 || metricas.Cancelamentos > 0;

                if (!TemDados)
                {
                    Atendimentos = "—";
                    Clientes = "—";
                    TempoTrabalhado = "—";
                    Faturamento = "—";
                    ResumoMaior = "—";
                    ResumoMenor = "—";
                    ResumoMedio = "—";
                    ResumoTotal = "—";
                    Cancelamentos = "—";
                    TotalFinalizados = "—";
                    TaxaCancelamento = "—";
                    PrincipalMotivoCancelamento = "—";
                    SemCancelamentos = false;
                    SemMotivos = false;
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlertAsync("Erro", ex.Message, "OK");
            }
            finally
            {
                Carregando = false;
                DadosAtualizados?.Invoke();
            }
        }

        private static string FormatrarVariacao(double valor, string rotulo)
        {
            if (valor == 0)
                return $"= {rotulo}";

            var sinal = valor > 0 ? "+" : "";
            return $"{sinal}{valor:0.#}% {rotulo}";
        }

        private static Color CorVariacao(double valor)
        {
            if (valor > 0)
                return Color.FromArgb("#2E7D32");

            if (valor < 0)
                return Color.FromArgb("#BA1A1A");

            return Color.FromArgb("#6E7978");
        }

        private static string FormatarDuracao(double minutos)
        {
            var total = (int)Math.Round(minutos);

            if (total < 60)
                return $"{total}min";

            var horas = total / 60;
            var restantes = total % 60;

            return restantes > 0 ? $"{horas}h{restantes:D2}min" : $"{horas}h";
        }

        [RelayCommand]
        private async Task Voltar()
            => await _navigation.PopAsync();
    }

    public class ServicoMetricaItem
    {
        public string Servico { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public double Percentual { get; set; }
        public double Progresso { get; set; }
        public Color Cor { get; set; }
        public string FaturamentoTexto { get; set; } = string.Empty;
        public string TempoMedioTexto { get; set; } = string.Empty;
        public string QuantidadeTexto => $"{Quantidade} ({Percentual:0.#}%)";
    }

    public class EvolucaoDiaItem
    {
        public DateTime Data { get; set; }
        public int Quantidade { get; set; }
    }

    public class MaiorAtendimentoItem
    {
        public string Servico { get; set; } = string.Empty;
        public string DuracaoTexto { get; set; } = string.Empty;
    }
}
