namespace DayPlannio.App.Views;

public partial class PoliticaPrivacidade : ContentPage
{
    private readonly Action<bool> _aoDecidir;
    private bool _botoesLiberados;

    public PoliticaPrivacidade(Action<bool> aoDecidir)
    {
        InitializeComponent();
        _aoDecidir = aoDecidir;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetNavBarIsVisible(this, false);
        AtualizarEstadoBotoes();
    }

    private void OnScrolled(object sender, ScrolledEventArgs e)
    {
        AtualizarEstadoBotoes();
    }

    private void OnScrollViewSizeChanged(object sender, EventArgs e)
    {
        AtualizarEstadoBotoes();
    }

    private void AtualizarEstadoBotoes()
    {
        if (_botoesLiberados)
            return;

        var altura = ConteudoScroll.ContentSize.Height;
        if (altura <= 0)
            return;

        var alcance = Math.Max(0, altura - ConteudoScroll.Height);

        if (ConteudoScroll.ScrollY >= alcance - 20)
        {
            _botoesLiberados = true;
            BotaoConcordar.IsEnabled = true;
            BotaoNaoConcordar.IsEnabled = true;
            AvisoRolagem.IsVisible = false;
        }
    }

    private void Finalizar(bool concordou)
    {
        _aoDecidir?.Invoke(concordou);
        Navigation.PopAsync();
    }

    private void OnConcordarClicked(object sender, EventArgs e)
    {
        Finalizar(true);
    }

    private void OnNaoConcordarClicked(object sender, EventArgs e)
    {
        Finalizar(false);
    }

    private void OnVoltarClicked(object sender, EventArgs e)
    {
        Finalizar(false);
    }
}