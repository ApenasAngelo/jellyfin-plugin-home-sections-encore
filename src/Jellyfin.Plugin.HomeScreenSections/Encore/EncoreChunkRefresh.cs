using System.Reflection;

namespace Jellyfin.Plugin.HomeScreenSections.Encore;

/// <summary>The inline script that replaces a copy of the home chunk cached before the plugin changed it.</summary>
public static class EncoreChunkRefresh
{
    public static string ScriptTag { get; } = Load();

    private static string Load()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{typeof(HomeScreenSectionsPlugin).Namespace}.Inject.EncoreChunkRefresh.js")!;
        using StreamReader reader = new StreamReader(stream);
        return $"<script type=\"text/javascript\">{reader.ReadToEnd()}</script>";
    }
}
