using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;

namespace DayPlannio.App.ViewModels;

public partial class NovaSenhaViewModel : ObservableObject
{
    private readonly string _email;
    private readonly string _codigo;
    private readonly Page _page;

    public NovaSenhaViewModel(string email, string codigo, Page page)
    {
        _email = email;
        _codigo = codigo;
        _page = page;
    }

    [ObservableProperty]
    private string novaSenha;

    [ObservableProperty]
    private string confirmarSenha;

    [ObservableProperty]
    private string erroMensagem;

    [ObservableProperty]
    private bool erroVisivel;
    [ObservableProperty]
    private bool senhaOculta = true;

    [ObservableProperty]
    private bool confirmarSenhaOculta = true;

    public string IconeSenha =>
        SenhaOculta ? "icon_olho_aberto.png" : "icon_olho_fechado.png";

    public string IconeConfirmarSenha =>
        ConfirmarSenhaOculta ? "icon_olho_aberto.png" : "icon_olho_fechado.png";


    [RelayCommand]
    private async Task Salvar()
    {
        try
        {
            ErroVisivel = false;

            if (string.IsNullOrWhiteSpace(NovaSenha) ||
                string.IsNullOrWhiteSpace(ConfirmarSenha))
                throw new Exception("Preencha todos os campos.");

            if (NovaSenha != ConfirmarSenha)
                throw new Exception("As senhas não coincidem.");

            bool sucesso = await UsuarioService.RedefinirSenha(
                _email,
                _codigo,
                NovaSenha,
                ConfirmarSenha);

            if (sucesso)
            {
                await _page.DisplayAlertAsync(
                    "Sucesso",
                    "Senha redefinida com sucesso!",
                    "OK");

                await _page.Navigation.PopToRootAsync();
            }
            else
            {
                throw new Exception("Código inválido ou expirado.");
            }
        }
        catch (Exception ex)
        {
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
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