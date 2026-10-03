using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class AdicionarFoto : ContentPage
{
    public AdicionarFotoViewModel ViewModel { get; }

    public AdicionarFoto(string agendamentoId, string tipoServico)
    {
        InitializeComponent();

        ViewModel = new AdicionarFotoViewModel(Navigation, agendamentoId, tipoServico);
        BindingContext = ViewModel;
    }
}