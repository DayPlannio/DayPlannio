using Microsoft.EntityFrameworkCore;

namespace DayPlannio.Api.Data
{
    public class DayPlannioContext : DbContext
    {
        public DayPlannioContext(DbContextOptions<DayPlannioContext> options)
          : base(options)
        {
        }
        public DbSet<DayPlannio.Api.Models.User> User { get; set; } = default!;
        public DbSet<DayPlannio.Api.Models.Agendamento> Agendamento { get; set; } = default!;
        public DbSet<DayPlannio.Api.Models.Cliente> Cliente { get; set; } = default!;
        public DbSet<DayPlannio.Api.Models.Financeiro> Financeiro { get; set; } = default!;
        public DbSet<DayPlannio.Api.Models.TipoServico> TipoServico { get; set; } = default!;
    }
}
