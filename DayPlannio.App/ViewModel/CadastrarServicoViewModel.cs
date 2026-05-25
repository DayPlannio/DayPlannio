using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Models;
using DayPlannio.App.Services;

namespace DayPlannio.App.ViewModels;

public partial class CadastrarServicoViewModel : ObservableObject
{
    private readonly Page _page;
    private readonly INavigation _navigation;
    private readonly string _userId;

    public CadastrarServicoViewModel(
        Page page,
        INavigation navigation)
    {
        _page = page;
        _navigation = navigation;

        _userId = Preferences.Get(
            "userId",
            string.Empty);
    }

    [ObservableProperty]
    private string tipo;

    [ObservableProperty]
    private string descricao;

    [ObservableProperty]
    private string tempo;

    [ObservableProperty]
    private string erro;

    [ObservableProperty]
    private bool erroVisivel;

    [RelayCommand]
    private async Task Criar()
    {
        try
        {
            ErroVisivel = false;

            if (string.IsNullOrWhiteSpace(Tipo))
                throw new Exception(
                    "O nome do serviço é obrigatório.");

            if (string.IsNullOrWhiteSpace(Tempo))
                throw new Exception(
                    "O tempo estimado é obrigatório.");

            if (!int.TryParse(Tempo, out int tempoEstimado)
                || tempoEstimado <= 0)
            {
                throw new Exception(
                    "Informe um tempo estimado válido.");
            }

            var servico = new
            {
                UsuarioId = Guid.Parse(_userId),
                Tipo = Tipo,
                Descricao = Descricao,
                TempoEstimado = tempoEstimado
            };

            var resultado = await ServicoService.Create(servico);

            if (resultado.sucesso)
            {
                await _page.DisplayAlertAsync(
                    "Sucesso",
                    "Serviço criado com sucesso!",
                    "OK");

                await _navigation.PopAsync();
            }
            else
            {
                throw new Exception(resultado.erro);
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
}