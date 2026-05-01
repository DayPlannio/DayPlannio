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
    }
}