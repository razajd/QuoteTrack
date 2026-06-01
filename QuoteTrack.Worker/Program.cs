// QuoteTrack.Worker/Program.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuoteTrack.Application.Interfaces;
using QuoteTrack.Infrastructure.Data;
using QuoteTrack.Infrastructure.Email;
using QuoteTrack.Infrastructure.Extraction;
using QuoteTrack.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

// 1. Register the PostgreSQL DbContext for the Worker
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Register application services
builder.Services.AddTransient<IEmailIngestionService, EmailIngestionService>();
builder.Services.AddTransient<IPdfQuotationExtractor, PdfPigQuotationExtractor>();
builder.Services.AddTransient<IExcelQuotationExtractor, ClosedXmlQuotationExtractor>();

// 3. Register the background worker
builder.Services.AddHostedService<EmailMonitorWorker>();

var host = builder.Build();
host.Run();