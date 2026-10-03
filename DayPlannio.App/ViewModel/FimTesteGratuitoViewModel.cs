using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;

namespace DayPlannio.App.ViewModels;

public partial class FimTesteGratuitoViewModel : ObservableObject
{
    private readonly Page _page;
    private readonly string _userId;

    [ObservableProperty]
    private bool carregando;

    [ObservableProperty]
    private bool avisoVisivel;

    [ObservableProperty]
    private string aviso = "";

    public FimTesteGratuitoViewModel(Page page, DateTime? expirouEm)
    {
        _page = page;
        _userId = Preferences.Get("userId", string.Empty);

        DataFim = expirouEm?.ToString("dd/MM/yyyy") ?? "";
    }

    public string DataFim { get; }

    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;

            var status = await PlanoAppService.ObterStatusTesteGratuitoAsync();

            if (status.Expirado)
            {
                Aviso = $"Seu teste gratuito do plano Full encerrou em {status.ExpirouEm:dd/MM/yyyy}.";
                AvisoVisivel = true;
            }
        }
        catch (Exception ex)
        {
            Aviso = ex.Message;
            AvisoVisivel = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private async Task EscolherPlano(string plano)
    {
        try
        {
            Carregando = true;

            var (sucesso, mensagem) = await UsuarioService.SolicitarPlano(_userId, plano);

            if (!sucesso)
                throw new Exception(mensagem);

            PlanoAppService.Invalidar();
            PlanoAppService.MarcarAvisoTrialMostrado();

            await _page.DisplayAlertAsync(
                "Solicitação enviada!",
                $"Você escolheu o plano {PlanoAppService.NomeExibicao(plano)}.\n\nA solicitação foi enviada e aguarda aprovação do administrador. Assim que for aprovada você recebe uma notificação aqui no app. Enquanto isso, o app continua bloqueado e só o Meu Perfil fica disponível.",
                "OK");

            await Fechar();
        }
        catch (Exception ex)
        {
            await _page.DisplayAlertAsync("Erro", ex.Message, "OK");
        }
        finally
        {
            Carregando = false;
        }
    }

    private async Task Fechar()
    {
        if (_page.Navigation.ModalStack.Contains(_page))
            await _page.Navigation.PopModalAsync();
    }
}
