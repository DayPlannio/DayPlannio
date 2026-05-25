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

        public bool TemObservacoes =>
            !string.IsNullOrWhiteSpace(Observacoes);
        public bool PodeEditar => StatusTexto == "AGENDADO";
    }
}
