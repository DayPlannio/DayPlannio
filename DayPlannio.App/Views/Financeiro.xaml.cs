namespace DayPlannio.App.Views;

public partial class Financeiro : ContentPage
{
    private string _periodoSelecionado = "Diario";
    private string _abaSelecionada = "Movimentacoes";

    public Financeiro()
    {
        InitializeComponent();

        AtualizarPeriodo();
        AtualizarAbas();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CustomTabBar.AbaAtual = "financeiro";
    }

    private void OnDiarioClicked(object sender, TappedEventArgs e)
    {
        _periodoSelecionado = "Diario";
        AtualizarPeriodo();
    }

    private void OnSemanalClicked(object sender, TappedEventArgs e)
    {
        _periodoSelecionado = "Semanal";
        AtualizarPeriodo();
    }

    private void OnMensalClicked(object sender, TappedEventArgs e)
    {
        _periodoSelecionado = "Mensal";
        AtualizarPeriodo();
    }

    private void AtualizarPeriodo()
    {
        btnDiario.BackgroundColor = Colors.Transparent;
        btnSemanal.BackgroundColor = Colors.Transparent;
        btnMensal.BackgroundColor = Colors.Transparent;

        txtDiario.TextColor = Color.FromArgb("#4F4F4F");
        txtSemanal.TextColor = Color.FromArgb("#4F4F4F");
        txtMensal.TextColor = Color.FromArgb("#4F4F4F");

        switch (_periodoSelecionado)
        {
            case "Diario":
                btnDiario.BackgroundColor = Colors.White;
                txtDiario.TextColor = Color.FromArgb("#006260");
                break;

            case "Semanal":
                btnSemanal.BackgroundColor = Colors.White;
                txtSemanal.TextColor = Color.FromArgb("#006260");
                break;

            case "Mensal":
                btnMensal.BackgroundColor = Colors.White;
                txtMensal.TextColor = Color.FromArgb("#006260");
                break;
        }
    }

    private void OnMovimentacoesClicked(object sender, TappedEventArgs e)
    {
        _abaSelecionada = "Movimentacoes";
        AtualizarAbas();
    }

    private void OnResumoClicked(object sender, TappedEventArgs e)
    {
        _abaSelecionada = "Resumo";
        AtualizarAbas();
    }

    private void AtualizarAbas()
    {
        btnMovimentacoes.BackgroundColor = Colors.Transparent;
        btnResumo.BackgroundColor = Colors.Transparent;

        txtMovimentacoes.TextColor = Color.FromArgb("#4F4F4F");
        txtResumo.TextColor = Color.FromArgb("#4F4F4F");

        if (_abaSelecionada == "Movimentacoes")
        {
            btnMovimentacoes.BackgroundColor = Colors.White;
            txtMovimentacoes.TextColor = Color.FromArgb("#006260");

            layoutMovimentacoes.IsVisible = true;
            layoutResumo.IsVisible = false;
        }
        else
        {
            btnResumo.BackgroundColor = Colors.White;
            txtResumo.TextColor = Color.FromArgb("#006260");

            layoutMovimentacoes.IsVisible = false;
            layoutResumo.IsVisible = true;
        }
    }

    private async void OnAdicionarClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new CadastrarFinanceiro());
    }

    private async void OnGerarPdfClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync(
            "PDF",
            "Relatório gerado com sucesso.",
            "OK");
    }
}