using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;

namespace DayPlannio.App.ViewModels;

public partial class RedefinirSenhaViewModel : ObservableObject
{
    private readonly INavigation _navigation;

    public RedefinirSenhaViewModel(INavigation navigation)
    {
        _navigation = navigation;
    }

    [ObservableProperty]
    private string email;

    [ObservableProperty]
    private string confirmarEmail;

    [ObservableProperty]
    private string erroMensagem;

    [ObservableProperty]
    private bool erroVisivel;

    [RelayCommand]
    private async Task Redefinir()
    {
        try
        {
            ErroVisivel = false;

            if (string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(ConfirmarEmail))
                throw new Exception("Preencha todos os campos.");

            if (Email != ConfirmarEmail)
                throw new Exception("Os e-mails não coincidem.");

            bool sucesso = await UsuarioService.EnviarRedefinicaoSenha(Email);

            if (sucesso)
            {
                await _navigation.PushAsync(
                    new Views.ConfirmarCodigo(Email));
            }
            else
            {
                throw new Exception("Erro ao enviar e-mail.");
            }
        }
        catch (Exception ex)
        {
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
    }
}