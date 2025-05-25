// Data/ApplicationDbContext.cs (lub jakkolwiek nazywa się twój DbContext)
using Microsoft.EntityFrameworkCore;
using tourist_map_backend.Entities;

namespace tourist_map_backend.Data // Upewnij się, że namespace jest poprawny
{
    public class ApplicationDbContext : DbContext // Lub IdentityDbContext jeśli używasz Identity
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}