# Mittons.Messaging.Email

## What is Mittons.Messaging.Email?

Mittons.Messaging.Email is a C# library which may be used for templating emails with Razor components and streamline the sending process.

## Road to Version 1.0

The library was originally devleoped to help sending emails through an SMTP server, so it's very limited in scope and useage.

The following functionality is required for version 1.0:

- A service needs exposed for building a message body from a template.

- The EmailService should support a more generic injection of mail server settings instead of assuming the mail server only supports the SMTP protocol. This doesn't mean support for all protocols is available, but it should be possible to add support for other protocols with minor version updates instead of major breaking updates.

## Using Mittons.Messaging.Email

Let's assume we want to send invoice emails with the following dynamic content:

- Invoice Number

- Invoice Date

- Amount

Our goal is to be able to write code like this:

```csharp
emailService.SendInvoiceAsync(
    invoiceNumber: "INV-001",
    invoiceDate: new DateTime(2026, 1, 1),
    amount: 150.99,
    subject: "Invoice for your order!",
    from: "no-reply@example.com",
    to: ["customer@example.com"]
);
```

For the purposes of this example, we'll assume a project named TestApp containing a Templates folder, making the namespace for the template `TestApp.Templates`.

### Creating Templates

---

A template is composed of two files:

- A Razor file providing the markup for the template

- A partial class file that acts as the trigger for the source generator and declares the dynamic parameters of the template

> **Note:** Splitting a template into two files is due to my current knowledge on how Razor files are converted to class files with source generators behind the scenes and the limitations that imposes on other source generators. If there's a way to have a source generator run off `@code` blocks in a Razor file, please let me know as that could allow a cleaner implementation.

The partial class file needs to have the `EmailTemplate` attribute applied to the class so it can be picked up by the source generator. The attribute takes in a name for the template, which is then used by the source generator to add a function to `IEmailService` for sending emails with that template.

#### TestApp/Templates/Invoice.razor

```razor
<p>Invoice Number: @InvoiceNumber</p>
<p>Invoice Date: @InvoiceDate</p>
<p>Amount: @Amount</p>
```

#### TestApp/Templates/Invoice.razor.cs

```csharp
using Microsoft.AspNetCore.Components;

namespace TestApp.Templates;

[EmailTemplate("Invoice")]
public partial class Invoice
{
    [Parameter]
    public required string InvoiceNumber { get; set; }

    [Parameter]
    public required DateTime InvoiceDate { get; set; }

    [Parameter]
    public required decimal Amount { get; set; }
}
```

### Configuring the Application

---

Now that we have our template, we need to configure the application to leverage it. This means providing configuration settings and secrets for communicating with the mail server and registering all the required services with the dotnet service provider.

#### TestApp/appsettings.json

For this example, we'll assume a local MailPit server with no authentication.

```json
{
    "SmtpSettings": {
        "Host": "localhost",
        "Port": 1025,
        "UseSsl": false,
        "Username": "",
        "Password": ""
    }
}
```

#### TestApp/Program.cs

To streamline the process of sending email, we use dependency injection. This requires registering `IEmailService` for sending the emails, and configuring `SmtpSettings` for communicating with the mail server.

The following service registrations need applied to accomplish this:

```csharp
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
```
