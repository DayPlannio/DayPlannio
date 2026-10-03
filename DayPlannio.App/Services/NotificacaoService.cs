using System.Text.Json;

namespace DayPlannio.App.Services
{
    public class NotificacaoService
    {
        public static async Task<List<NotificacaoItem>?> GetNotificacoes(string usuarioId)
        {
            try
            {
                var response = await App.HttpClient.GetAsync($"api/admin/notificacoes/{usuarioId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<NotificacaoItem>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
            return null;
        }

        public static async Task<int> ContarNaoLidas(string usuarioId)
        {
            try
            {
                var response = await App.HttpClient.GetAsync($"api/admin/notificacoes/{usuarioId}/nao-lidas");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (result != null && result.TryGetValue("total", out var total))
                        return Convert.ToInt32(total);
                }
            }
            catch { }
            return 0;
        }

        public static async Task MarcarLida(string notificacaoId)
        {
            try
            {
                await App.HttpClient.PutAsync($"api/admin/notificacoes/{notificacaoId}/lida", null);
            }
            catch { }
        }

        public static readonly string[] TiposMonitorados =
        {
            "foto_aprovada",
            "foto_rejeitada",
            "plano_aprovado",
            "plano_rejeitado",
            "cliente_encerrou_conta"
        };

        public static async Task<List<NotificacaoItem>> BuscarNovas(string usuarioId)
        {
            var lista = await GetNotificacoes(usuarioId);
            if (lista == null) return new();

            var relevantes = lista
                .Where(n => TiposMonitorados.Contains(n.Tipo))
                .OrderBy(n => n.DataCriacao)
                .ToList();

            if (relevantes.Count == 0) return new();

            var chave = $"ultimaNotificacaoTicks_{usuarioId}";
            var ultimoTicks = Preferences.Get(chave, 0L);
            var maisRecenteTicks = relevantes.Max(n => n.DataCriacao.Ticks);

            var novas = ultimoTicks == 0
                ? relevantes.Where(n => !n.Lida).TakeLast(1).ToList()
                : relevantes.Where(n => n.DataCriacao.Ticks > ultimoTicks).TakeLast(3).ToList();

            Preferences.Set(chave, Math.Max(ultimoTicks, maisRecenteTicks));
            return novas;
        }
    }

    public class NotificacaoItem
    {
        public string Id { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Mensagem { get; set; } = "";
        public string Tipo { get; set; } = "";
        public bool Lida { get; set; }
        public DateTime DataCriacao { get; set; }

        public string TipoTexto => Tipo switch
        {
            "foto_enviada" => "Em análise",
            "foto_aprovada" => "Aprovada",
            "foto_rejeitada" => "Rejeitada",
            "plano_aprovado" => "Aprovado",
            "plano_rejeitado" => "Não aprovado",
            "cliente_encerrou_conta" => "Cliente saiu",
            _ => Tipo
        };

        public string StatusTexto => Lida ? "Lida" : "Nova";

        public string StatusCor => Lida ? "#aaa" : "#006260";
    }
}
