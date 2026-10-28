using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DayPlannio.Api.Helpers
{
    public static class AcessoHelper
    {
        public static bool EhAdmin(this ClaimsPrincipal usuario) => usuario.IsInRole("Admin");

        public static Guid? ObterUsuarioId(this ClaimsPrincipal usuario)
        {
            var valor = usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(valor, out var id) ? (Guid?)id : null;
        }

        public static bool EhDonoOuAdmin(this ClaimsPrincipal usuario, Guid donoId)
        {
            if (usuario.EhAdmin())
                return true;

            var logado = usuario.ObterUsuarioId();
            return logado.HasValue && donoId != Guid.Empty && logado.Value == donoId;
        }

        public static bool EhDonoOuAdmin(this ClaimsPrincipal usuario, string? donoId)
        {
            if (usuario.EhAdmin())
                return true;

            return Guid.TryParse(donoId, out var id) && usuario.EhDonoOuAdmin(id);
        }

        public static IActionResult Negado() =>
            new ObjectResult(new { message = "Você não tem permissão para acessar ou alterar os dados de outro usuário." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
    }
}
