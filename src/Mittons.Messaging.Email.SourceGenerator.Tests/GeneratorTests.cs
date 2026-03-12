using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
namespace Mittons.Messaging.Email.SourceGenerator.Tests;

public class GeneratorTests
{
    private static readonly Lazy<List<PortableExecutableReference>> _references = new(LoadReferences);

    private static List<PortableExecutableReference> LoadReferences()
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        return refs;
    }

    /// <summary>
    /// Runs the MockGenerator against the given source and returns the generated files
    /// as an array of strings, ordered by hint name for stable snapshot comparison.
    /// </summary>
    protected static string[] RunGenerator(string source, CSharpParseOptions? parseOptions = null)
    {
        parseOptions ??= CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        ).WithReferences(_references.Value);

        TemplateExtensionMethodGenerator generator = new();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        var runResult = driver.RunGenerators(compilation).GetRunResult();

        // Check for generator errors
        var errors = runResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            var errorMessages = string.Join(Environment.NewLine, errors.Select(e => e.ToString()));
            throw new InvalidOperationException($"Generator produced errors:{Environment.NewLine}{errorMessages}");
        }

        return [.. runResult.GeneratedTrees
            .OrderBy(t => t.FilePath, StringComparer.Ordinal)
            .Select(t => t.GetText().ToString())];
    }

    /// <summary>
    /// Runs the generator and returns a normalized, scrubbed string for snapshot verification.
    /// </summary>
    protected static string RunGeneratorAndFormat(string source)
    {
        var files = RunGenerator(source);
        var combined = string.Join("\n\n// ===== FILE SEPARATOR =====\n\n",
            files.Select(NormalizeNewlines));
        return combined;
    }

    /// <summary>
    /// Snapshot-verifies the generated output. Compares against a .verified.txt file
    /// in the test project directory. If no verified file exists, creates a .received.txt.
    /// </summary>
    protected static async Task VerifyGeneratorOutput(
        string source,
        [CallerMemberName] string testName = "",
        [CallerFilePath] string filePath = "")
    {
        var generatedOutput = RunGeneratorAndFormat(source);
        generatedOutput = NormalizeNewlines(generatedOutput);

        var testDir = Path.GetDirectoryName(filePath)!;
        var receivedPath = Path.Combine(testDir, "Snapshots", $"{testName}.received.txt");
        var verifiedPath = Path.Combine(testDir, "Snapshots", $"{testName}.verified.txt");

        // Ensure Snapshots directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(receivedPath)!);

        if (!File.Exists(verifiedPath))
        {
            // Write .received.txt for review and fail — never auto-accept
            await File.WriteAllTextAsync(receivedPath, generatedOutput);
            throw new InvalidOperationException(
                $"No verified snapshot found for '{testName}'.\n" +
                $"Review: {receivedPath}\n" +
                $"Accept by renaming to '.verified.txt'.");
        }

        var verified = NormalizeNewlines(await File.ReadAllTextAsync(verifiedPath));

        if (!string.Equals(generatedOutput, verified, StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(receivedPath, generatedOutput);
            throw new InvalidOperationException(
                $"Snapshot mismatch for '{testName}'.\n" +
                $"Received: {receivedPath}\n" +
                $"Verified: {verifiedPath}\n" +
                $"Update the .verified.txt file if this change is intentional.");
        }

        // Clean up any leftover .received.txt on success
        if (File.Exists(receivedPath))
        {
            File.Delete(receivedPath);
        }
    }

    private static string NormalizeNewlines(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    [Test]
    public Task TemplateWithNoParameters()
    {
        var source = $$"""
        using Microsoft.AspNetCore.Components;
        using Mittons.Messaging.Email;

        namespace Mittons.Messaging.Email.SourceGenerator.Tests;

        [EmailTemplate("InvoiceEmail")]
        public class InvoiceEmail : ComponentBase
        {
            public string Title { get; set; } = "Invoice";
        }
        """;

        return VerifyGeneratorOutput(source);
    }

    [Test]
    public Task TemplateWithParameters()
    {
        var source = $$"""
        using Microsoft.AspNetCore.Components;
        using Mittons.Messaging.Email;

        namespace Mittons.Messaging.Email.SourceGenerator.Tests;

        [EmailTemplate("InvoiceEmail")]
        public class InvoiceEmail : ComponentBase
        {
            public string Title { get; set; } = "Invoice";

            [Microsoft.AspNetCore.Components.ParameterAttribute]
            public string InvoiceNumber { get; set; }

            [Microsoft.AspNetCore.Components.ParameterAttribute]
            public DateTime InvoiceDate { get; set; }
        }
        """;

        return VerifyGeneratorOutput(source);
    }

    [Test]
    public Task TemplateWithMultipleNames()
    {
        var source = $$"""
        using Microsoft.AspNetCore.Components;
        using Mittons.Messaging.Email;

        namespace Mittons.Messaging.Email.SourceGenerator.Tests;

        [EmailTemplate("InvoiceEmail")]
        [EmailTemplate("OrderEmail")]
        public class InvoiceEmail : ComponentBase
        {
            public string Title { get; set; } = "Invoice";

            [Microsoft.AspNetCore.Components.ParameterAttribute]
            public string InvoiceNumber { get; set; }

            [Microsoft.AspNetCore.Components.ParameterAttribute]
            public DateTime InvoiceDate { get; set; }
        }
        """;

        return VerifyGeneratorOutput(source);
    }
}
