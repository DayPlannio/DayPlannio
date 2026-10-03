using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;

namespace DayPlannio.App.ViewModels;

public partial class RedefinirSenhaViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    public RedefinirSenhaViewModel(INavigation navigation, string emailInicial = "")
    {
        _navigation = navigation;
        Email = emailInicial;
        ConfirmarEmail = emailInicial;
    }

    [ObservableProperty]
    private string email;

    [ObservableProperty]
    private string confirmarEmail;

    [ObservableProperty]
    private string erroMensagem;

    [ObservableProperty]
    private bool erroVisivel;

    [ObservableProperty]
    private string avisoMensagem;

    [ObservableProperty]
    private bool avisoVisivel;

    [RelayCommand]
    private async Task Voltar()
    {
        await _navigation.PopAsync();
    }

    public void LimparAviso()
    {
        AvisoVisivel = false;
        AvisoMensagem = null;
    }

    [RelayCommand]
    private async Task Redefinir()
    {
        try
        {
            ErroVisivel = false;
            AvisoVisivel = false;

            if (string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(ConfirmarEmail))
                throw new Exception("Preencha todos os campos.");

            if (Email != ConfirmarEmail)
                throw new Exception("Os e-mails não coincidem.");

            var (sucesso, mensagem) = await UsuarioService.EnviarRedefinicaoSenha(Email);

            if (sucesso)
            {
                if (!string.IsNullOrWhiteSpace(mensagem))
                {
                    AvisoMensagem = mensagem;
                    AvisoVisivel = true;
                }

                await _navigation.PushAsync(
                    new Views.ConfirmarCodigo(Email));
            }
            else
            {
                ErroMensagem = string.IsNullOrWhiteSpace(mensagem) ? "Erro ao enviar e-mail." : mensagem;
                ErroVisivel = true;
            }
        }
        catch (Exception ex)
        {
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
    }
}