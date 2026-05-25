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
                Telefone = string.IsNullOrWhiteSpace(Telefone) ? null : Telefone
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