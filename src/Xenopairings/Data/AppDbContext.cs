using Microsoft.EntityFrameworkCore;
using Xenopairings.Models;

namespace Xenopairings.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMatchup> TeamMatchups => Set<TeamMatchup>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<PlayerRating> PlayerRatings => Set<PlayerRating>();
    public DbSet<PlayerRatingHistory> PlayerRatingHistories => Set<PlayerRatingHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(u =>
        {
            u.HasKey(x => x.Id);
            u.HasIndex(x => x.Email).IsUnique();
            u.Property(x => x.Email)
                .HasConversion(v => v.ToLowerInvariant(), v => v);
        });

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

        modelBuilder.Entity<Organization>(o =>
        {
            o.HasKey(x => x.Id);
            o.HasIndex(x => x.InviteToken).IsUnique();
        });

        modelBuilder.Entity<OrganizationMember>(m =>
        {
            m.HasKey(x => x.Id);
            m.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
            m.HasOne(x => x.Organization)
                .WithMany(o => o.Members)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            m.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Player>(p =>
        {
            p.HasOne(x => x.Organization)
                .WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
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
            m.HasOne(x => x.TeamMatchup)
                .WithMany()
                .HasForeignKey(x => x.TeamMatchupId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);
        });

        modelBuilder.Entity<Team>(t =>
        {
            t.HasKey(x => x.Id);
            t.HasIndex(x => x.InviteToken).IsUnique();
            t.HasOne(x => x.Tournament)
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Player>(p =>
        {
            // TeamId FK — optional, SetNull on team delete
            p.HasOne(x => x.Team)
                .WithMany(t => t.Players)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        modelBuilder.Entity<PlayerRating>(pr =>
        {
            pr.HasKey(x => x.Id);
            pr.HasIndex(x => x.Email).IsUnique();
            pr.Property(x => x.Email)
                .HasConversion(v => v.ToLowerInvariant(), v => v);
        });

        modelBuilder.Entity<PlayerRatingHistory>(h =>
        {
            h.HasKey(x => x.Id);
            h.HasIndex(x => x.PlayerRatingId);
            h.HasOne(x => x.PlayerRating)
                .WithMany()
                .HasForeignKey(x => x.PlayerRatingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamMatchup>(tm =>
        {
            tm.HasKey(x => x.Id);
            tm.HasOne(x => x.Round)
                .WithMany()
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);
            tm.HasOne(x => x.Team1)
                .WithMany()
                .HasForeignKey(x => x.Team1Id)
                .OnDelete(DeleteBehavior.Restrict);
            tm.HasOne(x => x.Team2)
                .WithMany()
                .HasForeignKey(x => x.Team2Id)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });
    }
}
