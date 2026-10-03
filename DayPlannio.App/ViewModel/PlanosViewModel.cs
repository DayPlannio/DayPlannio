using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Models;
using DayPlannio.App.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DayPlannio.App.ViewModels;

public partial class CapaPlano
{
    public string Nome { get; init; } = "";
    public string Preco { get; init; } = "";
    public string Descricao { get; init; } = "";
    public string Caracteristicas { get; init; } = "";
    public Color Cor { get; init; } = Color.FromArgb("#006260");
    public ICommand SolicitarCommand { get; set; } = null!;
}

public partial class PlanosViewModel : ObservableObject
{
    private readonly Page _page;
    private string _userId = "";

    public ObservableCollection<CapaPlano> Planos { get; } = new();

    [ObservableProperty]
    private string mensagem;

    [ObservableProperty]
    private bool mensagemVisivel;

    [ObservableProperty]
    private bool temPlanoPendente;

    [ObservableProperty]
    private bool semAssinatura;

    [ObservableProperty]
    private string planoPendente = "";

    [ObservableProperty]
    private string planoPendentePreco = "";

    [ObservableProperty]
    private string planoPendenteDesde = "";

    [ObservableProperty]
    private string avisoPendente = "";

    [ObservableProperty]
    private string planoAtual = "";

    [ObservableProperty]
    private bool assinaturaVisivel;

    [ObservableProperty]
    private string assinaturaPlano = "";

    [ObservableProperty]
    private string assinaturaPreco = "";

    [ObservableProperty]
    private string assinaturaStatus = "";

    [ObservableProperty]
    private bool renovacaoAtiva;

    [ObservableProperty]
    private bool renovacaoCancelada;

    [ObservableProperty]
    private string avisoRenovacao = "";

    [ObservableProperty]
    private bool avisoRenovacaoVisivel;

    public PlanosViewModel(Page page)
    {
        _page = page;
        _ = Inicializar();
    }

    public async Task Atualizar()
    {
        PlanoAppService.Invalidar();
        await Inicializar();
    }

    [RelayCommand]
    private async Task Voltar()
    {
        await _page.Navigation.PopAsync();
    }

    private async Task Inicializar()
    {
        _userId = Preferences.Get("userId", string.Empty);
        try
        {
            var perfil = await UsuarioService.GetPerfil(_userId);
            if (perfil != null)
            {
                PlanoAtual = PlanoAppService.PlanoEfetivo(perfil);
                SemAssinatura = !PlanoAppService.TemAssinaturaAtiva(PlanoAtual);
                TemPlanoPendente = !string.IsNullOrWhiteSpace(perfil.PlanoPendente);
                MontarAvisoPendente(perfil);
                MontarAssinatura(perfil);
            }
        }
        catch
        {
        }

        MontarCatalogo();
    }

    private void MontarAvisoPendente(Usuario perfil)
    {
        if (string.IsNullOrWhiteSpace(perfil.PlanoPendente))
        {
            PlanoPendente = "";
            PlanoPendentePreco = "";
            PlanoPendenteDesde = "";
            AvisoPendente = "";
            return;
        }

        PlanoPendente = PlanoAppService.NomeExibicao(perfil.PlanoPendente);
        PlanoPendentePreco = PlanoAppService.PrecoMensal(perfil.PlanoPendente);
        PlanoPendenteDesde = perfil.PlanoSolicitadoEm.HasValue
            ? $"Solicitação enviada em {perfil.PlanoSolicitadoEm.Value:dd/MM/yyyy} às {perfil.PlanoSolicitadoEm.Value:HH:mm}."
            : "Solicitação enviada.";

        var atual = SemAssinatura
            ? "Você ainda não tem acesso aos recursos do app até a aprovação"
            : $"Enquanto isso, você continua usando o plano {PlanoAppService.NomeExibicao(PlanoAtual)}";

        AvisoPendente = $"Aguardando aprovação do administrador. Assim que o plano {PlanoPendente} for aprovado você recebe uma notificação aqui no app e o acesso é liberado na hora. {atual}.";
    }

