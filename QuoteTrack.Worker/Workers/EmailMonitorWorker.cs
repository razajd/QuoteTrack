// QuoteTrack.Worker/Workers/EmailMonitorWorker.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using QuoteTrack.Application.Interfaces;

namespace QuoteTrack.Worker.Workers
{
    public class EmailMonitorWorker : BackgroundService
    {
        private readonly ILogger<EmailMonitorWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public EmailMonitorWorker(ILogger<EmailMonitorWorker> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("--- QUOTETRACK WORKER INITIALIZED ---");

            // Check if settings are actually loaded
            var host = _configuration["EmailSettings:IMAP:Host"];
            if (string.IsNullOrEmpty(host))
            {
                _logger.LogCritical("CRITICAL: EmailSettings:IMAP:Host is MISSING from appsettings.json! Check 'Copy to Output Directory' properties.");
            }
            else
            {
                _logger.LogInformation("Target IMAP Host detected: {host}", host);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var ingestionService = scope.ServiceProvider.GetRequiredService<IEmailIngestionService>();

                        _logger.LogInformation(">>> Starting Email Poll at: {time}", DateTimeOffset.Now);
                        await ingestionService.ProcessNewEmailsAsync(stoppingToken);
                        _logger.LogInformation("<<< Poll Completed successfully.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ERROR during email processing loop.");
                }

                // Default to 1 minute for testing purposes so we don't wait 5 mins
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}