using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;

namespace DayPlannio.App.ViewModels;

public partial class ConfirmarCodigoViewModel : ObservableObject
{
    private readonly string _email;
    private readonly INavigation _navigation;

    public ConfirmarCodigoViewModel(string email, INavigation navigation)
    {
        _email = email;
        _navigation = navigation;
    }

    [ObservableProperty]
    private string codigo;

    [ObservableProperty]
    private string erroMensagem;

    [ObservableProperty]
    private bool erroVisivel;

    [RelayCommand]
    private async Task Confirmar()
    {
        try
        {
            ErroVisivel = false;

            if (string.IsNullOrWhiteSpace(Codigo) || Codigo.Length < 6)
                throw new Exception("Digite o código de 6 dígitos.");

            var (valido, erro) = await UsuarioService.VerificarCodigo(_email, Codigo);

            if (valido)
            {
                await _navigation.PushAsync(
                    new Views.NovaSenha(_email, Codigo));
            }
            else
            {
                throw new Exception(string.IsNullOrWhiteSpace(erro) ? "Código inválido ou expirado." : erro);
            }
        }
        catch (Exception ex)
        {
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
    }
}