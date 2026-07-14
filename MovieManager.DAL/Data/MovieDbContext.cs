using Microsoft.EntityFrameworkCore;
using MovieManager.DAL.Entities;

namespace MovieManager.DAL.Data
{
    public class MovieDbContext : DbContext
    {
        public MovieDbContext(DbContextOptions<MovieDbContext> options) : base(options)
        {
        }

        public DbSet<Movie> Movies              { get; set; }
        public DbSet<Genre> Genres              { get; set; }
        public DbSet<Director> Directors        { get; set; }
        public DbSet<Actor> Actors              { get; set; }
        public DbSet<MovieActor> MovieActors    { get; set; }
        public DbSet<Review> Reviews            { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Chiave composta di MovieActor (tabella ponte della relazione molti-a-molti Movie <-> Actor)
            modelBuilder.Entity<MovieActor>()
                .HasKey(ma => new { ma.MovieId, ma.ActorId });

            // Relazione MovieActor -> Movie (molti-a-uno)
            modelBuilder.Entity<MovieActor>()
                .HasOne(ma => ma.Movie)
                .WithMany(m => m.MovieActors)
                .HasForeignKey(ma => ma.MovieId);

            // Relazione MovieActor -> Actor (molti-a-uno)
            modelBuilder.Entity<MovieActor>()
                .HasOne(ma => ma.Actor)
                .WithMany(a => a.MovieActors)
                .HasForeignKey(ma => ma.ActorId);

            // Lunghezza e obbligatorietà sui campi principali
            modelBuilder.Entity<Movie>()
                .Property(m => m.Title).IsRequired().HasMaxLength(200);

            modelBuilder.Entity<Genre>()
                .Property(g => g.Name).IsRequired().HasMaxLength(100);

            modelBuilder.Entity<Director>()
                .Property(d => d.FirstName).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<Director>()
                .Property(d => d.LastName).IsRequired().HasMaxLength(100);

            modelBuilder.Entity<Actor>()
                .Property(a => a.FirstName).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<Actor>()
                .Property(a => a.LastName).IsRequired().HasMaxLength(100);

            modelBuilder.Entity<Review>()
                .Property(r => r.ReviewerName).IsRequired().HasMaxLength(100);

            // Vincolo applicativo sul punteggio della recensione: da 1 a 10
            modelBuilder.Entity<Review>()
                .ToTable(t => t.HasCheckConstraint("CK_Review_Score", "[Score] >= 1 AND [Score] <= 10"));

            base.OnModelCreating(modelBuilder);
        }
    }
}
