using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Models;
using DayPlannio.App.Services;

namespace DayPlannio.App.ViewModels;

public partial class MeuPerfilViewModel : ObservableObject
{
    private readonly Page _page;
    private readonly string _userId;

    public MeuPerfilViewModel(Page page)
    {
        _page = page;
        _userId = Preferences.Get("userId", string.Empty);
    }

    [ObservableProperty]
    private string nomeCompleto;

    [ObservableProperty]
    private string email;

    [ObservableProperty]
    private string telefone;

    [ObservableProperty]
    private string cidade;

    [ObservableProperty]
    private bool cidadeVisivel;

    [ObservableProperty]
    private bool telefoneVisivel;

    [ObservableProperty]
    private string planoAtual = "";

    [ObservableProperty]
    private string planoDetalhe = "";

    [ObservableProperty]
    private string planoPendenteTexto = "";

    [ObservableProperty]
    private bool planoPendenteVisivel;

    [ObservableProperty]
    private string erroMensagem;

    [ObservableProperty]
    private bool erroVisivel;

    public async Task CarregarPerfil()
    {
        try
        {
            ErroVisivel = false;

            if (string.IsNullOrEmpty(_userId))
                throw new Exception("Usuário não encontrado. Faça login novamente.");

            var perfil = await UsuarioService.GetPerfil(_userId);

            if (perfil != null)
            {
                NomeCompleto = perfil.NomeCompleto;
                Email = perfil.Email;
                Telefone = perfil.Telefone ?? "";
                Cidade = perfil.Cidade ?? "";
                CidadeVisivel = perfil.CidadeVisivel;
                TelefoneVisivel = perfil.TelefoneVisivel;
                AtualizarInfosPlano(perfil);
            }
            else
            {
                throw new Exception("Erro ao carregar perfil.");
            }
        }
        catch (Exception ex)
        {
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
    }

    private void AtualizarInfosPlano(Usuario? perfil)
    {
        var planoEfetivo = PlanoAppService.PlanoEfetivo(perfil);

        PlanoAtual = PlanoAppService.TemAssinaturaAtiva(planoEfetivo)
            ? PlanoAppService.NomeExibicao(planoEfetivo)
            : "Sem assinatura ativa";

        var origem = (perfil?.PlanoOrigem ?? "").ToLowerInvariant() switch
        {
            "trial" => "Teste gratuito",
            "assinatura" => "Assinatura",
            _ => ""
        };

        var detalhe = new List<string>();
        if (perfil?.PlanoExpiraEm != null)
            detalhe.Add($"Vence em {perfil.PlanoExpiraEm.Value.ToLocalTime():dd/MM/yyyy}");
        if (!string.IsNullOrWhiteSpace(origem))
            detalhe.Add(origem);
        PlanoDetalhe = string.Join("  |  ", detalhe);

        PlanoPendenteTexto = string.IsNullOrWhiteSpace(perfil?.PlanoPendente)
            ? ""
            : $"Solicitação do plano {PlanoAppService.NomeExibicao(perfil!.PlanoPendente)} aguardando aprovação do administrador.";
        PlanoPendenteVisivel = !string.IsNullOrWhiteSpace(perfil?.PlanoPendente);
    }

    [RelayCommand]
    private async Task AbrirPoliticaPrivacidade()
    {
        await _page.Navigation.PushAsync(new Views.PoliticaPrivacidade(AoDecidirPoliticaPrivacidade));
    }

    private async void AoDecidirPoliticaPrivacidade(bool concordou)
    {
        if (concordou)
        {
            await _page.DisplayAlertAsync(
                "Política de Privacidade",
                "Você continua de acordo. Seu acesso permanece liberado.",
                "OK");
            return;
        }

        var encerrar = await _page.DisplayAlertAsync(
            "Você não concorda mais",
            "Se você não concordar com a Política de Privacidade, a sua conta será encerrada e os seus dados serão removidos do DayPlannio.\n\nDeseja realmente encerrar a sua conta?",
            "Encerrar conta",
            "Cancelar");

        if (!encerrar)
            return;

        await ConfirmarEncerrarConta();
    }

    [RelayCommand]
    private async Task AbrirEncerrarConta()
    {
        await _page.Navigation.PushModalAsync(new Views.EncerrarConta(AoDecidirEncerrarConta));
    }

    private async void AoDecidirEncerrarConta(bool confirmar)
    {
        if (!confirmar)
            return;

        await ConfirmarEncerrarConta();
    }

    private async Task ConfirmarEncerrarConta()
    {
        var resultado = await UsuarioService.EncerrarConta(_userId);

        if (!resultado.sucesso)
        {
            await _page.DisplayAlertAsync("Erro", resultado.mensagem, "OK");
            return;
        }

        PlanoAppService.Invalidar();
        Preferences.Clear();

        await _page.DisplayAlertAsync("Conta encerrada", "Sua conta foi encerrada e seus dados foram removidos. Até logo!", "OK");

        Application.Current.Windows[0].Page =
            new NavigationPage(new Views.Login());
    }

    [RelayCommand]
    private async Task EscolherPlano()
    {
        await _page.Navigation.PushAsync(new Views.Planos());
    }

    partial void OnTelefoneChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var text = new string(value.Where(char.IsDigit).ToArray());

        string formatted = text.Length switch
        {
            <= 2 => $"({text}",
            <= 7 => $"({text[..2]}) {text[2..]}",
            <= 11 => $"({text[..2]}) {text[2..7]}-{text[7..]}",
            _ => $"({text[..2]}) {text[2..7]}-{text[7..11]}"
        };

        if (Telefone != formatted)
            Telefone = formatted;
    }

    [RelayCommand]
    private async Task Salvar()
    {
        try
        {
            ErroVisivel = false;

            if (string.IsNullOrWhiteSpace(NomeCompleto) ||
                string.IsNullOrWhiteSpace(Email))
            {
                throw new Exception(
                    "Preencha os campos obrigatórios.");
            }

            var perfil = new
            {
                Id = Guid.Parse(_userId),
                NomeCompleto = NomeCompleto,
                Email = Email,
                Telefone = string.IsNullOrWhiteSpace(Telefone) ? null : Telefone,
                Cidade = string.IsNullOrWhiteSpace(Cidade) ? null : Cidade,
                CidadeVisivel = CidadeVisivel,
                TelefoneVisivel = TelefoneVisivel
            };

            var resultado =
                await UsuarioService.Edit(
                    _userId,
                    perfil);

            if (resultado.sucesso)
            {
                await _page.DisplayAlertAsync(
                    "Sucesso",
                    "Perfil atualizado com sucesso!",
                    "OK");
            }
            else
            {
                throw new Exception(resultado.erro);
            }
        }
        catch (Exception ex)
        {
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
    }

    [RelayCommand]
    private async Task Sair()
    {
        bool confirmar = await _page.DisplayAlertAsync(
            "Sair da conta",
            "Tem certeza que deseja encerrar a sessão?",
            "Sim",
            "Cancelar");

        if (!confirmar)
            return;

        Preferences.Remove("userId");

        Application.Current.Windows[0].Page =
            new NavigationPage(new Views.Login());
    }
}