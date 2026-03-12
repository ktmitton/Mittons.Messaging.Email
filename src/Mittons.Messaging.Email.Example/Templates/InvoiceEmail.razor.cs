using Microsoft.AspNetCore.Components;

namespace Mittons.Messaging.Email.Example.Templates;

[EmailTemplate("InvoiceEmail")]
public partial class InvoiceEmail
{
    [Parameter]
    public required string InvoiceNumber { get; set; }

    [Parameter]
    public required DateTime InvoiceDate { get; set; }

    [Parameter]
    public required decimal Amount { get; set; }
}
