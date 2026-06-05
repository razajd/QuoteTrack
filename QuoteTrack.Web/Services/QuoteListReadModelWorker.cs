using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuoteTrack.Application.Interfaces;

namespace QuoteTrack.Web.Services
{
    public class QuoteListReadModelWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<QuoteListReadModelWorker> _logger;

        public QuoteListReadModelWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<QuoteListReadModelWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IQuoteListReadModelService>();
                    await service.RefreshAllAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Quote list read model refresh failed.");
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
    }
}
