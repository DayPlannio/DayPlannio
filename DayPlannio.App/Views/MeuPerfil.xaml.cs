using DayPlannio.App.Services;
using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class MeuPerfil : ContentPage, IExibeToast
{
    private readonly MeuPerfilViewModel _viewModel;

    public MeuPerfil()
    {
        InitializeComponent();

        _viewModel = new MeuPerfilViewModel(this);

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        NotificacaoMonitor.Iniciar();
        NotificacaoMonitor.NotificacaoRecebida += OnNotificacaoRecebida;

        await _viewModel.CarregarPerfil();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        NotificacaoMonitor.NotificacaoRecebida -= OnNotificacaoRecebida;
    }

    private async void OnNotificacaoRecebida(NotificacaoItem notificacao)
    {
        if (notificacao.Tipo is "plano_aprovado" or "plano_rejeitado")
            await _viewModel.CarregarPerfil();
    }

    public Task MostrarToastAsync(string titulo, string mensagem, bool sucesso)
        => Toast.ShowAsync(titulo, mensagem, sucesso);
}
