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

        public DbSet<User> Users { get; set; } // Już powinieneś to mieć
        public DbSet<Comment> Comments { get; set; } // Dodaj to
        public DbSet<Point> Points { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Konfiguracja relacji, jeśli potrzebna (np. onDelete Cascade)
            // Przykład:
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany() // Jeśli User nie ma kolekcji Comments, użyj WithMany() bez parametru
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Lub Cascade, jeśli chcesz usuwać komentarze po usunięciu użytkownika

            modelBuilder.Entity<Point>()
                .HasOne(p => p.User)
                .WithMany() // Jeśli User nie ma kolekcji Points, użyj WithMany() bez parametru
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict); //
        }
    }
}