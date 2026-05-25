using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class EditarCliente : ContentPage
{
    public EditarCliente(
        string clienteId,
        string nome,
        string telefone,
        string endereco,
        string observacoes)
    {
        InitializeComponent();

        BindingContext = new EditarClienteViewModel(
            clienteId,
            nome,
            telefone,
            endereco,
            observacoes);
    }
}