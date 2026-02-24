using System.Diagnostics;
using CodeNoesis.CodexSdk.Generator;

const string schemaFolderName = "codex_app-server_schema";
const string combinedSchemaFileName = "codex_app_server_protocol.schemas.json";
const string defaultNamespace = "CodeNoesis.CodexSdk";

string? schemaFile = null;
string? outputDir = null;
var rootNamespace = defaultNamespace;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--schema" or "-s" when i + 1 < args.Length:
            schemaFile = args[++i];
            break;
        case "--output" or "-o" when i + 1 < args.Length:
            outputDir = args[++i];
            break;
        case "--namespace" or "-n" when i + 1 < args.Length:
            rootNamespace = args[++i];
            break;
    }
}

// Default schema location: next to the running executable
var exeDir = AppContext.BaseDirectory;
var schemaDir = Path.Combine(exeDir, schemaFolderName);

if (schemaFile is null)
{
    var candidate = Path.Combine(schemaDir, combinedSchemaFileName);
    if (!File.Exists(candidate))
    {
        Console.WriteLine($"Schema not found at {candidate}");
        Console.WriteLine("Generating schema via: codex app-server generate-json-schema ...");

        Directory.CreateDirectory(schemaDir);
        var psi = new ProcessStartInfo("codex", $"app-server generate-json-schema --out \"{schemaDir}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start codex process.");
        await proc.WaitForExitAsync().ConfigureAwait(false);

        if (proc.ExitCode != 0)
        {
            var stderr = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
            Console.Error.WriteLine($"codex exited with code {proc.ExitCode}: {stderr}");
            return 1;
        }

        Console.WriteLine("Schema generated successfully.");
    }
    else
    {
        Console.WriteLine("Schema folder already exists, skipping generation.");
    }

    schemaFile = candidate;
}

// Default output: src/CodeNoesis.CodexSdk/generated (relative to repo root)
outputDir ??= Path.GetFullPath(
    Path.Combine(exeDir, "..", "..", "..", "..", "CodeNoesis.CodexSdk", "generated"));

Console.WriteLine($"Schema:    {schemaFile}");
Console.WriteLine($"Output:    {outputDir}");
Console.WriteLine($"Namespace: {rootNamespace}");
Console.WriteLine();

// Load all definitions
var defs = await SchemaWalker.LoadDefinitionsAsync(schemaFile, rootNamespace);
Console.WriteLine($"Found {defs.Count} type definitions");

// Build emitter with full type registry
var emitter = new CSharpEmitter(defs, rootNamespace);

// Emit all types
var filesByNamespace = emitter.EmitAll(defs);

// Clean output directory before writing
if (Directory.Exists(outputDir))
    Directory.Delete(outputDir, recursive: true);

// Write files
var totalFiles = 0;
foreach (var (ns, files) in filesByNamespace)
{
    // Map namespace to directory: CodeNoesis.CodexSdk -> outputDir,
    // CodeNoesis.CodexSdk.V2 -> outputDir/V2
    var relPath = ns == rootNamespace
        ? ""
        : ns[(rootNamespace.Length + 1)..].Replace('.', Path.DirectorySeparatorChar);
    var dir = Path.Combine(outputDir, relPath);
    Directory.CreateDirectory(dir);

    foreach (var (fileName, content) in files)
    {
        var filePath = Path.Combine(dir, fileName);
        await File.WriteAllTextAsync(filePath, content).ConfigureAwait(false);
        totalFiles++;
    }
}

Console.WriteLine($"Generated {totalFiles} files in {outputDir}/");
return 0;
