
using MateoApiAotCdk.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace MateoApiAotCdk.Entities.Contexts {
    public class MateoDbContext : DbContext {

        public MateoDbContext(DbContextOptions<MateoDbContext> options) : base(options) { }

        public DbSet<Entrenamiento> Entrenamientos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            modelBuilder.UseIdentityAlwaysColumns();
        }
    }
}
