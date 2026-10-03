using System;
using System.Collections.Generic;
using System.Text;

namespace DayPlannio.App.ViewModel
{
    public class AgendamentoItemViewModel
    {
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
        public bool PodeCancelar { get; set; }
        public bool PodeConcluir { get; set; }

    public string? EnderecoAtendimento { get; set; }
    public DateTime? Inicio { get; set; }
        public DateTime? Fim { get; set; }
        public double? DuracaoMinutos { get; set; }

        public bool TemObservacoes =>
            !string.IsNullOrWhiteSpace(Observacoes);

        public bool TemTempo => !string.IsNullOrWhiteSpace(TempoTexto);

        public string TempoTexto
        {
            get
            {
                if (Inicio.HasValue && DuracaoMinutos.HasValue)
                    return $"Início {Inicio.Value.ToLocalTime():HH:mm} • Duração {FormatarDuracao(DuracaoMinutos.Value)}";

                if (Inicio.HasValue)
                    return $"Início {Inicio.Value.ToLocalTime():HH:mm} • Em andamento";

                return string.Empty;
            }
        }

        private static string FormatarDuracao(double minutos)
        {
            var total = (int)Math.Round(minutos);

            if (total < 60)
                return $"{total}min";

            var horas = total / 60;
            var restantes = total % 60;

            return restantes > 0 ? $"{horas}h {restantes}min" : $"{horas}h";
        }

        public bool PodeEditar => StatusTexto == "AGENDADO";
    }
}
