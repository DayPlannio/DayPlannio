using DayPlannio.App.Services;
using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class FimTesteGratuito : ContentPage
{
    private readonly FimTesteGratuitoViewModel _viewModel;

    public FimTesteGratuito(DateTime? expirouEm)
    {
        InitializeComponent();

        PlanoAppService.MarcarAvisoTrialMostrado();

        _viewModel = new FimTesteGratuitoViewModel(this, expirouEm);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.CarregarAsync();
    }

    public static async Task ExibirSeNecessarioAsync(DateTime? expirouEm)
    {
        var status = await PlanoAppService.ObterStatusTesteGratuitoAsync();

        if (!status.Expirado)
            return;

        var userId = Preferences.Get("userId", string.Empty);
        if (!string.IsNullOrEmpty(userId))
        {
            var perfil = await UsuarioService.GetPerfil(userId);
            if (perfil != null && !string.IsNullOrWhiteSpace(perfil.PlanoPendente))
                return;
        }

        var page = Application.Current?.Windows[0]?.Page;
        if (page == null)
            return;

        if (page.Navigation.ModalStack.Count > 0)
            return;

        await page.Navigation.PushModalAsync(new FimTesteGratuito(expirouEm ?? status.ExpirouEm));
    }
}
