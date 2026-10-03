using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DayPlannio.App.Services;
using System.Threading;

namespace DayPlannio.App.ViewModels;

public partial class ConfirmarCodigoViewModel : ObservableObject
{
    private readonly string _email;
    private readonly INavigation _navigation;

    private readonly CancellationTokenSource _cts = new();

    public ConfirmarCodigoViewModel(string email, INavigation navigation)
    {
        _email = email;
        _navigation = navigation;
        _ = IniciarContagemRegressiva();
    }

    private async Task IniciarContagemRegressiva()
    {
        for (int i = 60; i >= 1; i--)
        {
            TempoReenviar = i;
            ReenviarCommand.NotifyCanExecuteChanged();
            try
            {
                await Task.Delay(1000, _cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
        TempoReenviar = 0;
        ReenviarCommand.NotifyCanExecuteChanged();
    }

    public void Cancelar()
    {
        _cts.Cancel();
    }

    [ObservableProperty]
    private string codigo;

    [ObservableProperty]
    private string erroMensagem;

    [ObservableProperty]
    private bool erroVisivel;

    [ObservableProperty]
    private int tempoReenviar = 60;

    public bool PodeReenviar => TempoReenviar <= 0;

    public string TextoReenviar => TempoReenviar > 0
        ? $"Reenviar código ({TempoReenviar}s)"
        : "Reenviar código";

    partial void OnTempoReenviarChanged(int value)
    {
        OnPropertyChanged(nameof(PodeReenviar));
        OnPropertyChanged(nameof(TextoReenviar));
    }

    public string EmailMascarado => MascararEmail(_email);

    public static string MascararEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return email ?? "";

        email = email.Trim();
        int arroba = email.IndexOf('@');
        if (arroba <= 1)
            return email;

        var nome = email.Substring(0, arroba);
        var dominio = email.Substring(arroba + 1);

        if (nome.Length <= 2)
            return nome + "@" + dominio;

        var primeiro = nome.Substring(0, 2);
        var ultimo = nome.Substring(nome.Length - 1);
        var mascaraNome = primeiro + new string('*', nome.Length - 3) + ultimo;
        return mascaraNome + "@" + dominio;
    }

    [RelayCommand]
    private async Task EditarEmail()
    {
        await _navigation.PopAsync();
    }

    [RelayCommand]
    private async Task Voltar()
    {
        await _navigation.PopAsync();
    }

    [RelayCommand(CanExecute = nameof(CanReenviar))]
    private async Task Reenviar()
    {
        ErroVisivel = false;
        var (sucesso, mensagem) = await UsuarioService.EnviarRedefinicaoSenha(_email);
        if (sucesso)
        {
            ErroMensagem = string.IsNullOrWhiteSpace(mensagem) ? "Código reenviado." : mensagem;
            ErroVisivel = true;
        }
        else
        {
            ErroMensagem = string.IsNullOrWhiteSpace(mensagem) ? "Não foi possível reenviar o código." : mensagem;
            ErroVisivel = true;
        }
    }

    private bool CanReenviar() => PodeReenviar;

    [RelayCommand]
    private async Task Confirmar()
    {
        try
        {
            ErroVisivel = false;

            if (string.IsNullOrWhiteSpace(Codigo) || Codigo.Length < 6)
                throw new Exception("Digite o código de 6 dígitos.");

            bool valido;
            string mensagem = "";
            try
            {
                (valido, mensagem) = await UsuarioService.VerificarCodigo(_email, Codigo);
            }
            catch
            {
                throw new Exception("Código inválido ou expirado.");
            }

            if (valido)
            {
                await _navigation.PushAsync(
                    new Views.NovaSenha(_email, Codigo));
            }
            else
            {
                throw new Exception(string.IsNullOrWhiteSpace(mensagem) ? "Código inválido ou expirado." : mensagem);
            }
        }
        catch (Exception ex)
        {
            ErroMensagem = ex.Message;
            ErroVisivel = true;
        }
    }
}