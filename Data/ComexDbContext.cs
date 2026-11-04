using Microsoft.EntityFrameworkCore;
using ComexTool.Models;

namespace ComexTool.Data
{
    public class ComexDbContext : DbContext
    {
        public ComexDbContext(DbContextOptions<ComexDbContext> options) : base(options) { }

        public DbSet<Document> Documents { get; set; }
    }
}
