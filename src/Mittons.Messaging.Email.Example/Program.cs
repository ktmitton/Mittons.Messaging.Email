using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Mittons.Messaging.Email;
using Mittons.Messaging.Email.Services;
using Mittons.Messaging.Email.Settings;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

using IHost host = builder.Build();

var emailService = host.Services.GetRequiredService<IEmailService>();

await emailService.SendInvoiceEmailAsync(
    "INV-12345",
    DateTime.UtcNow,
    99.9m,
    "Your Invoice",
    "no-reply@example.com",
    ["recipient@example.com"]
);

await host.RunAsync();
