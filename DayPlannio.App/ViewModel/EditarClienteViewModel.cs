using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Helpers;
using DayPlannio.App.Models;
using DayPlannio.App.Services;
using DayPlannio.App.Views;

namespace DayPlannio.App.ViewModels;

public partial class EditarClienteViewModel : ObservableObject
{
    private readonly string _clienteId;

    public EditarClienteViewModel(
        string clienteId,
        string nome,
        string telefone,
        string endereco,
        string observacoes,
        double? latitude = null,
        double? longitude = null,
        int raioGeofenceMetros = RaioGeofence.Padrao)
    {
        _clienteId = clienteId;
        RaioSelecionado = RaioGeofence.ParaTexto(raioGeofenceMetros);

        Nome = nome;
        Telefone = telefone;
        Endereco = endereco;
        Latitude = latitude;
        Longitude = longitude;
        Observacoes = observacoes;
    }

    [ObservableProperty]
    private string nome;

    [ObservableProperty]
    private string telefone;

    [ObservableProperty]
    private string endereco;

    [ObservableProperty]
    private double? latitude;

    [ObservableProperty]
    private double? longitude;

    [ObservableProperty]
    private string observacoes;

    public IReadOnlyList<string> OpcoesRaio => RaioGeofence.Opcoes;

    [ObservableProperty]
    private string raioSelecionado = RaioGeofence.ParaTexto(RaioGeofence.Padrao);

    [ObservableProperty]
    private string erroMensagem;

    [ObservableProperty]
    private bool erroVisivel;

    partial void OnTelefoneChanged(string value)
    {
        var text = new string((value ?? "").Where(char.IsDigit).ToArray());

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
    private async Task Salvar()
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

            var telefoneNumeros = new string(
                Telefone.Where(char.IsDigit).ToArray());

            if (telefoneNumeros.Length != 10 &&
                telefoneNumeros.Length != 11)
            {
                throw new Exception(
                    "Telefone inválido. Use o formato (11) 91234-5678.");
            }

            var cliente = new
            {
                Id = Guid.Parse(_clienteId),
                Nome = Nome,
                Telefone = Telefone,
                Endereco = Endereco,
                Latitude = Latitude,
                Longitude = Longitude,
                RaioGeofenceMetros = RaioGeofence.ParaMetros(RaioSelecionado),
                Observacoes = Observacoes
            };

            var resultado =
                await ClienteService.Edit(
                    _clienteId,
                    cliente);

            if (resultado.sucesso)
            {
                await Application.Current.Windows[0].Page.DisplayAlertAsync(
                    "Sucesso",
                    "Cliente atualizado com sucesso!",
                    "OK");

                await Application.Current.Windows[0]
                    .Page.Navigation.PopAsync();
            }
            else
            {
                throw new Exception("Erro ao Salvar Cliente");
            }
        }
        catch (Exception ex)
        {
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
    }

    [RelayCommand]
    private async Task Cancelar()
    {
        await Application.Current.Windows[0].Page.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task Voltar()
    {
        await Application.Current.Windows[0].Page.Navigation.PopAsync();
    }
}