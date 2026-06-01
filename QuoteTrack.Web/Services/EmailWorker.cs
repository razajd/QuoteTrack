// QuoteTrack.Web/Services/EmailWorker.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuoteTrack.Application.Interfaces;

namespace QuoteTrack.Web.Services
{
    public class EmailWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public EmailWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var config = scope.ServiceProvider.GetRequiredService<AppConfigService>();

                    // Only check emails if the Setup Wizard has been completed
                    if (config.Current.IsSetupComplete)
                    {
                        var emailService = scope.ServiceProvider.GetRequiredService<IEmailIngestionService>();
                        await emailService.ProcessNewEmailsAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    QuoteTrack.Infrastructure.Logging.SystemLogger.LogEvent("ERROR", "Background Worker", ex.Message);
                }

                // Wait 60 seconds before checking the inbox again
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
    }
}