using DayPlannio.App.Models;

namespace DayPlannio.App.Services;

public static class PlanoAppService
{
    public const string Basico = "Básico";
    public const string Profissional = "Profissional";
    public const string Full = "Full";
    public const string SemPlano = "SemPlano";

    private const string _cacheKey = "planoAtual";
    private const int _cacheMinutos = 1;

    private static string? _plano;
    private static DateTime? _ultimaChecagem;

    public static void Invalidar()
    {
        _plano = null;
        _ultimaChecagem = null;
        Preferences.Remove(_cacheKey);
    }

    public sealed record StatusTesteGratuito(bool Expirado, DateTime? ExpirouEm);

    public static async Task<StatusTesteGratuito> ObterStatusTesteGratuitoAsync()
    {
        var userId = Preferences.Get("userId", string.Empty);
        if (string.IsNullOrWhiteSpace(userId))
            return new StatusTesteGratuito(false, null);

        try
        {
            var perfil = await UsuarioService.GetPerfil(userId);

            if (perfil == null ||
                !string.Equals(perfil.PlanoOrigem?.Trim(), "trial", StringComparison.OrdinalIgnoreCase) ||
                !perfil.PlanoExpiraEm.HasValue)
                return new StatusTesteGratuito(false, null);

            var expiraLocal = DateTime.SpecifyKind(perfil.PlanoExpiraEm.Value, DateTimeKind.Utc).ToLocalTime();

            return expiraLocal <= DateTime.Now
                ? new StatusTesteGratuito(true, expiraLocal)
                : new StatusTesteGratuito(false, expiraLocal);
        }
        catch
        {
            return new StatusTesteGratuito(false, null);
        }
    }

    public static void MarcarAvisoTrialMostrado()
        => Preferences.Set("avisoTrialMostrado", DateTime.Now.ToString("O"));

    public static void LimparAvisoTrial()
        => Preferences.Remove("avisoTrialMostrado");

    public static async Task<string> ObterPlanoAsync()
    {
        var userId = Preferences.Get("userId", string.Empty);
        if (string.IsNullOrWhiteSpace(userId))
            return SemPlano;

        if (!string.IsNullOrWhiteSpace(_plano) &&
            _ultimaChecagem.HasValue &&
            (DateTime.UtcNow - _ultimaChecagem.Value).TotalMinutes < _cacheMinutos)
            return _plano;

        var plano = SemPlano;
        try
        {
            var perfil = await UsuarioService.GetPerfil(userId);
            plano = PlanoEfetivo(perfil);
        }
        catch
        {
            plano = Preferences.Get(_cacheKey, SemPlano);
        }

        _plano = plano;
        _ultimaChecagem = DateTime.UtcNow;
        Preferences.Set(_cacheKey, plano);
        return plano;
    }

    public static async Task<bool> PermiteAsync(string minimo)
    {
        var plano = await ObterPlanoAsync();
        return Nivel(plano) >= Nivel(minimo);
    }

    public static async Task<bool> ExigirPlanoAsync(string minimo)
    {
        if (await PermiteAsync(minimo))
            return true;

        if (!TemAssinaturaAtiva(await ObterPlanoAsync()))
        {
            await ExigirAssinaturaAtivaAsync();
            return false;
        }

        var planoAtual = NomeExibicao(await ObterPlanoAsync());
        var planoNecessario = NomeExibicaoMinimo(minimo);

        var page = Application.Current?.Windows[0]?.Page;
        if (page != null)
        {
            await page.DisplayAlertAsync(
                "Recurso bloqueado",
                $"Este recurso exige o plano {planoNecessario}.\nSeu plano atual é {planoAtual}.\n\nPara liberar, abra a aba Perfil e toque em \"Escolher plano\".",
                "OK");
        }

        return false;
    }

    public static int Nivel(string plano)
    {
        var normalizado = Normalizar(plano);
        return normalizado switch
        {
            Full => 3,
            Profissional => 2,
            Basico => 1,
            _ => 0
        };
    }

    public static string Normalizar(string? plano)
    {
        if (string.Equals(plano?.Trim(), Full, StringComparison.OrdinalIgnoreCase))
            return Full;
        if (string.Equals(plano?.Trim(), Profissional, StringComparison.OrdinalIgnoreCase))
            return Profissional;
        if (string.Equals(plano?.Trim(), SemPlano, StringComparison.OrdinalIgnoreCase))
            return SemPlano;
        return Basico;
    }

    public static string NomeExibicao(string plano)
    {
        if (string.IsNullOrWhiteSpace(plano) ||
            string.Equals(plano.Trim(), SemPlano, StringComparison.OrdinalIgnoreCase))
            return "Sem assinatura";

        return Normalizar(plano) switch
        {
            Full => "Full",
            Profissional => "Profissional",
            _ => "Básico"
        };
    }

    public static string NomeExibicaoMinimo(string minimo) => NomeExibicao(minimo);

    public static string PrecoMensal(string plano) => Normalizar(plano) switch
    {
        Full => "R$ 29,90/mês",
        Profissional => "R$ 19,90/mês",
        _ => "R$ 9,90/mês"
    };

    public static string PlanoEfetivo(Usuario? perfil)
    {
        var plano = Normalizar(perfil?.Plano);

        if (string.IsNullOrWhiteSpace(perfil?.Plano) ||
            perfil?.PlanoAtivo == false ||
            (perfil!.PlanoExpiraEm.HasValue && perfil.PlanoExpiraEm.Value < DateTime.UtcNow))
            return SemPlano;

        return plano;
    }

    public static bool TemAssinaturaAtiva(string? plano) =>
        !string.Equals(Normalizar(plano), SemPlano, StringComparison.OrdinalIgnoreCase) &&
        Nivel(plano) > 0;

    public static async Task<bool> ExigirAssinaturaAtivaAsync()
    {
        if (TemAssinaturaAtiva(await ObterPlanoAsync()))
            return true;

        var page = Application.Current?.Windows[0]?.Page;
        if (page == null)
            return false;

        var verPlanos = await page.DisplayAlertAsync(
            "Sem assinatura ativa",
            "Seu acesso ao DayPlannio está pausado porque você não tem nenhum plano ativo.\n\nEscolha um plano para voltar a usar o app.",
            "Ver planos",
            "Agora não");

        if (verPlanos && page.Navigation != null)
            await page.Navigation.PushAsync(new Views.Planos());

        return false;
    }
}