    private void MontarAssinatura(Usuario perfil)
    {
        var isTrial = string.Equals(perfil.PlanoOrigem, "trial", StringComparison.OrdinalIgnoreCase);
        var cancelada = perfil.PlanoCanceladoEm.HasValue;
        var expiraEm = perfil.PlanoExpiraEm;

        AssinaturaVisivel = !string.IsNullOrWhiteSpace(perfil.Plano) &&
                            perfil.PlanoAtivo &&
                            expiraEm.HasValue &&
                            string.Equals(perfil.Plano, PlanoAtual, StringComparison.OrdinalIgnoreCase);

        if (!AssinaturaVisivel)
        {
            AvisoRenovacaoVisivel = false;
            return;
        }

        AssinaturaPlano = PlanoAppService.NomeExibicao(perfil.Plano ?? PlanoAtual);
        AssinaturaPreco = PlanoAppService.PrecoMensal(perfil.Plano ?? PlanoAtual);
        RenovacaoAtiva = perfil.RenovacaoAutomatica && !cancelada;
        RenovacaoCancelada = cancelada;

        if (isTrial)
        {
            AssinaturaStatus = $"Teste grátis até {expiraEm!.Value:dd/MM/yyyy}";
            AvisoRenovacao = "Seu teste grátis está ativo. Ao terminar, você escolhe o plano que quiser.";
            AvisoRenovacaoVisivel = true;
            return;
        }

        if (cancelada)
        {
            var data = perfil.PlanoCanceladoEm!.Value;
            AssinaturaStatus = $"Renovação cancelada — o plano {AssinaturaPlano} acaba em {data:dd/MM/yyyy}";

            if (data > DateTime.Now)
            {
                var restantes = Math.Ceiling((data - DateTime.Now).TotalDays);
                AvisoRenovacao = restantes <= 1
                    ? "Você continua com acesso normal até amanhã. Depois disso o app fica bloqueado e só o Meu Perfil fica disponível, até você escolher um plano."
                    : $"Você continua com acesso normal por mais {restantes} dias. Depois disso o app fica bloqueado e só o Meu Perfil fica disponível, até você escolher um plano.";
                AvisoRenovacaoVisivel = true;
            }
            else
            {
                AvisoRenovacao = string.Empty;
                AvisoRenovacaoVisivel = false;
            }

            return;
        }

        AssinaturaStatus = $"Renova automaticamente em {expiraEm!.Value:dd/MM/yyyy}";
        AvisoRenovacao = $"Renovação automática ligada em {AssinaturaPreco}. Você pode cancelar quando quiser e continua usando o {AssinaturaPlano} até o fim do período já pago.";
        AvisoRenovacaoVisivel = true;
    }

