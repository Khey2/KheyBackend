using KheyBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace KheyBackend.DBContext
{
    public class AppDbContext: DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options ) : base(options)
        {
        }

        // Aquí agregarás tus tablas más adelante como DbSet, por ejemplo:
        // public DbSet<User> Users { get; set; }

        // Tabla de usuarios de la pagina
        public DbSet<User> Users { get; set; }
        public DbSet<RevokedToken> RevokedTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RevokedToken>()
                .HasIndex(r => r.Jti)
                .IsUnique();
        }

    }


}
