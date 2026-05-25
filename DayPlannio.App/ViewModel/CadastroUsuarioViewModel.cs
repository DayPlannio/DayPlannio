using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;

namespace DayPlannio.App.ViewModels;

public partial class CadastroUsuarioViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    public CadastroUsuarioViewModel(INavigation navigation)
    {
        _navigation = navigation;
    }

    [ObservableProperty]
    private string nome;

    [ObservableProperty]
    private string email;

    [ObservableProperty]
    private string senha;

    [ObservableProperty]
    private string confirmarSenha;

    [ObservableProperty]
    private string telefone;

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

    public string IconeSenha =>
        SenhaOculta ? "icon_olho_aberto.png" : "icon_olho_fechado.png";

    public string IconeConfirmarSenha =>
        ConfirmarSenhaOculta ? "icon_olho_aberto.png" : "icon_olho_fechado.png";

    partial void OnSenhaChanged(string value)
    {
        value ??= "";

        ReqTamanho = value.Length >= 6;
        ReqMaiusculo = value.Any(char.IsUpper);
        ReqMinusculo = value.Any(char.IsLower);
        ReqNumero = value.Any(char.IsDigit);
        ReqEspecial = value.Any(c => !char.IsLetterOrDigit(c));
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
    private async Task Cadastrar()
    {
        try
        {
            ErroVisivel = false;

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

            var usuario = new
            {
                nomeCompleto = Nome,
                email = Email,
                telefone = Telefone,
                senha = Senha,
                confirmeSenha = ConfirmarSenha
            };

            string? userId = await UsuarioService.Cadastrar(usuario);

            if (userId != null)
            {
                Preferences.Set("userId", userId);

                Application.Current.Windows[0].Page =
                    new NavigationPage(new Views.Agendamentos());
            }
            else
            {
                throw new Exception("Erro ao cadastrar. Verifique os dados.");
            }
        }
        catch (Exception ex)
        {
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
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
