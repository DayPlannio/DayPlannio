using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using DayPlannio.Api.Services;
using DayPlannio.Api.Settings;
using Microsoft.Extensions.Options;

namespace DayPlannio.Api.IntegrationTests.Infra;

public sealed record EmailEnviado(string Para, string Assunto, string CorpoHtml);

public sealed class FakeEmailService : EmailService
{
    private static readonly Regex CodigoRegex = new(@"<h1[^>]*>(\d{6})</h1>", RegexOptions.Compiled);

    public ConcurrentQueue<EmailEnviado> Enviados { get; } = new();

    public FakeEmailService() : base(Options.Create(new EmailSettings())) { }

    public override Task SendEmailAsync(string toEmail, string subject, string message)
    {
        Enviados.Enqueue(new EmailEnviado(toEmail, subject, message));
        return Task.CompletedTask;
    }

    public string? UltimoCodigoPara(string email) =>
        Enviados
            .Where(e => string.Equals(e.Para, email, StringComparison.OrdinalIgnoreCase))
            .Select(e => CodigoRegex.Match(e.CorpoHtml))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .LastOrDefault();
}
