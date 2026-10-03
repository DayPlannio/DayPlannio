using DayPlannio.Api.Models;
using MongoDB.Driver;

namespace DayPlannio.Api.Services
{
    public class LogService
    {
        private readonly ContextMongodb _context;

        public LogService()
        {
            _context = new ContextMongodb();
        }

        public async Task RegistrarAsync(string tipo, string descricao, string origem, string? usuarioId = null, string? usuarioNome = null, string? email = null)
        {
            if (usuarioId != null && (usuarioNome == null || email == null) && Guid.TryParse(usuarioId, out var uid))
            {
                var usuario = await _context.ApplicationUsers
                    .Find(u => u.Id == uid)
                    .FirstOrDefaultAsync();

                usuarioNome ??= usuario?.NomeCompleto;
                email ??= usuario?.Email;
            }

            var log = new Log
            {
                Id = Guid.NewGuid(),
                Data = DateTime.UtcNow,
                Tipo = tipo,
                Descricao = descricao,
                Origem = origem,
                UsuarioId = usuarioId,
                UsuarioNome = usuarioNome,
                Email = email
            };

            await _context.Log.InsertOneAsync(log);
        }
    }
}