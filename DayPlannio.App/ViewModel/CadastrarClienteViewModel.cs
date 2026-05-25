using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Models;
using DayPlannio.App.Services;

namespace DayPlannio.App.ViewModels;

public partial class CadastrarClienteViewModel : ObservableObject
{
    private readonly Page _page;
    private readonly INavigation _navigation;
    private readonly string _userId;

    public CadastrarClienteViewModel(Page page, INavigation navigation)
    {
        _page = page;
        _navigation = navigation;

        _userId = Preferences.Get("userId", string.Empty);
    }

    [ObservableProperty]
    private string nome;

    [ObservableProperty]
    private string telefone;

    [ObservableProperty]
    private string endereco;

    [ObservableProperty]
    private string observacoes;

    [ObservableProperty]
    private string erro;

    [ObservableProperty]
    private bool erroVisivel;

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

        if (formatted != value)
            Telefone = formatted;
    }

    [RelayCommand]
    private async Task Criar()
    {
        try
        {
            ErroVisivel = false;

            if (string.IsNullOrWhiteSpace(Nome))
                throw new Exception("O nome é obrigatório.");

            if (string.IsNullOrWhiteSpace(Telefone))
                throw new Exception("O telefone é obrigatório.");

            if (string.IsNullOrWhiteSpace(Endereco))
                throw new Exception("O endereço é obrigatório.");

            var telefoneNumeros = new string(Telefone.Where(char.IsDigit).ToArray());

            if (telefoneNumeros.Length != 10 && telefoneNumeros.Length != 11)
                throw new Exception("Telefone inválido. Use o formato (11) 91234-5678.");

            var cliente = new
            {
                UsuarioId = Guid.Parse(_userId),
                Nome = Nome,
                Telefone = Telefone,
                Endereco = Endereco,
                Observacoes = Observacoes
            };

            var resultado = await ClienteService.Create(cliente);

            if (resultado.sucesso)
            {
                await _page.DisplayAlertAsync(
                    "Sucesso",
                    "Cliente criado com sucesso!",
                    "OK");

                await _navigation.PopAsync();
            }
            else
            {
                throw new Exception("Erro ao criar o cliente.");
            }
        }
        catch (Exception ex)
        {
            Erro = ex.Message;
            ErroVisivel = true;
        }
    }

    [RelayCommand]
    private async Task Cancelar()
    {
        await _navigation.PopAsync();
    }

    [RelayCommand]
    private async Task Voltar()
    {
        await _navigation.PopAsync();
    }
}