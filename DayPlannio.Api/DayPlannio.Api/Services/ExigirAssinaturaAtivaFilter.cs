using System.Reflection;
using System.Text.Json;
using DayPlannio.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DayPlannio.Api.Services
{
    public class ExigirAssinaturaAtivaFilter : IAsyncActionFilter
    {
        private static readonly string[] ChavesRota = { "usuarioId", "prestadorId" };
        private static readonly string[] ChavesCorpo = { "usuarioId", "prestadorId", "userId" };

        private readonly ContextMongodb _contexto;

        public ExigirAssinaturaAtivaFilter(ContextMongodb contexto)
        {
            _contexto = contexto;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var controller = context.ActionDescriptor as ControllerActionDescriptor;
            var controllerType = controller?.ControllerTypeInfo.AsType();

            var anonimo = controllerType?.GetCustomAttribute<AllowAnonymousAttribute>() != null ||
                context.ActionDescriptor.EndpointMetadata?.OfType<AllowAnonymousAttribute>().Any() == true;

            if (anonimo || controllerType?.Name == "AdminController")
            {
                await next();
                return;
            }

            var usuarioId = await ObterUsuarioIdAsync(context);

            if (usuarioId == Guid.Empty)
            {
                await next();
                return;
            }

            var usuario = await PlanoService.RenovarSeNecessarioAsync(usuarioId, _contexto);

            if (usuario == null)
            {
                context.Result = new ObjectResult(new { message = PlanoService.MensagemPrestadorRemovido })
                {
                    StatusCode = StatusCodes.Status410Gone
                };
                return;
            }

            var plano = PlanoService.ObterPlanoEfetivo(usuario);

            if (plano != PlanoService.SemPlano)
            {
                await next();
                return;
            }

            context.Result = new ObjectResult(new
            {
                message = "Você não tem nenhuma assinatura ativa. Escolha um plano para voltar a usar o app."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        private static async Task<Guid> ObterUsuarioIdAsync(ActionExecutingContext context)
        {
            foreach (var chave in ChavesRota)
                if (context.RouteData.Values.TryGetValue(chave, out var valor) &&
                    Guid.TryParse(valor?.ToString(), out var daRota))
                    return daRota;

            foreach (var chave in ChavesCorpo)
                if (context.ActionArguments.TryGetValue(chave, out var argumento) &&
                    TryGuidFromObject(argumento, out var doCorpo))
                    return doCorpo;

            foreach (var argumento in context.ActionArguments.Values)
            {
                if (TryGuidFromProperties(argumento, out var dasPropriedades))
                    return dasPropriedades;
            }

            var query = context.HttpContext.Request.Query;
            foreach (var chave in ChavesCorpo)
                if (query.TryGetValue(chave, out var valorQuery) &&
                    Guid.TryParse(valorQuery.ToString(), out var daQuery))
                    return daQuery;

            return await ReadFromBodyAsync(context);
        }

        private static bool TryGuidFromObject(object? valor, out Guid guid)
        {
            guid = Guid.Empty;

            if (valor is null)
                return false;

            if (valor is Guid g)
            {
                guid = g;
                return true;
            }

            if (valor is string s && Guid.TryParse(s, out var gs))
            {
                guid = gs;
                return true;
            }

            return false;
        }

        private static bool TryGuidFromProperties(object? modelo, out Guid guid)
        {
            guid = Guid.Empty;

            if (modelo is null)
                return false;

            var tipo = modelo.GetType();

            if (tipo.IsPrimitive || modelo is string)
                return false;

            foreach (var propriedade in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!ChavesCorpo.Any(c => string.Equals(c, propriedade.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                object? valor;

                try
                {
                    valor = propriedade.GetValue(modelo);
                }
                catch
                {
                    continue;
                }

                if (TryGuidFromObject(valor, out guid))
                    return true;
            }

            return false;
        }

        private static async Task<Guid> ReadFromBodyAsync(ActionExecutingContext context)
        {
            var request = context.HttpContext.Request;

            if (!request.HasJsonContentType())
                return Guid.Empty;

            request.EnableBuffering();
            var posicao = request.Body.Position;
            request.Body.Position = 0;

            string corpo;

            using (var leitor = new StreamReader(request.Body, System.Text.Encoding.UTF8, leaveOpen: true))
                corpo = await leitor.ReadToEndAsync();

            request.Body.Position = posicao;

            if (string.IsNullOrWhiteSpace(corpo))
                return Guid.Empty;

            try
            {
                using var doc = JsonDocument.Parse(corpo);

                foreach (var propriedade in doc.RootElement.EnumerateObject())
                    foreach (var chave in ChavesCorpo)
                        if (string.Equals(propriedade.Name, chave, StringComparison.OrdinalIgnoreCase) &&
                            Guid.TryParse(propriedade.Value.ToString(), out var guid))
                            return guid;
            }
            catch (JsonException)
            {
                return Guid.Empty;
            }

            return Guid.Empty;
        }
    }
}
