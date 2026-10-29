using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class EditarCliente : ContentPage
{
    public EditarCliente(
        string clienteId,
        string nome,
        string telefone,
        string endereco,
        string observacoes,
        double? latitude = null,
        double? longitude = null,
        int raioGeofenceMetros = 100)
    {
        InitializeComponent();

        BindingContext = new EditarClienteViewModel(
            clienteId,
            nome,
            telefone,
            endereco,
            observacoes,
            latitude,
            longitude,
            raioGeofenceMetros);
    }
}