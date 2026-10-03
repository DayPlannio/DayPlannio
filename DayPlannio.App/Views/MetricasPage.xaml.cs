using DayPlannio.App.Helpers;
using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class MetricasPage : ContentPage
{
    private MetricasViewModel _viewModel;

    public MetricasPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel == null)
        {
            _viewModel = new MetricasViewModel(Navigation);
            BindingContext = _viewModel;

            var donut = new DonutChartDrawable(_viewModel);

            DonutChart.Drawable = donut;

            _viewModel.DadosAtualizados += () =>
            {
                DonutChart.Invalidate();
            };
        }

        _viewModel.InicializarCommand.ExecuteAsync(null);
    }
}
