using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace DayPlannio.App.ViewModel
{
    public class AgendamentoItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public DateTime? InicioPausa { get; set; }
        public double PausaAcumuladaMinutos { get; set; }

        public void AtualizarRelogio()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TempoTexto)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TemTempo)));
        }

        public string Id { get; set; }
        public string Hora { get; set; }
        public string Servico { get; set; }
        public string Cliente { get; set; }
        public string Endereco { get; set; }
        public string Telefone { get; set; }
        public string StatusTexto { get; set; }
        public Color CorStatus { get; set; }
        public Color CorFundoStatus { get; set; }
        public string ClienteId { get; set; }
        public string TipoServicoId { get; set; }
        public DateTime DataHora { get; set; }
        public decimal ValorCobrado { get; set; }
        public decimal CustoMaterial { get; set; }
        public string Observacoes { get; set; }
        public bool EmAtendimento => StatusTexto == "EM ATENDIMENTO";
        public bool EmPausa => StatusTexto == "EM PAUSA";

        public Color CorFundoCard => EmAtendimento ? Color.FromArgb("#FFF6E8") : Color.FromArgb("#FAFAFA");
        public double EspessuraBorda => EmAtendimento ? 1.5 : 0;

        public bool PodeCancelar { get; set; }
        public bool PodeConcluir { get; set; }

    public string? EnderecoAtendimento { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? Inicio { get; set; }
        public DateTime? Fim { get; set; }
        public double? DuracaoMinutos { get; set; }

        public bool TemObservacoes =>
            !string.IsNullOrWhiteSpace(Observacoes);

        public bool TemTempo => !string.IsNullOrWhiteSpace(TempoTexto);

        public DateTime? ConfirmacaoChegadaEm { get; set; }
        public DateTime? PausaConcluiEm { get; set; }

        public bool PrecisaRelogio =>
            EmAtendimento
            || (EmPausa && PausaConcluiEm.HasValue)
            || (StatusTexto == "AGENDADO" && ConfirmacaoChegadaEm.HasValue);

        public string TempoTexto
        {
            get
            {
                if (Inicio.HasValue && DuracaoMinutos.HasValue)
                {
                    var fim = Fim.HasValue ? $" • Término {Fim.Value.ToLocalTime():HH:mm}" : string.Empty;
                    return $"Início {Inicio.Value.ToLocalTime():HH:mm}{fim} • Duração {FormatarDuracao(DuracaoMinutos.Value)}";
                }

                if (Inicio.HasValue)
                {
                    var inicio = DateTime.SpecifyKind(Inicio.Value, DateTimeKind.Utc);

                    var referencia = EmPausa && InicioPausa.HasValue
                        ? DateTime.SpecifyKind(InicioPausa.Value, DateTimeKind.Utc)
                        : DateTime.UtcNow;

                    var trabalhado = referencia - inicio - TimeSpan.FromMinutes(PausaAcumuladaMinutos);
                    if (trabalhado < TimeSpan.Zero) trabalhado = TimeSpan.Zero;

                    if (!EmPausa)
                        return $"Início {inicio.ToLocalTime():HH:mm} • Em andamento {FormatarRelogio(trabalhado)}";

                    var texto = $"Início {inicio.ToLocalTime():HH:mm} • Em pausa\nTempo trabalhado {FormatarRelogio(trabalhado)}";

                    if (PausaConcluiEm.HasValue)
                    {
                        var resta = DateTime.SpecifyKind(PausaConcluiEm.Value, DateTimeKind.Utc) - DateTime.UtcNow;
                        texto += resta > TimeSpan.Zero
                            ? $" • conclui em {FormatarContagem(resta)} se não voltar"
                            : " • concluindo...";
                    }

                    return texto;
                }

                if (StatusTexto == "AGENDADO" && ConfirmacaoChegadaEm.HasValue)
                {
                    var resta = DateTime.SpecifyKind(ConfirmacaoChegadaEm.Value, DateTimeKind.Utc) - DateTime.UtcNow;
                    return resta > TimeSpan.Zero
                        ? $"Chegada detectada • iniciando em {FormatarContagem(resta)}"
                        : "Chegada confirmada • iniciando...";
                }

                return string.Empty;
            }
        }

        private static string FormatarContagem(TimeSpan t) =>
            $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";

        private static string FormatarRelogio(TimeSpan t) =>
            $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

        private static string FormatarDuracao(double minutos)
        {
            var total = (int)Math.Round(minutos);

            if (total < 1)
                return "menos de 1 min";

            if (total < 60)
                return $"{total}min";

            var horas = total / 60;
            var restantes = total % 60;

            return restantes > 0 ? $"{horas}h {restantes}min" : $"{horas}h";
        }

        public bool PodeEditar => StatusTexto == "AGENDADO";
    }
}
