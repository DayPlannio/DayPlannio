namespace DayPlannio.App.Views;

public partial class EditarFinanceiro : ContentPage
{
    public EditarFinanceiro()
    {
        InitializeComponent();
        picker_.Items.Add("Entrada");
        picker_.Items.Add("Saída");
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {

    }

    private async void OnTipoChanged(object sender, EventArgs e)
    {
        var tipoSelecionado = picker_.SelectedItem?.ToString();

        lblTipo.Text = tipoSelecionado;
    }

    private async void OnDataChanged(object sender, EventArgs e)
    {

    }

    private async void OnHorarioChanged(object sender, EventArgs e)
    {

    }

    private async void OnConfirmarClicked(object sender, EventArgs e)
    {

    }

    private async void OnCancelarClicked(object sender, EventArgs e)
    {

    }
}