namespace DayPlannio.App.Services;

public static class NotificacaoMonitor
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(15);
    private static CancellationTokenSource? _cts;

    public static event Action<NotificacaoItem>? NotificacaoRecebida;

    public static void Iniciar()
    {
        if (_cts != null) return;

        _cts = new CancellationTokenSource();
        _ = ExecutarAsync(_cts.Token);
    }

    public static void Parar()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private static async Task ExecutarAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Intervalo);
        try
        {
            do
            {
                await VerificarAsync();
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { }
    }

    private static async Task VerificarAsync()
    {
        try
        {
            var userId = Preferences.Get("userId", string.Empty);
            if (string.IsNullOrEmpty(userId)) return;

            var novas = await NotificacaoService.BuscarNovas(userId);
            if (novas.Count == 0) return;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (novas.Any(n => n.Tipo is "plano_aprovado" or "plano_rejeitado"))
                    PlanoAppService.Invalidar();

                foreach (var nova in novas)
                {
                    try { NotificacaoRecebida?.Invoke(nova); }
                    catch { }
                }

                foreach (var nova in novas)
                    await ExibirAsync(nova);
            });
        }
        catch { }
    }

    private static async Task ExibirAsync(NotificacaoItem n)
    {
        var pagina = ObterPaginaAtual();
        if (pagina == null) return;

        var titulo = n.Tipo switch
        {
            "plano_aprovado" => "Plano aprovado! 🎉",
            "plano_rejeitado" => "Solicitação não aprovada",
            "foto_aprovada" => "Foto aprovada",
            "foto_rejeitada" => "Foto rejeitada",
            _ => "Cliente encerrou a conta"
        };

        var sucesso = n.Tipo is "plano_aprovado" or "foto_aprovada";

        var mensagem = string.IsNullOrWhiteSpace(n.Mensagem)
            ? (sucesso ? "Tudo certo!" : "Verifique os detalhes.")
            : n.Mensagem;

        var apenasToast = n.Tipo is "foto_aprovada" or "foto_rejeitada";

        if (apenasToast && pagina is IExibeToast host)
        {
            await host.MostrarToastAsync(titulo, mensagem, sucesso);
            return;
        }

        await pagina.DisplayAlertAsync(titulo, mensagem, "OK");
    }

    private static Page? ObterPaginaAtual()
    {
        var raiz = Application.Current?.Windows.FirstOrDefault()?.Page;

        if (raiz is NavigationPage nav)
        {
            var modal = nav.Navigation.ModalStack.LastOrDefault();
            if (modal != null)
                return modal is NavigationPage navModal ? navModal.CurrentPage : modal;

            return nav.CurrentPage;
        }

        return raiz;
    }
}
