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
                            optionsBuilder.UseNpgsql(conn);
                            return;
                        }
                    }
                }
                catch { }
            }

            if (!optionsBuilder.IsConfigured)
                optionsBuilder.UseNpgsql("Host=localhost;Database=temp_setup;Username=temp;Password=temp");
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

            builder.Entity<MergeRequest>()
                .HasIndex(m => m.Status);
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
