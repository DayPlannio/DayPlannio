using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DayPlannio.App.ViewModels;

public partial class OpcaoPlanoCadastro : ObservableObject
{
    public string Nome { get; init; } = "";
    public string Preco { get; init; } = "";
    public string Selo { get; init; } = "";
    public string Descricao { get; init; } = "";
    public string Caracteristicas { get; init; } = "";
    public string Cor { get; init; } = "#006260";
    public string Plano { get; init; } = "";
    public bool TesteGratuito { get; init; }
    public ICommand SelecionarCommand { get; init; } = null!;

    private bool _selecionado;

    public bool Selecionado
    {
        get => _selecionado;
        set
        {
            if (_selecionado == value)
                return;

            _selecionado = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BordaLargura));
            OnPropertyChanged(nameof(BordaCor));
            OnPropertyChanged(nameof(Fundo));
        }
    }

    public double BordaLargura => Selecionado ? 2 : 0;
    public string BordaCor => Selecionado ? Cor : "Transparent";
    public string Fundo => Selecionado ? "#F3FAF9" : "White";
}

public partial class CadastroUsuarioViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    public CadastroUsuarioViewModel(INavigation navigation)
    {
        _navigation = navigation;

        MontarOpcoesPlano();
    }

    public ObservableCollection<OpcaoPlanoCadastro> OpcoesPlano { get; } = new();

    [ObservableProperty]
    private string nome;

    [ObservableProperty]
    private string email;

    [ObservableProperty]
    private string senha;

    [ObservableProperty]
    private string confirmarSenha;

    [ObservableProperty]
    private string erroMensagem;

    [ObservableProperty]
    private bool erroVisivel;

    [ObservableProperty]
    private bool reqTamanho;

    [ObservableProperty]
    private bool reqMaiusculo;

    [ObservableProperty]
    private bool reqMinusculo;

    [ObservableProperty]
    private bool reqEspecial;

    [ObservableProperty]
    private bool reqNumero;

    [ObservableProperty]
    private bool senhaOculta = true;

    [ObservableProperty]
    private bool confirmarSenhaOculta = true;

    [ObservableProperty]
    private bool planoVisivel;

    [ObservableProperty]
    private string planoSelecionado = "";

    [ObservableProperty]
    private string planoSelecionadoNome = "";

    [ObservableProperty]
    private bool avisoPlanoVisivel;

    [ObservableProperty]
    private string avisoPlano = "";

    [ObservableProperty]
    private bool cadastroVisivel = true;

    [ObservableProperty]
    private bool concordaPrivacidade;

    private void MontarOpcoesPlano()
    {
        OpcoesPlano.Add(new OpcaoPlanoCadastro
        {
            Nome = "Testar Full por 7 dias",
            Preco = "Grátis",
            Selo = "Recomendado para conhecer",
            Descricao = "Libera todos os recursos do plano Full durante 7 dias, sem cobrança.",
            Caracteristicas = "• Agenda, clientes e serviços\n• Financeiro e métricas\n• Fotos e portfólio\n• Geofencing (em construção)",
            Cor = "#006260",
            TesteGratuito = true,
            SelecionarCommand = new Command(() => SelecionarPlano(true, ""))
        });

        OpcoesPlano.Add(new OpcaoPlanoCadastro
        {
            Nome = "Básico",
            Preco = "R$ 9,90/mês",
            Selo = "Para quem está começando",
            Descricao = "O essencial para organizar sua agenda.",
            Caracteristicas = "• Criar, editar e cancelar agendamentos\n• Ver a agenda\n• Cadastrar clientes\n• Histórico de clientes\n• Cadastrar tipos de serviço\n• Perfil do profissional",
            Cor = "#6E7978",
            Plano = "Basico",
            SelecionarCommand = new Command(() => SelecionarPlano(false, "Basico"))
        });

        OpcoesPlano.Add(new OpcaoPlanoCadastro
        {
            Nome = "Profissional",
            Preco = "R$ 19,90/mês",
            Selo = "Para quem usa o app no dia a dia",
            Descricao = "Tudo do Básico mais o controle financeiro.",
            Caracteristicas = "• Tudo do Básico\n• Entradas e saídas financeiras\n• Cálculo de lucro\n• Lucro geral\n• Relatórios financeiros\n• Métricas de serviços",
            Cor = "#1E88E5",
            Plano = "Profissional",
            SelecionarCommand = new Command(() => SelecionarPlano(false, "Profissional"))
        });

        OpcoesPlano.Add(new OpcaoPlanoCadastro
        {
            Nome = "Full",
            Preco = "R$ 29,90/mês",
            Selo = "Para aproveitar toda a plataforma",
            Descricao = "Tudo do Profissional, com as ferramentas que seu cliente enxerga.",
            Caracteristicas = "• Tudo do Profissional\n• Portfólio público\n• Upload de fotos dos serviços\n• Área/acesso do cliente\n• Métricas para o cliente\n• Geolocalização/geofencing (em construção)",
            Cor = "#006260",
            Plano = "Full",
            SelecionarCommand = new Command(() => SelecionarPlano(false, "Full"))
        });
    }

    private void SelecionarPlano(bool testeGratuito, string plano)
    {
        PlanoSelecionado = plano;
        PlanoSelecionadoNome = testeGratuito
            ? "Full por 7 dias grátis"
            : PlanoAppService.NomeExibicao(plano);

        _testeGratuito = testeGratuito;
        AvisoPlanoVisivel = false;

        foreach (var opcao in OpcoesPlano)
            opcao.Selecionado = opcao.TesteGratuito == testeGratuito && opcao.Plano == plano;

        OnPropertyChanged(nameof(ContinuarTexto));
    }

    private bool _testeGratuito;

    public string ContinuarTexto => string.IsNullOrWhiteSpace(PlanoSelecionado) && !_testeGratuito
        ? "Escolha um plano para continuar"
        : $"Continuar com o {PlanoSelecionadoNome}";

    [RelayCommand]
    private void VoltarParaDados()
    {
        PlanoVisivel = false;
        CadastroVisivel = true;
    }

    public string IconeSenha =>
        SenhaOculta ? "icon_olho_aberto.png" : "icon_olho_fechado.png";

    public string IconeConfirmarSenha =>
        ConfirmarSenhaOculta ? "icon_olho_aberto.png" : "icon_olho_fechado.png";

    public bool SenhasDiferentes =>
        !string.IsNullOrEmpty(ConfirmarSenha) && Senha != ConfirmarSenha;

    partial void OnConfirmarSenhaChanged(string value)
    {
        OnPropertyChanged(nameof(SenhasDiferentes));
    }

    partial void OnSenhaChanged(string value)
    {
        OnPropertyChanged(nameof(SenhasDiferentes));

        value ??= "";

        ReqTamanho = value.Length >= 6;
        ReqMaiusculo = value.Any(char.IsUpper);
        ReqMinusculo = value.Any(char.IsLower);
        ReqNumero = value.Any(char.IsDigit);
        ReqEspecial = value.Any(c => !char.IsLetterOrDigit(c));
    }

    [RelayCommand]
    private async Task LerPoliticaPrivacidade()
    {
        await _navigation.PushAsync(new Views.PoliticaPrivacidade(concordou =>
        {
            ConcordaPrivacidade = concordou;
        }));
    }

    [RelayCommand]
    private async Task Cadastrar()
    {
        try
        {
            ErroVisivel = false;

            if (!ValidarCampos())
                return;

            if (!PlanoVisivel)
            {
                if (!ConcordaPrivacidade)
                {
                    ErroMensagem = "Leia e concorde com a Política de Privacidade para continuar.";
                    ErroVisivel = true;
                    return;
                }

                CadastroVisivel = false;
                PlanoVisivel = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(PlanoSelecionado) && !_testeGratuito)
                throw new Exception("Escolha um plano para continuar o cadastro.");

            var usuario = new
            {
                nomeCompleto = Nome,
                email = Email,
                senha = Senha,
                confirmeSenha = ConfirmarSenha,
                usarTesteGratuito = _testeGratuito,
                plano = string.IsNullOrWhiteSpace(PlanoSelecionado) ? null : PlanoSelecionado
            };

            var (userId, erro) = await UsuarioService.Cadastrar(usuario);

            if (userId == null)
                throw new Exception(!string.IsNullOrWhiteSpace(erro) ? erro : "Erro ao cadastrar. Verifique os dados.");

            Preferences.Set("userId", userId);

            PlanoAppService.Invalidar();

            if (_testeGratuito)
            {
                await ExibirAlertaAsync(
                    "Bem-vindo!",
                    "Seu teste gratuito do plano Full foi ativado por 7 dias.\n\nAproveite para testar todos os recursos da plataforma. Quando o teste acabar você escolhe o plano que quiser continuar.");
            }
            else
            {
                await ExibirAlertaAsync(
                    "Solicitação enviada!",
                    $"Você escolheu o plano {PlanoAppService.NomeExibicao(PlanoSelecionado)}.\n\nA solicitação foi enviada e aguarda aprovação do administrador. Assim que for aprovada você recebe uma notificação aqui no app e o acesso é liberado.");
            }

            Application.Current.Windows[0].Page =
                new NavigationPage(_testeGratuito
                    ? (Page)new Views.Agendamentos()
                    : new Views.MeuPerfil());

            await Views.FimTesteGratuito.ExibirSeNecessarioAsync(null);
        }
        catch (Exception ex)
        {
            VoltarParaDados();
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
    }

    private bool ValidarCampos()
    {
        if (string.IsNullOrWhiteSpace(Nome) ||
            string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(Senha) ||
            string.IsNullOrWhiteSpace(ConfirmarSenha))
            throw new Exception("Preencha todos os campos obrigatórios.");

        if (Senha != ConfirmarSenha)
            throw new Exception("As senhas não coincidem.");

        if (!ReqTamanho ||
            !ReqMaiusculo ||
            !ReqMinusculo ||
            !ReqNumero ||
            !ReqEspecial)
            throw new Exception("A senha não atende aos requisitos.");

        return true;
    }

    private static Task ExibirAlertaAsync(string titulo, string mensagem)
    {
        var page = Application.Current?.MainPage;
        if (page == null)
            return Task.CompletedTask;

        return page.DisplayAlert(titulo, mensagem, "OK");
    }

    [RelayCommand]
    private async Task Login()
    {
        await _navigation.PushAsync(new Views.Login());
    }

    [RelayCommand]
    private void MostrarOcultarSenha()
    {
        SenhaOculta = !SenhaOculta;

        OnPropertyChanged(nameof(IconeSenha));
    }

    [RelayCommand]
    private void MostrarOcultarConfirmarSenha()
    {
        ConfirmarSenhaOculta = !ConfirmarSenhaOculta;

        OnPropertyChanged(nameof(IconeConfirmarSenha));
    }
}
