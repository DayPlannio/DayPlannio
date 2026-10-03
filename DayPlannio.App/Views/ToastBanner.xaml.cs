namespace DayPlannio.App.Views;

public partial class ToastBanner : ContentView
{
    private CancellationTokenSource? _cts;

    public ToastBanner()
    {
        InitializeComponent();
    }

    public async Task ShowAsync(string titulo, string mensagem, bool sucesso, int duracaoMs = 3500)
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;

        TituloLabel.Text = titulo;
        MensagemLabel.Text = mensagem;
        IconLabel.Text = sucesso ? "\u2713" : "\u2715";
        CardBorder.BackgroundColor = sucesso
            ? Color.FromArgb("#2E7D32")
            : Color.FromArgb("#BA1A1A");

        CardBorder.AbortAnimation("FadeTo");
        CardBorder.AbortAnimation("TranslateTo");
        CardBorder.TranslationY = -40;
        CardBorder.Opacity = 0;

        try
        {
            await Task.WhenAll(
                CardBorder.FadeTo(1, 220, Easing.CubicOut),
                CardBorder.TranslateTo(0, 0, 220, Easing.CubicOut));

            await Task.Delay(duracaoMs, cts.Token);

            if (cts.Token.IsCancellationRequested) return;

            await Task.WhenAll(
                CardBorder.FadeTo(0, 200, Easing.CubicIn),
                CardBorder.TranslateTo(0, -40, 200, Easing.CubicIn));
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async void OnTapped(object? sender, EventArgs e)
    {
        _cts?.Cancel();

        await Task.WhenAll(
            CardBorder.FadeTo(0, 150),
            CardBorder.TranslateTo(0, -40, 150));
    }
}
