using Microsoft.EntityFrameworkCore;
using ComexTool.Models;

namespace ComexTool.Data
{
    public class ComexDbContext : DbContext
    {
        public ComexDbContext(DbContextOptions<ComexDbContext> options) : base(options) { }

        public DbSet<ProcessoComex> Processos { get; set; }
        public DbSet<Document> Documents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ProcessoComex>()
                .HasMany(p => p.Documentos)
                .WithOne(d => d.Processo)
                .HasForeignKey(d => d.ProcessoId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
