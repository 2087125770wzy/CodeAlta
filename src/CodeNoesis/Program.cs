using JKToolKit.CodexSDK.AppServer;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;

/*
Environment.SetEnvironmentVariable("PATH", $"C:\\Users\\alexa\\.cache\\fnm_multishells\\26060_1771870496842;{Environment.GetEnvironmentVariable("PATH")}");

// --- App Server: threads + turns + streaming deltas ---
await using var codex = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
{
    DefaultClientInfo = new("CodeNoesis", "CodeNoesis App", "1.0.0")
});


var threads = await codex.ListThreadsAsync(new ThreadListOptions()
{
    Cwd = @"C:\code\lunet\lunet"
});


foreach(var thread in threads.Threads)
{
    Console.WriteLine($"Name: {thread.Name}, Id: {thread.ThreadId}");
}
*/



var schemaPath = @"C:\code\CodexJsonSchema\codex_app_server_protocol.schemas.json";


var schemaData = await File.ReadAllTextAsync(schemaPath);

var resolver = new JsonReferenceResolver(null);

var schema = await JsonSchema.FromJsonAsync(schemaData, schemaPath, schemaToResolve =>
    {
        Console.WriteLine($"{schemaToResolve.ToJson()}");
        return resolver;
    }
);

var generator = new CSharpGenerator(schema, new CSharpGeneratorSettings()
{

});
var file = generator.GenerateFile();