using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Models;
using DayPlannio.App.Services;

namespace DayPlannio.App.ViewModels;

public partial class EditarServicoViewModel : ObservableObject
{
    private readonly string _servicoId;
    private readonly INavigation _navigation;
    private readonly Page _page;

    public EditarServicoViewModel(
        Servico servico,
        INavigation navigation,
        Page page)
    {
        _servicoId = servico.Id;
        _navigation = navigation;
        _page = page;

        Tipo = servico.Tipo;
        Descricao = servico.Descricao;
        Tempo = servico.TempoEstimado.ToString();
    }

    [ObservableProperty]
    private string tipo;

    [ObservableProperty]
    private string descricao;

    [ObservableProperty]
    private string tempo;

    [ObservableProperty]
    private string erroMensagem;

    [ObservableProperty]
    private bool erroVisivel;

    [RelayCommand]
    private async Task Salvar()
    {
        try
        {
            ErroVisivel = false;

            if (string.IsNullOrWhiteSpace(Tipo))
                throw new Exception("O tipo de serviço é obrigatório.");

            if (string.IsNullOrWhiteSpace(Tempo))
                throw new Exception("O tempo estimado é obrigatório.");

            if (!int.TryParse(Tempo, out int tempoEstimado) || tempoEstimado <= 0)
                throw new Exception("Informe um tempo estimado válido.");

            var servico = new
            {
                Id = Guid.Parse(_servicoId),
                Tipo = Tipo,
                Descricao = Descricao,
                TempoEstimado = tempoEstimado
            };

            var resultado =
                await ServicoService.Edit(
                    _servicoId,
                    servico);

            if (resultado.sucesso)
            {
                await _page.DisplayAlertAsync(
                    "Sucesso",
                    "Serviço atualizado com sucesso!",
                    "OK");

                await _navigation.PopAsync();
            }
            else
            {
                throw new Exception("Erro ao atualizar serviço.");
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
        await _navigation.PopAsync();
    }

    [RelayCommand]
    private async Task Voltar()
    {
        await _navigation.PopAsync();
    }
}