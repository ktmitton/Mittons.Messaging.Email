using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Mittons.Messaging.Email.Models;
using Mittons.Messaging.Email.Settings;

namespace Mittons.Messaging.Email.Services;

public interface IEmailService
{
    Task SendEmailAsync<TComponent>(
        string subject,
        string from,
        IEnumerable<string>? to = default,
        IEnumerable<string>? cc = default,
        IEnumerable<string>? bcc = default,
        IEnumerable<string>? replyTo = default,
        IEnumerable<Attachment>? attachments = default,
        Dictionary<string, object?>? parameters = default,
        CancellationToken cancellationToken = default
    ) where TComponent : IComponent;

    Task SendEmailAsync<TComponent>(
        string subject,
        MailboxAddress from,
        IEnumerable<MailboxAddress>? to = default,
        IEnumerable<MailboxAddress>? cc = default,
        IEnumerable<MailboxAddress>? bcc = default,
        IEnumerable<MailboxAddress>? replyTo = default,
        IEnumerable<Attachment>? attachments = default,
        Dictionary<string, object?>? parameters = default,
        CancellationToken cancellationToken = default
    ) where TComponent : IComponent;
}

public class EmailService(IOptionsSnapshot<SmtpSettings> smtpSettings, IServiceProvider serviceProvider, ILoggerFactory loggerFactory) : IEmailService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly SmtpSettings _smtpSettings = smtpSettings.Value;

    private Task<string> RenderComponentToStringAsync<TComponent>(ParameterView parameters) where TComponent : IComponent
    {
        using HtmlRenderer renderer = new(_serviceProvider, _loggerFactory);

        return renderer.Dispatcher.InvokeAsync(async () =>
        {
            var htmlContent = await renderer.RenderComponentAsync<TComponent>(parameters);

            return htmlContent.ToHtmlString();
        });
    }

    public async Task SendEmailAsync<TComponent>(
        string subject,
        string from,
        IEnumerable<string>? to,
        IEnumerable<string>? cc = default,
        IEnumerable<string>? bcc = default,
        IEnumerable<string>? replyTo = default,
        IEnumerable<Attachment>? attachments = default,
        Dictionary<string, object?>? parameters = default,
        CancellationToken cancellationToken = default
    ) where TComponent : IComponent
        => await SendEmailAsync<TComponent>(
            subject: subject,
            from: new MailboxAddress(from, from),
            to: to?.Select(email => new MailboxAddress(email, email)),
            cc: cc?.Select(email => new MailboxAddress(email, email)),
            bcc: bcc?.Select(email => new MailboxAddress(email, email)),
            replyTo: replyTo?.Select(email => new MailboxAddress(email, email)),
            parameters: parameters,
            cancellationToken: cancellationToken
        );

    public async Task SendEmailAsync<TTemplate>(
        string subject,
        MailboxAddress from,
        IEnumerable<MailboxAddress>? to,
        IEnumerable<MailboxAddress>? cc = default,
        IEnumerable<MailboxAddress>? bcc = default,
        IEnumerable<MailboxAddress>? replyTo = default,
        IEnumerable<Attachment>? attachments = default,
        Dictionary<string, object?>? parameters = default,
        CancellationToken cancellationToken = default
    ) where TTemplate : IComponent
    {
        BodyBuilder body = new()
        {
            HtmlBody = await RenderComponentToStringAsync<TTemplate>(ParameterView.FromDictionary(parameters ?? [])),
        };

        if (attachments is not null)
        {
            foreach (var attachment in attachments)
            {
                if (attachment.Content is not null && attachment.ContentType is not null)
                {
                    await body.Attachments.AddAsync(attachment.FileName, attachment.Content, attachment.ContentType);
                }
                else if (attachment.Content is not null)
                {
                    await body.Attachments.AddAsync(attachment.FileName, attachment.Content);
                }
                else if (attachment.ContentType is not null)
                {
                    await body.Attachments.AddAsync(attachment.FileName, attachment.ContentType);
                }
                else
                {
                    await body.Attachments.AddAsync(attachment.FileName);
                }
            }
        }

        MimeMessage message = new()
        {
            Subject = subject,
            Body = body.ToMessageBody()
        };
        message.From.Add(from);
        message.To.AddRange(to ?? []);
        message.Bcc.AddRange(bcc ?? []);
        message.Cc.AddRange(cc ?? []);
        message.ReplyTo.AddRange(replyTo ?? []);

        using SmtpClient client = new();

        await client.ConnectAsync(_smtpSettings.Host, _smtpSettings.Port, _smtpSettings.UseSsl, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_smtpSettings.Password))
        {
            await client.AuthenticateAsync(_smtpSettings.Username, _smtpSettings.Password, cancellationToken).ConfigureAwait(false);
        }

        var response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);

        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
    }
}
