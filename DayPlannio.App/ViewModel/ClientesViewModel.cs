using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Models;
using DayPlannio.App.Services;
using DayPlannio.App.Views;
using System.Collections.ObjectModel;

namespace DayPlannio.App.ViewModels;

public partial class ClientesViewModel : ObservableObject
{
    private readonly Page _page;
    private readonly INavigation _navigation;
    private readonly string _userId;

    private List<Cliente> _todosClientes = new();

    public ClientesViewModel(
        Page page,
        INavigation navigation)
    {
        _page = page;
        _navigation = navigation;

        _userId =
            Preferences.Get(
                "userId",
                string.Empty);
    }

    [ObservableProperty]
    private ObservableCollection<Cliente> clientes = new();

    [ObservableProperty]
    private string busca;

    partial void OnBuscaChanged(string value)
    {
        var termo = value?.ToLower() ?? "";

        var filtrados = _todosClientes.Where(c =>
            c.Nome.ToLower().Contains(termo) ||
            (c.Telefone?.ToLower().Contains(termo) ?? false)
        ).ToList();

        Clientes =
            new ObservableCollection<Cliente>(
                filtrados);
    }

    public async Task CarregarClientes()
    {
        try
        {
            _todosClientes =
                await ClienteService.GetClientes(_userId)
                ?? new();

            var podeGerarCredenciais =
                await PlanoAppService.PermiteAsync(PlanoAppService.Full);

            foreach (var cliente in _todosClientes)
            {
                cliente.PodeGerarCredenciais =
                    podeGerarCredenciais && !cliente.TemAcessoWeb;

                var historico =
                    await AgendamentoService.GetHistorico(cliente.Id)
                    ?? new();

                var ultimaVisita = historico
                    .Where(a =>
                        a.Status?.Trim().ToLower() is "concluido" or "concluído")
                    .OrderByDescending(a => a.DataHora)
                    .FirstOrDefault();

                cliente.UltimaVisitaTexto =
                     ultimaVisita != null
                         ? $"Última Visita: {DateTime.SpecifyKind(ultimaVisita.DataHora, DateTimeKind.Utc).ToLocalTime():dd/MM/yyyy}"
                         : "Nenhuma visita concluída";
            }

            Clientes =
                new ObservableCollection<Cliente>(
                    _todosClientes);
        }
        catch (Exception ex)
        {
            await _page.DisplayAlertAsync(
                "Erro",
                ex.Message,
                "OK");
        }
    }

    [RelayCommand]
    private async Task NovoCliente()
    {
        await _navigation.PushAsync(
            new CadastrarCliente());
    }

    [RelayCommand]
    private async Task Editar(Cliente cliente)
    {
        await _navigation.PushAsync(
            new EditarCliente(
                cliente.Id,
                cliente.Nome,
                cliente.Telefone ?? "",
                cliente.Endereco ?? "",
                cliente.Observacoes ?? ""));
    }

    [RelayCommand]
    private async Task Deletar(Cliente cliente)
    {
        try
        {
            var popup =
                new ExcluirCliente(cliente.Nome);

            await _navigation.PushModalAsync(popup);

            await Task.Delay(100);

            while (_navigation.ModalStack.Contains(popup))
                await Task.Delay(100);

            if (popup.Confirmado)
            {
                var resultado = await ClienteService.Delete(cliente.Id);

                if (resultado.sucesso)
                {
                    await _page.DisplayAlertAsync(
                        "Sucesso",
                        "Cliente deletado com sucesso!",
                        "OK");

                    await CarregarClientes();
                }
                else
                {
                    throw new Exception("Erro ao deletar cliente.");
                }
            }
        }
        catch (Exception ex)
        {
            await _page.DisplayAlertAsync(
                "Erro",
                ex.Message,
                "OK");
        }
    }

    [RelayCommand]
    private async Task Historico(Cliente cliente)
    {
        await _navigation.PushAsync(
            new HistoricoCliente(
                cliente.Id,
                cliente.Nome));
    }

    [RelayCommand]
    private async Task GerarCredenciais(Cliente cliente)
    {
        try
        {
            if (!await PlanoAppService.ExigirPlanoAsync(PlanoAppService.Full))
                return;

            var confirmar = await _page.DisplayAlertAsync(
                "Gerar email e senha",
                $"Será criado um e-mail e uma senha provisória para {cliente.Nome} acessar o portal web. Deseja continuar?",
                "Sim", "Não");

            if (!confirmar) return;

            var resultado = await ClienteService.GerarCredenciais(cliente.Id);

            if (!resultado.sucesso)
                throw new Exception(string.IsNullOrWhiteSpace(resultado.erro)
                    ? "Não foi possível gerar o acesso do cliente."
                    : resultado.erro);

            await CompartilharAcessoAsync(cliente, resultado.email, resultado.senha);

            await CarregarClientes();
        }
        catch (Exception ex)
        {
            await _page.DisplayAlertAsync(
                "Erro",
                ex.Message,
                "OK");
        }
    }

    // Preencha com o endereço do portal web do cliente. Se ficar vazio, o link não entra na mensagem.
    private const string PortalUrl = "";

    private async Task CompartilharAcessoAsync(Cliente cliente, string email, string senha)
    {
        var mensagem = MontarMensagemAcesso(cliente.Nome, email, senha);

        // A senha só aparece agora, então o menu reabre até o prestador tocar em "Fechar".
        while (true)
        {
            var opcao = await _page.DisplayActionSheetAsync(
                $"Acesso gerado para {cliente.Nome}",
                "Fechar",
                null,
                "Enviar por WhatsApp",
                "Copiar mensagem");

            if (opcao == "Enviar por WhatsApp")
            {
                await AbrirWhatsAppAsync(cliente.Telefone, mensagem);
            }
            else if (opcao == "Copiar mensagem")
            {
                await Clipboard.Default.SetTextAsync(mensagem);
                await _page.DisplayAlertAsync("Copiado", "Mensagem copiada. É só colar na conversa com o cliente.", "OK");
            }
            else
            {
                return;
            }
        }
    }

    private static string MontarMensagemAcesso(string nome, string email, string senha)
    {
        var primeiroNome = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;
        var linhas = new List<string>
        {
            $"Olá, {primeiroNome}! Seu acesso ao portal DayPlannio foi criado.",
            string.Empty
        };

        if (!string.IsNullOrWhiteSpace(PortalUrl))
            linhas.Add($"Link: {PortalUrl}");

        linhas.Add($"E-mail: {email}");
        linhas.Add($"Senha provisória: {senha}");
        linhas.Add(string.Empty);
        linhas.Add("No primeiro acesso você precisará criar uma nova senha.");

        return string.Join("\n", linhas);
    }

    private async Task AbrirWhatsAppAsync(string? telefone, string mensagem)
    {
        var numero = new string((telefone ?? string.Empty).Where(char.IsDigit).ToArray());

        // Telefone brasileiro sem DDI (10 ou 11 dígitos): acrescenta 55.
        if (numero.Length is 10 or 11)
            numero = "55" + numero;

        var texto = Uri.EscapeDataString(mensagem);

        // Sem telefone válido, o WhatsApp abre para o prestador escolher o contato.
        var url = numero.Length >= 12
            ? $"https://wa.me/{numero}?text={texto}"
            : $"https://wa.me/?text={texto}";

        try
        {
            await Launcher.OpenAsync(new Uri(url));
        }
        catch
        {
            await _page.DisplayAlertAsync(
                "WhatsApp",
                "Não foi possível abrir o WhatsApp. Use \"Copiar mensagem\" e cole na conversa.",
                "OK");
        }
    }
}