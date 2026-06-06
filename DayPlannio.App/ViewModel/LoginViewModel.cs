using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;

namespace DayPlannio.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    public LoginViewModel(INavigation navigation)
    {
        _navigation = navigation;
    }

    [ObservableProperty]
    private string email;

    [ObservableProperty]
    private string senha;

    [ObservableProperty]
    private string erroMensagem;

    [ObservableProperty]
    private bool erroVisivel;
    [ObservableProperty]
    private bool senhaOculta = true;
    public string IconeSenha =>
        SenhaOculta ? "icon_olho_aberto.png" : "icon_olho_fechado.png";

    [RelayCommand]
    private async Task Login()
    {
        try
        {
            ErroVisivel = false;

            if (string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Senha))
                throw new Exception("Preencha todos os campos.");

            string? userId = await UsuarioService.Login(Email, Senha);

            if (userId != null)
            {
                Preferences.Set("userId", userId);

                await App.Db.Insert(new Models.SessaoLocal  
                {
                    UserId = userId,
                    UltimoAcesso = DateTime.Now
                });

                Application.Current.Windows[0].Page =
                    new NavigationPage(new Views.Agendamentos());
            }
            else
            {
                throw new Exception("E-mail ou senha inválidos.");
            }
        }
        catch (Exception ex)
        {
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
    }

    [RelayCommand]
    private async Task Cadastro()
    {
        await _navigation.PushAsync(new Views.CadastroUsuario());
    }

    [RelayCommand]
    private async Task RedefinirSenha()
    {
        await _navigation.PushAsync(new Views.RedefinirSenha());
    }


    [RelayCommand]
    private void MostrarOcultarSenha()
    {
        SenhaOculta = !SenhaOculta;

        OnPropertyChanged(nameof(IconeSenha));
    }
}