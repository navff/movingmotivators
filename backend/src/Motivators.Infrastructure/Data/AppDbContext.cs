using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Motivators.Application.Common;
using Motivators.Domain;

namespace Motivators.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<LoginToken> LoginTokens => Set<LoginToken>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Survey> Surveys => Set<Survey>();
    public DbSet<Response> Responses => Set<Response>();
    public DbSet<SurveyResultSnapshot> SurveyResultSnapshots => Set<SurveyResultSnapshot>();
    public DbSet<Checkout> Checkouts => Set<Checkout>();
    public DbSet<FunnelEvent> FunnelEvents => Set<FunnelEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("citext");

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(x => x.Email).HasColumnType("citext").HasMaxLength(254);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.Property(x => x.ConsentVersion).HasMaxLength(20);
        });

        b.Entity<LoginToken>(e =>
        {
            e.ToTable("login_tokens");
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.Property(x => x.Purpose).HasMaxLength(20);
            e.Property(x => x.ReturnUrl).HasMaxLength(500);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Session>(e =>
        {
            e.ToTable("sessions");
            e.HasIndex(x => x.SessionHash).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Team>(e =>
        {
            e.ToTable("teams");
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.OwnerId);
            e.HasOne(x => x.Owner).WithMany(u => u.Teams).HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Survey>(e =>
        {
            e.ToTable("surveys");
            e.Property(x => x.Type).HasMaxLength(32);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.WelcomeMessage).HasMaxLength(500);
            e.Property(x => x.PublicCode).HasColumnType("varchar(16)");
            e.HasIndex(x => x.PublicCode).IsUnique();
            e.HasIndex(x => new { x.TeamId, x.CreatedAt }).IsDescending(false, true);
            e.HasIndex(x => new { x.Status, x.ClosesAt });
            e.Property(x => x.Settings)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => Json.Serialize(v),
                    v => Json.Deserialize<SurveySettings>(v),
                    new ValueComparer<SurveySettings>(
                        (a, c) => Json.Serialize(a) == Json.Serialize(c),
                        v => Json.Serialize(v).GetHashCode(),
                        v => Json.Deserialize<SurveySettings>(Json.Serialize(v))));
            e.HasOne(x => x.Team).WithMany(t => t.Surveys).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Response>(e =>
        {
            e.ToTable("responses");
            e.Property(x => x.Payload).HasColumnType("jsonb");
            e.Property(x => x.ParticipantName).HasMaxLength(60);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.Property(x => x.ConsentVersion).HasMaxLength(20);
            e.Property(x => x.UpdatedCount).HasDefaultValue(0);
            e.HasIndex(x => new { x.SurveyId, x.RespondentKeyHash }).IsUnique();
            e.HasOne(x => x.Survey).WithMany(s => s.Responses).HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SurveyResultSnapshot>(e =>
        {
            e.ToTable("survey_result_snapshots");
            e.HasKey(x => x.SurveyId);
            e.Property(x => x.Result).HasColumnType("jsonb");
            e.HasOne(x => x.Survey).WithOne(s => s.Snapshot)
                .HasForeignKey<SurveyResultSnapshot>(x => x.SurveyId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Checkout>(e =>
        {
            e.ToTable("checkouts");
            e.Property(x => x.Method).HasMaxLength(16);
            e.Property(x => x.Mode).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.ProviderPaymentId).HasMaxLength(64);
            e.HasIndex(x => x.ProviderPaymentId).IsUnique();
            e.HasOne(x => x.Survey).WithMany().HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<FunnelEvent>(e =>
        {
            e.ToTable("funnel_events");
            e.Property(x => x.Event).HasMaxLength(64);
            e.Property(x => x.Props).HasColumnType("jsonb");
            e.HasIndex(x => new { x.Event, x.At });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Survey>().WithMany().HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_log");
            e.Property(x => x.Action).HasMaxLength(64);
            e.Property(x => x.Entity).HasMaxLength(64);
            e.HasIndex(x => x.At);
        });
    }
}
