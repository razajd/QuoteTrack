using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuoteTrack.Application.Interfaces;
using QuoteTrack.Domain.Entities;
using QuoteTrack.Domain.Enums;

namespace QuoteTrack.Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>, IAppDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Quote> Quotes { get; set; }
        public DbSet<Rfq> Rfqs { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<FollowUp> FollowUps { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<QuoteEvent> QuoteEvents { get; set; }
        public DbSet<CommandCenterSnapshot> CommandCenterSnapshots { get; set; }
        public DbSet<CommandCenterQueueItem> CommandCenterQueueItems { get; set; }
        public DbSet<CommandCenterActivityItem> CommandCenterActivityItems { get; set; }
        public DbSet<CommandCenterRadarItem> CommandCenterRadarItems { get; set; }
        public DbSet<QuoteListItem> QuoteListItems { get; set; }
        public DbSet<ReadModelState> ReadModelStates { get; set; }
        public DbSet<MergeRequest> MergeRequests { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.custom.json");

            if (File.Exists(path))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    if (doc.RootElement.TryGetProperty("DbConnectionString", out var prop))
                    {
                        var conn = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(conn))
                        {
                            optionsBuilder.UseNpgsql(conn, npgsql =>
                            {
                                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                            });
                            return;
                        }
                    }
                }
                catch
                {
                }
            }

            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql("Host=localhost;Database=temp_setup;Username=temp;Password=temp", npgsql =>
                {
                    npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Quote>()
                .HasMany(q => q.Attachments)
                .WithOne(a => a.Quote)
                .HasForeignKey(a => a.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Quote>()
                .HasMany(q => q.FollowUps)
                .WithOne(f => f.Quote)
                .HasForeignKey(f => f.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Quote>()
                .HasMany(q => q.Events)
                .WithOne(e => e.Quote)
                .HasForeignKey(e => e.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<QuoteEvent>()
                .HasOne(e => e.ActorUser)
                .WithMany()
                .HasForeignKey(e => e.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<FollowUp>()
                .HasOne(f => f.CreatedByUser)
                .WithMany()
                .HasForeignKey(f => f.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Quote>()
                .HasOne(q => q.Owner)
                .WithMany()
                .HasForeignKey(q => q.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Client>()
                .HasOne(c => c.SubmittedBy)
                .WithMany()
                .HasForeignKey(c => c.SubmittedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Client>()
                .HasOne(c => c.ApprovedBy)
                .WithMany()
                .HasForeignKey(c => c.ApprovedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Quote>()
                .HasOne(q => q.Client)
                .WithMany(c => c.Quotes)
                .HasForeignKey(q => q.ClientId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ActivityLog>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Existing
            builder.Entity<MergeRequest>()
                .HasIndex(m => m.Status);

            // ===== PERFORMANCE INDEXES =====

            // Main dashboard / quotes page filters
            builder.Entity<Quote>()
                .HasIndex(q => q.RecordType);

            builder.Entity<Quote>()
                .HasIndex(q => q.OwnerId);

            builder.Entity<Quote>()
                .HasIndex(q => q.Status);

            builder.Entity<Quote>()
                .HasIndex(q => q.CreatedAt);

            builder.Entity<Quote>()
                .HasIndex(q => q.NextFollowUpDate);

            builder.Entity<Quote>()
                .HasIndex(q => q.AssignedAt);

            builder.Entity<Quote>()
                .HasIndex(q => q.FirstContactedAt);

            builder.Entity<Quote>()
                .HasIndex(q => q.QuotedAt);

            builder.Entity<Quote>()
                .HasIndex(q => q.WonAt);

            builder.Entity<Quote>()
                .HasIndex(q => q.LostAt);

            builder.Entity<Quote>()
                .HasIndex(q => q.ClientId);

            builder.Entity<Quote>()
                .HasIndex(q => q.IsDeleteRequested);

            builder.Entity<Quote>()
                .HasIndex(q => q.EmailMessageId);

            // Useful composite indexes for common dashboard / list queries
            builder.Entity<Quote>()
                .HasIndex(q => new { q.RecordType, q.Status });

            builder.Entity<Quote>()
                .HasIndex(q => new { q.RecordType, q.OwnerId, q.Status });

            builder.Entity<Quote>()
                .HasIndex(q => new { q.RecordType, q.IsDeleteRequested, q.CreatedAt });

            builder.Entity<Quote>()
                .HasIndex(q => new { q.RecordType, q.IsDeleteRequested, q.NextFollowUpDate });

            builder.Entity<Quote>()
                .HasIndex(q => new { q.OwnerId, q.IsDeleteRequested, q.CreatedAt });

            // Follow-up heavy lookups
            builder.Entity<FollowUp>()
                .HasIndex(f => f.QuoteId);

            builder.Entity<FollowUp>()
                .HasIndex(f => f.CreatedAt);

            builder.Entity<FollowUp>()
                .HasIndex(f => new { f.QuoteId, f.CreatedAt });

            builder.Entity<FollowUp>()
                .HasIndex(f => new { f.QuoteId, f.DueDate });

            // Attachment lookups
            builder.Entity<Attachment>()
                .HasIndex(a => a.QuoteId);

            // Activity log lookups
            builder.Entity<ActivityLog>()
                .HasIndex(a => a.UserId);

            builder.Entity<ActivityLog>()
                .HasIndex(a => a.Timestamp);

            builder.Entity<ActivityLog>()
                .HasIndex(a => new { a.UserId, a.Timestamp });

            builder.Entity<ActivityLog>()
                .HasIndex(a => a.RelatedQuoteId);

            // Structured workflow/audit lookups
            builder.Entity<QuoteEvent>()
                .HasIndex(e => e.QuoteId);

            builder.Entity<QuoteEvent>()
                .HasIndex(e => e.EventType);

            builder.Entity<QuoteEvent>()
                .HasIndex(e => e.OccurredAt);

            builder.Entity<QuoteEvent>()
                .HasIndex(e => new { e.QuoteId, e.OccurredAt });

            builder.Entity<QuoteEvent>()
                .HasIndex(e => new { e.EventType, e.OccurredAt });

            builder.Entity<QuoteEvent>()
                .HasIndex(e => e.ActorUserId);

            // Prepared Command Center read model
            builder.Entity<CommandCenterSnapshot>()
                .HasKey(s => s.ScopeKey);

            builder.Entity<CommandCenterSnapshot>()
                .HasIndex(s => s.IsStale);

            builder.Entity<CommandCenterSnapshot>()
                .HasIndex(s => s.LastRefreshedAt);

            builder.Entity<CommandCenterQueueItem>()
                .HasIndex(i => new { i.ScopeKey, i.SortRank });

            builder.Entity<CommandCenterQueueItem>()
                .HasIndex(i => new { i.ScopeKey, i.RecordType });

            builder.Entity<CommandCenterActivityItem>()
                .HasIndex(i => new { i.ScopeKey, i.SortRank });

            builder.Entity<CommandCenterActivityItem>()
                .HasIndex(i => new { i.ScopeKey, i.WhenUtc });

            builder.Entity<CommandCenterRadarItem>()
                .HasIndex(i => new { i.ScopeKey, i.RadarType, i.SortRank });

            builder.Entity<QuoteListItem>()
                .HasKey(i => i.QuoteId);

            builder.Entity<QuoteListItem>()
                .HasIndex(i => new { i.RecordType, i.IsDeleteRequested, i.CreatedAt });

            builder.Entity<QuoteListItem>()
                .HasIndex(i => new { i.RecordType, i.Status, i.IsDeleteRequested, i.OwnerId });

            builder.Entity<QuoteListItem>()
                .HasIndex(i => new { i.OwnerId, i.IsDeleteRequested, i.CreatedAt });

            builder.Entity<QuoteListItem>()
                .HasIndex(i => i.NextFollowUpDate);

            builder.Entity<QuoteListItem>()
                .HasIndex(i => i.EmailReceivedDateTime);

            builder.Entity<QuoteListItem>()
                .HasIndex(i => i.LastNoteAt);

            builder.Entity<ReadModelState>()
                .HasKey(s => s.Key);

            builder.Entity<ReadModelState>()
                .HasIndex(s => s.IsStale);

            // Client lookups / linking
            builder.Entity<Client>()
                .HasIndex(c => c.CompanyName);

            // Rfq queue
            builder.Entity<Rfq>()
                .HasIndex(r => r.ReceivedAt);

            //builder.Entity<Rfq>()
            //    .HasIndex(r => r.AssignedToUserId);
        }

        public override int SaveChanges()
        {
            NormalizeDateTimes();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            NormalizeDateTimes();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            NormalizeDateTimes();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            NormalizeDateTimes();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void NormalizeDateTimes()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified);

            foreach (var entry in entries)
            {
                foreach (var property in entry.Properties)
                {
                    if (property.Metadata.ClrType == typeof(DateTime))
                    {
                        if (property.CurrentValue is DateTime dt)
                            property.CurrentValue = EnsureUtc(dt);
                    }
                    else if (property.Metadata.ClrType == typeof(DateTime?))
                    {
                        if (property.CurrentValue is DateTime dtNullable)
                            property.CurrentValue = EnsureUtc(dtNullable);
                    }
                }
            }
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            if (value == DateTime.MinValue)
                return DateTime.SpecifyKind(value, DateTimeKind.Utc);

            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }
    }
}
