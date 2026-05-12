namespace DayPlannio.App.Views;

public partial class Agendamentos : ContentPage
{
    public Agendamentos()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        CustomTabBar.AbaAtual = "agenda";
    }

    private void OnDiaSelecionado(object sender, SelectionChangedEventArgs e)
    {
    }

    private void OnEditarClicked(object sender, EventArgs e)
    {
    }

    private void OnConcluirClicked(object sender, EventArgs e)
    {
    }

    private void OnCancelarClicked(object sender, EventArgs e)
    {
    }

    private void OnExcluirClicked(object sender, EventArgs e)
    {
    }

    private void OnNovoAgendamentoClicked(object sender, EventArgs e)
    {
    }
}