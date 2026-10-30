using Jellyfin.Plugin.HomeScreenSections.Encore;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections.Encore;

/// <summary>A row of the catalog. The key becomes a CSS class on the client, so it only uses a-z, 0-9 and hyphens.</summary>
public sealed record EncoreRow(string Key, string Title, Func<EncoreEntry, bool> Match);
