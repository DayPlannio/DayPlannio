using DayPlannio.Api.Models;

namespace DayPlannio.Api.Services
{
    public class PlanoRenovacaoService : BackgroundService
    {
        private readonly ContextMongodb _contexto;
        private readonly ILogger<PlanoRenovacaoService> _logger;

        public PlanoRenovacaoService(
            ContextMongodb contexto,
            ILogger<PlanoRenovacaoService> logger)
        {
            _contexto = contexto;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var renovados = await PlanoService.RenovarTodosAsync(_contexto);

                    if (renovados > 0)
                        _logger.LogInformation("Renovação automática: {Quantidade} assinatura(s) renovada(s).", renovados);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao renovar assinaturas automaticamente.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
