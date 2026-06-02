using Microsoft.EntityFrameworkCore;
using Xenopairings.Models;

namespace Xenopairings.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<Match> Matches => Set<Match>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tournament>(t =>
        {
            t.HasKey(x => x.Id);
            t.HasIndex(x => x.Slug).IsUnique();
            t.HasIndex(x => x.ManageToken).IsUnique();
            t.Property(x => x.OrganizerEmail)
                .HasConversion(v => v.ToLowerInvariant(), v => v);
        });

        modelBuilder.Entity<Player>(p =>
        {
            p.HasKey(x => x.Id);
            p.HasIndex(x => x.EditToken).IsUnique();
            p.HasIndex(x => x.TournamentId);
            p.Property(x => x.Email)
                .HasConversion(
                    v => v == null ? null : v.ToLowerInvariant(),
                    v => v);
            p.HasOne(x => x.Tournament)
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Round>(r =>
        {
            r.HasKey(x => x.Id);
            r.HasIndex(x => new { x.TournamentId, x.RoundNumber }).IsUnique();
            r.HasOne(x => x.Tournament)
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Match>(m =>
        {
            m.HasKey(x => x.Id);
            m.HasOne(x => x.Round)
                .WithMany()
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);
            m.HasOne(x => x.Player1)
                .WithMany()
                .HasForeignKey(x => x.Player1Id)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
            m.HasOne(x => x.Player2)
                .WithMany()
                .HasForeignKey(x => x.Player2Id)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });
    }
}
