using System;
using System.Linq;
using System.Text.Json;
using DevConverter.Core;

var arguments = args.ToList();
var json = arguments.Remove("--json");
var command = string.Join(' ', arguments);
var results = new ConversionEngine().Convert(command);

if (json)
{
    Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }));
}
else
{
    foreach (var result in results)
        Console.WriteLine(result.CopyText.Length > 0 ? result.CopyText : $"{result.Title}: {result.Subtitle}");
}

Environment.ExitCode = results.Any(result => result.IsError) ? 2 : 0;
