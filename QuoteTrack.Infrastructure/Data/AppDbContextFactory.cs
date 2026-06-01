using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace QuoteTrack.Infrastructure.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // Probe paths to locate appsettings.custom.json reliably during migrations
            var basePath = Directory.GetCurrentDirectory();

            string[] probePaths = new[]
            {
                basePath,
                Path.Combine(basePath, "QuoteTrack.Web"),
                Path.Combine(basePath, "..", "QuoteTrack.Web"),
                Path.Combine(basePath, "..", "..", "QuoteTrack.Web"),
                Path.Combine(basePath, "..", "..", "..", "QuoteTrack.Web")
            };

            IConfigurationRoot? config = null;

            foreach (var p in probePaths)
            {
                var custom = Path.Combine(p, "appsettings.custom.json");
                var app = Path.Combine(p, "appsettings.json");

                if (File.Exists(custom) || File.Exists(app))
                {
                    config = new ConfigurationBuilder()
                        .SetBasePath(p)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                        .AddJsonFile("appsettings.custom.json", optional: true, reloadOnChange: false)
                        .Build();
                    break;
                }
            }

            // Connection string priority:
            // 1) DbConnectionString from custom
            // 2) ConnectionStrings:DefaultConnection
            // 3) Environment variable QUOTETRACK_DB
            var conn =
                config?.GetValue<string>("DbConnectionString")
                ?? config?.GetConnectionString("DefaultConnection")
                ?? Environment.GetEnvironmentVariable("QUOTETRACK_DB");

            if (string.IsNullOrWhiteSpace(conn))
                throw new Exception("DB connection string not found. Ensure DbConnectionString exists in QuoteTrack.Web/appsettings.custom.json, or set QUOTETRACK_DB env var.");

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(conn);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}