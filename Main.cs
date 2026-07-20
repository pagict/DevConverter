using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using DevConverter.Core;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.DevConvert;

public sealed class Main : IPlugin, IPluginI18n
{
    private static readonly ConversionEngine Engine = new();
    private static readonly string IconPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
        "Images",
        "devconvert.png");

    public static string PluginID => "D3C6C982BFB64D1A8FD1837D01E7A4C9";
    public string Name => "DevConverter";
    public string Description => "Developer conversions powered by the shared DevConverter core.";

    public void Init(PluginInitContext context) { }
    public string GetTranslatedPluginTitle() => Name;
    public string GetTranslatedPluginDescription() => Description;

    public List<Result> Query(Query query) => Engine.Convert(query.Search).Select(ToPowerToysResult).ToList();

    private static Result ToPowerToysResult(ConversionResult conversion) => new()
    {
        Title = conversion.Title,
        SubTitle = conversion.CopyText.Length == 0 ? conversion.Subtitle : conversion.Subtitle + " | Enter: copy",
        IcoPath = IconPath,
        Action = _ =>
        {
            if (conversion.CopyText.Length > 0)
                Clipboard.SetText(conversion.CopyText);
            return true;
        }
    };
}
