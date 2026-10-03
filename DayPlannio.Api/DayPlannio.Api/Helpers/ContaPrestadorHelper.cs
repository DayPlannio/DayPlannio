using System.Net;
using DayPlannio.Api.Models;
using DayPlannio.Api.Services;
using MongoDB.Driver;

namespace DayPlannio.Api.Helpers
{
    public static class ContaPrestadorHelper
    {
        public static async Task<List<Cliente>> ListarClientesAsync(ContextMongodb context, Guid prestadorId)
        {
            return await context.Cliente
                .Find(Builders<Cliente>.Filter.Eq(c => c.UsuarioId, prestadorId))
                .ToListAsync();
        }

        public static void AvisarClientesPorEmail(EmailService emailService, List<Cliente> clientes, string? prestadorNome)
        {
            var destinatarios = clientes
                .Where(c => !string.IsNullOrWhiteSpace(c.EmailSecundario))
                .Select(c => (email: c.EmailSecundario!.Trim(), nome: c.Nome))
                .ToList();

            if (destinatarios.Count == 0)
                return;

            var profissional = string.IsNullOrWhiteSpace(prestadorNome)
                ? "O profissional que atendia você"
                : $"O profissional <strong>{WebUtility.HtmlEncode(prestadorNome.Trim())}</strong>";

            _ = Task.Run(async () =>
            {
                foreach (var destinatario in destinatarios)
                {
                    try
                    {
                        var corpo = $@"
                        <h2>Seu acesso ao DayPlannio foi encerrado</h2>
                        <p>Olá, {WebUtility.HtmlEncode(destinatario.nome)}.</p>
                        <p>{profissional} encerrou a conta no DayPlannio. Por isso, o seu acesso ao portal do cliente também foi encerrado e os seus dados nesse cadastro foram removidos.</p>
                        <p>Se precisar de um novo atendimento, entre em contato com o profissional ou procure outro profissional que utilize o DayPlannio.</p>";

                        await emailService.SendEmailAsync(
                            destinatario.email,
                            "Seu acesso ao DayPlannio foi encerrado",
                            corpo);
                    }
                    catch
                    {
                    }
                }
            });
        }
    }
}
