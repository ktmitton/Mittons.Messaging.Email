using MimeKit;

namespace Mittons.Messaging.Email.Models;

public record Attachment(string FileName, Stream? Content = null, ContentType? ContentType = null);
