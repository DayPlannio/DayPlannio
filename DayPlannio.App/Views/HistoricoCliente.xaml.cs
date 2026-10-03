using System.Linq;
using DayPlannio.App.ViewModels;

namespace DayPlannio.App.Views;

public partial class HistoricoCliente : ContentPage
{
    private readonly HistoricoClienteViewModel _viewModel;

    public HistoricoCliente(string clienteId, string nomeCliente)
    {
        InitializeComponent();

        _viewModel = new HistoricoClienteViewModel(
            clienteId,
            nomeCliente,
            Navigation,
            this);

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.CarregarHistorico();
    }

    private void OnFotosHistoricoScrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        if (sender is not CollectionView collectionView) return;
        if (collectionView.Parent is not VerticalStackLayout container) return;

        var trackIndex = container.Children.IndexOf(collectionView) + 1;
        if (trackIndex >= container.Children.Count) return;
        if (container.Children[trackIndex] is not Grid track) return;
        if (track.Children.Count < 1 || track.Children[0] is not BoxView thumb) return;

        int totalItems = collectionView.ItemsSource?.Cast<object>().Count() ?? 0;
        int visibleCount = e.LastVisibleItemIndex - e.FirstVisibleItemIndex + 1;

        if (totalItems <= visibleCount || totalItems == 0)
        {
            track.IsVisible = false;
            return;
        }

        track.IsVisible = true;

        double trackWidth = track.Width;
        if (trackWidth <= 0) return;

        double thumbWidth = Math.Max(trackWidth * ((double)visibleCount / totalItems), 20);
        double maxIndex = totalItems - visibleCount;
        double ratio = maxIndex > 0 ? (double)e.FirstVisibleItemIndex / maxIndex : 0;

        thumb.WidthRequest = thumbWidth;
        thumb.TranslationX = (trackWidth - thumbWidth) * ratio;
    }
}