    [RelayCommand]
    private async Task CancelarAssinatura()
    {
        try
        {
            var plano = PlanoAppService.Normalizar(PlanoAtual);
            var confirmado = await _page.DisplayAlertAsync(
                "Cancelar renovação automática?",
                $"Seu plano {PlanoAppService.NomeExibicao(plano)} continua disponível até a data de vencimento. Depois disso o app fica bloqueado e só o Meu Perfil continua acessível, até você escolher um plano. Você não perde nenhum dado.",
                "Cancelar renovação",
                "Voltar");

            if (!confirmado)
                return;

            var (sucesso, mensagem) = await UsuarioService.DefinirRenovacao(_userId, false);

            await _page.DisplayAlertAsync(sucesso ? "Renovação cancelada" : "Não foi possível", mensagem, "OK");

            if (sucesso)
            {
                PlanoAppService.Invalidar();
                await Atualizar();
            }
        }
        catch (Exception ex)
        {
            await _page.DisplayAlertAsync("Erro", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task ReativarAssinatura()
    {
        try
        {
            var (sucesso, mensagem) = await UsuarioService.DefinirRenovacao(_userId, true);

            await _page.DisplayAlertAsync(sucesso ? "Renovação automática ativada" : "Não foi possível", mensagem, "OK");

            if (sucesso)
            {
                PlanoAppService.Invalidar();
                await Atualizar();
            }
        }
        catch (Exception ex)
        {
            await _page.DisplayAlertAsync("Erro", ex.Message, "OK");
        }
    }

    private void MontarCatalogo()
    {
        Planos.Clear();

        var catalogo = new[]
        {
            new {
                nome = "Basico", preco = "R$ 9,90",
                desc = "Para quem está começando:",
                feats = new[]
                {
                    "Criar, editar e cancelar agendamentos",
                    "Visualizar agenda",
                    "Cadastrar clientes",
                    "Histórico de clientes",
                    "Cadastrar tipos de serviço",
                    "Perfil do profissional"
                },
                cor = "#6E7978"
            },
            new {
                nome = "Profissional", preco = "R$ 19,90",
                desc = "Para quem já utiliza o aplicativo no dia a dia:",
                feats = new[]
                {
                    "Tudo do Básico",
                    "Entradas e saídas financeiras",
                    "Cálculo de lucro",
                    "Lucro geral",
                    "Relatórios financeiros",
                    "Métricas de serviços"
                },
                cor = "#1E88E5"
            },
            new {
                nome = "Full", preco = "R$ 29,90",
                desc = "Para aproveitar praticamente toda a plataforma:",
                feats = new[]
                {
                    "Tudo do Profissional",
                    "Portfólio público",
                    "Upload de fotos dos serviços",
                    "Área/acesso do cliente",
                    "Métricas para o cliente",
                    "Geolocalização/geofencing (em construção)"
                },
                cor = "#006260"
            }
        };

        foreach (var c in catalogo)
        {
            var nomeCapturado = c.nome;
            var item = new CapaPlano
            {
                Nome = PlanoAppService.NomeExibicao(c.nome),
                Preco = c.preco,
                Descricao = c.desc,
                Caracteristicas = string.Join("\n", c.feats.Select(f => "• " + f)),
                Cor = Color.FromArgb(c.cor),
                SolicitarCommand = new AsyncRelayCommand(
                    () => Solicitar(nomeCapturado))
            };
            Planos.Add(item);
        }
    }

    private async Task Solicitar(string plano)
    {
        MensagemVisivel = false;

        if (TemPlanoPendente)
        {
            Mensagem = "Você já tem uma solicitação em análise pelo administrador.";
            MensagemVisivel = true;
            return;
        }

        if (string.Equals(plano, PlanoAtual, StringComparison.OrdinalIgnoreCase))
        {
            Mensagem = "Você já usa esse plano.";
            MensagemVisivel = true;
            return;
        }

        var (sucesso, mensagem) = await UsuarioService.SolicitarPlano(_userId, plano);
        Mensagem = string.IsNullOrWhiteSpace(mensagem)
            ? (sucesso ? "Solicitação enviada." : "Não foi possível solicitar o plano.")
            : mensagem;
        MensagemVisivel = true;

        if (sucesso)
        {
            TemPlanoPendente = true;
            PlanoAppService.Invalidar();
            await Atualizar();
        }
    }

    [RelayCommand]
    private async Task CancelarSolicitacao()
    {
        var (sucesso, mensagem) = await UsuarioService.SolicitarPlano(_userId, "Nenhum");

        Mensagem = string.IsNullOrWhiteSpace(mensagem)
            ? (sucesso ? "Solicitação cancelada." : "Não foi possível cancelar a solicitação.")
            : mensagem;

        MensagemVisivel = true;

        if (sucesso)
        {
            PlanoAppService.Invalidar();
            await Atualizar();
        }
    }
}