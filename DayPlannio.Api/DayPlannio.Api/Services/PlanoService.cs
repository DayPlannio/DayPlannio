using DayPlannio.Api.Models;
using MongoDB.Driver;

namespace DayPlannio.Api.Services
{
    public static class PlanoService
    {
        public const string Basico = "Basico";
        public const string Profissional = "Profissional";
        public const string Full = "Full";
        public const string SemPlano = "SemPlano";

        public const string MensagemPrestadorRemovido = "Este profissional não faz mais parte do DayPlannio.";

        public static async Task<ApplicationUser?> RenovarSeNecessarioAsync(Guid usuarioId, ContextMongodb contexto)
        {
            var usuario = await contexto.ApplicationUsers
                .Find(u => u.Id == usuarioId)
                .FirstOrDefaultAsync();

            return await RenovarSeNecessarioAsync(usuario, contexto);
        }

        public static async Task<ApplicationUser?> RenovarSeNecessarioAsync(ApplicationUser? usuario, ContextMongodb contexto)
        {
            if (usuario == null)
                return null;

            if (!usuario.RenovacaoAutomatica ||
                !usuario.PlanoAtivo ||
                string.IsNullOrWhiteSpace(usuario.Plano) ||
                string.Equals(usuario.PlanoOrigem, "trial", StringComparison.OrdinalIgnoreCase) ||
                !usuario.PlanoExpiraEm.HasValue ||
                usuario.PlanoExpiraEm.Value > DateTime.UtcNow)
            {
                return usuario;
            }

            usuario.PlanoExpiraEm = DateTime.UtcNow.AddDays(30);
            usuario.PlanoCanceladoEm = null;

            await contexto.ApplicationUsers.UpdateOneAsync(
                Builders<ApplicationUser>.Filter.Eq(u => u.Id, usuario.Id),
                Builders<ApplicationUser>.Update
                    .Set(u => u.PlanoExpiraEm, usuario.PlanoExpiraEm)
                    .Set(u => u.PlanoCanceladoEm, (DateTime?)null)
                    .Set(u => u.PlanoAtivo, true));

            return usuario;
        }

        public static async Task<int> RenovarTodosAsync(ContextMongodb contexto)
        {
            var usuarios = await contexto.ApplicationUsers
                .Find(u => u.RenovacaoAutomatica &&
                          u.PlanoAtivo &&
                          u.PlanoExpiraEm <= DateTime.UtcNow &&
                          u.PlanoOrigem != "trial")
                .ToListAsync();

            var renovados = 0;

            foreach (var usuario in usuarios)
            {
                await RenovarSeNecessarioAsync(usuario, contexto);
                renovados++;
            }

            return renovados;
        }

        public static string ObterPlanoEfetivo(ApplicationUser? usuario)
        {
            if (usuario == null ||
                string.IsNullOrWhiteSpace(usuario.Plano) ||
                !usuario.PlanoAtivo ||
                (usuario.PlanoExpiraEm.HasValue && usuario.PlanoExpiraEm.Value < DateTime.UtcNow))
            {
                return SemPlano;
            }

            return Normalizar(usuario.Plano);
        }

        public static async Task<string> ObterPlanoEfetivoAsync(Guid usuarioId, ContextMongodb contexto)
        {
            var usuario = await RenovarSeNecessarioAsync(usuarioId, contexto);

            return ObterPlanoEfetivo(usuario);
        }

        public static async Task<(bool autorizado, string? plano, string mensagem)> VerificarAsync(
            Guid usuarioId, string minimo, ContextMongodb contexto)
        {
            var plano = await ObterPlanoEfetivoAsync(usuarioId, contexto);
            var autorizado = Nivel(plano) >= Nivel(minimo);

            var mensagem = autorizado
                ? string.Empty
                : plano == SemPlano
                    ? $"Este recurso exige o plano {NomeExibicao(minimo)}. Você não tem nenhuma assinatura ativa."
                    : $"Este recurso exige o plano {NomeExibicao(minimo)}. Seu plano atual é {NomeExibicao(plano)}.";

            return (autorizado, plano, mensagem);
        }

        public static string Normalizar(string plano)
        {
            if (plano.Trim().Equals(Full, StringComparison.OrdinalIgnoreCase)) return Full;
            if (plano.Trim().Equals(Profissional, StringComparison.OrdinalIgnoreCase)) return Profissional;
            if (plano.Trim().Equals(SemPlano, StringComparison.OrdinalIgnoreCase)) return SemPlano;
            return Basico;
        }

        public static string NomeExibicao(string? plano)
        {
            if (string.IsNullOrWhiteSpace(plano)) return "Sem assinatura";

            return plano.Trim().Equals(SemPlano, StringComparison.OrdinalIgnoreCase)
                ? "Sem assinatura"
                : Normalizar(plano) switch
                {
                    Full => "Full",
                    Profissional => "Profissional",
                    _ => "Básico"
                };
        }

        public static int Nivel(string plano) => plano switch
        {
            Full => 3,
            Profissional => 2,
            Basico => 1,
            _ => 0
        };
    }
}