using System.Reflection;

namespace Jellyfin.Plugin.HomeScreenSections.Encore;

/// <summary>
/// The inline scripts: one replaces a copy of the home chunk cached before the plugin changed it, the other opens the
/// library and collection pages in the format of the home screen.
/// </summary>
public static class EncoreChunkRefresh
{
    public static string ScriptTag { get; } = Load("EncoreChunkRefresh.js") + Load("EncoreViews.js");

    private static string Load(string name)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{typeof(HomeScreenSectionsPlugin).Namespace}.Inject.{name}")!;
        using StreamReader reader = new StreamReader(stream);
        return $"<script type=\"text/javascript\">{reader.ReadToEnd()}</script>";
    }
}
