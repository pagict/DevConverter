namespace DevConverter.Core;

public sealed record ConversionResult(string Title, string Subtitle, string CopyText = "", bool IsError = false);
