using System.ComponentModel.DataAnnotations;
using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;

namespace DayPlannio.Api.Models
{
    [CollectionName("User")]
    public class ApplicationUser : MongoIdentityUser
    {
        [Display(Name = "Nome Completo")]
        public string? NomeCompleto { get; set; }

        [Phone]
        public string? Telefone { get; set; }

        [Display(Name = "Cidade")]
        public string? Cidade { get; set; }

        public bool CidadeVisivel { get; set; }
        public bool TelefoneVisivel { get; set; }

        public string? CodigoRecuperacao { get; set; }
        public DateTime? CodigoRecuperacaoExpira { get; set; }
        public int TentativasCodigo { get; set; }
        public bool CodigoBloqueado { get; set; }

        public string? Plano { get; set; }
        public bool PlanoAtivo { get; set; }
        public DateTime? PlanoExpiraEm { get; set; }
        public string? PlanoOrigem { get; set; }
        public string? PlanoPendente { get; set; }
        public DateTime? PlanoSolicitadoEm { get; set; }
        public bool RenovacaoAutomatica { get; set; }
        public DateTime? PlanoCanceladoEm { get; set; }

        public DateTime? CriadoEm { get; set; }
    }
}