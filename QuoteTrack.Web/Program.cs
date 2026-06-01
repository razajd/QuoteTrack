using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using QuoteTrack.Application.Interfaces;
using QuoteTrack.Application.Services;
using QuoteTrack.Domain.Entities;
using QuoteTrack.Infrastructure.Data;
using QuoteTrack.Infrastructure.Email;
using QuoteTrack.Infrastructure.Extraction;
using QuoteTrack.Infrastructure.Logging;
using QuoteTrack.Web.Components;
using QuoteTrack.Web.Components.Account;
using QuoteTrack.Web.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.custom.json", optional: true, reloadOnChange: true);

// IMPORTANT: Don't kill the web host if a background service throws.
// We'll log it and keep the website running.
builder.Services.Configure<HostOptions>(o =>
{
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

// QuoteDetails injects IHttpClientFactory
builder.Services.AddHttpClient();

// Extra common web DI helpers
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
builder.Services.AddSingleton<QuoteTrack.Web.Help.HelpIndex>();
builder.Services.AddScoped<WorkflowEmailService>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddControllers();

var connectionString =
    builder.Configuration.GetValue<string>("DbConnectionString")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

builder.Services.AddScoped<IQuoteService, QuoteService>();
builder.Services.AddScoped<IRfqService, RfqService>();
builder.Services.AddScoped<IClientService, ClientService>();

builder.Services.AddSingleton<AppConfigService>();
builder.Services.AddScoped<EmailParsingService>();

builder.Services.AddScoped<IPdfQuotationExtractor, PdfPigQuotationExtractor>();
builder.Services.AddScoped<IExcelQuotationExtractor, ClosedXmlQuotationExtractor>();

// Background services
builder.Services.AddHostedService<ReminderBackgroundService>();
builder.Services.AddHostedService<QuoteTrack.Web.BackgroundServices.ImapLeadIngestionService>();

var app = builder.Build();

// Global exception hooks
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    try
    {
        var ex = e.ExceptionObject as Exception;
        var msg = ex?.ToString() ?? e.ExceptionObject?.ToString() ?? "Unknown unhandled exception";
        Console.WriteLine("UNHANDLED EXCEPTION: " + msg);
        SystemLogger.LogEvent("ERROR", "UNHANDLED", msg);
    }
    catch { }
};

TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    try
    {
        var flat = e.Exception?.Flatten();
        var all = flat?.InnerExceptions?.ToList() ?? new List<Exception>();

        var isOnlyNavigationException =
            all.Count > 0 &&
            all.All(x => x is Microsoft.AspNetCore.Components.NavigationException);

        if (isOnlyNavigationException)
        {
            e.SetObserved();
            return;
        }

        var baseEx = e.Exception?.GetBaseException();
        if (baseEx is Microsoft.AspNetCore.Components.NavigationException)
        {
            e.SetObserved();
            return;
        }

        var msg = e.Exception?.ToString() ?? "Unknown unobserved task exception";
        Console.WriteLine("UNOBSERVED TASK EXCEPTION: " + msg);
        SystemLogger.LogEvent("ERROR", "UNOBSERVED_TASK", msg);
        e.SetObserved();
    }
    catch { }
};

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));

        var seedEnabled = app.Configuration.GetValue<bool>("AdminSeed:Enabled");
        if (seedEnabled)
        {
            var adminEmail = app.Configuration.GetValue<string>("AdminSeed:Email") ?? "";
            var adminPassword = app.Configuration.GetValue<string>("AdminSeed:Password") ?? "";
            var adminName = app.Configuration.GetValue<string>("AdminSeed:FullName") ?? "System Admin";

            if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            {
                var existing = await userManager.FindByEmailAsync(adminEmail);
                if (existing == null)
                {
                    var adminUser = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        FullName = adminName,
                        EmailConfirmed = true
                    };

                    var result = await userManager.CreateAsync(adminUser, adminPassword);
                    if (result.Succeeded)
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Startup migrate/seed failed: " + ex);
        SystemLogger.LogEvent("ERROR", "STARTUP", ex.ToString());
    }
}

app.Run();
