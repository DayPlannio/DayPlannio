using DayPlannio.App.Models;
using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class EditarServico : ContentPage
{
    public EditarServico(Servico servico)
    {
        InitializeComponent();

        BindingContext = new EditarServicoViewModel(
            servico,
            Navigation,
            this);
    }
}