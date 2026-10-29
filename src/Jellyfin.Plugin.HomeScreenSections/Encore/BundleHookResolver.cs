using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HomeScreenSections.Encore
{
    /// <summary>
    /// Finds the minified names that <c>loadSections.js</c> needs by reading the jellyfin-web bundle as it is served,
    /// instead of hardcoding them per Jellyfin version. Only the webpack module that defines <c>loadSections</c> is
    /// searched, because other modules in the same chunk reuse the same short names for other things.
    /// </summary>
    public static class BundleHookResolver
    {
        private const string c_loadSectionsMarker = ",loadSections:";

        // Start of a webpack module: `12345:function(e,t,r){` or `12345:(e,t,r)=>{`.
        private static readonly Regex s_moduleStart = new Regex(@"[{,](\d+):(?:function)?\(([\w$]+),([\w$]+),([\w$]+)\)(?:=>)?\{");

        private static readonly Regex s_cardBuilder = new Regex(@"(?<![\w$.])([\w$]+\.[\w$]+)\.getCardsHtml\(");
        private static readonly Regex s_appRouter = new Regex(@"(?<![\w$.])([\w$]+)\.appRouter\.");
        private static readonly Regex s_layoutManager = new Regex(@"(?<![\w$.])([\w$]+)\.([\w$]+)\.tv\b");
        private static readonly Regex s_imageHelper = new Regex(@"(?<![\w$.])([\w$]+\.[\w$]+)\.getLibraryIcon\(");

        // The shape helpers are pure functions that return card CSS class names, so they are rebuilt here instead of
        // being looked up. The keys are the ones loadSections.js reads (UI = backdrop, xK = portrait, zP = square).
        private const string c_shapeBuilder =
            "{UI:function(e){return e===void 0||e?\"overflowBackdrop\":\"backdrop\"}," +
            "xK:function(e){return e===void 0||e?\"overflowPortrait\":\"portrait\"}," +
            "zP:function(e){return e===void 0||e?\"overflowSquare\":\"square\"}}";

        // Used only by the library buttons; a missing icon is better than a broken home screen.
        private const string c_imageHelperFallback = "{getLibraryIcon:function(){return \"folder\"}}";

        public static string? LastError { get; private set; }

        /// <summary>
        /// Replaces the hook placeholders in <paramref name="replacementText"/>. Returns <c>null</c> when a required
        /// name is not found, so the caller can leave the bundle untouched and the stock home screen still renders.
        /// </summary>
        public static string? Apply(string chunk, string replacementText)
        {
            LastError = null;

            string? module = FindLoadSectionsModule(chunk);
            if (module == null)
            {
                return Fail("could not find the webpack module that defines loadSections");
            }

            string? cardBuilder = MostCommon(s_cardBuilder, module, m => m.Groups[1].Value);
            string? appRouter = MostCommon(s_appRouter, module, m => m.Groups[1].Value);
            string? layoutManager = MostCommon(s_layoutManager, module, m => m.Groups[1].Value + "." + m.Groups[2].Value);
            if (cardBuilder == null || appRouter == null || layoutManager == null)
            {
                return Fail($"missing names (cardBuilder={cardBuilder}, appRouter={appRouter}, layoutManager={layoutManager})");
            }

            // loadSections.js reads `{{layoutmanager_hook}}.A`.
            string layoutManagerHook = layoutManager.EndsWith(".A", StringComparison.Ordinal)
                ? layoutManager[..^2]
                : $"({{A:{layoutManager}}})";

            string imageHelper = MostCommon(s_imageHelper, module, m => m.Groups[1].Value) ?? c_imageHelperFallback;

            return replacementText
                .Replace("{{cardbuilder_hook}}", cardBuilder)
                .Replace("{{appRouterParent_hook}}", appRouter)
                .Replace("{{layoutmanager_hook}}", layoutManagerHook)
                .Replace("{{shapebuilder_hook}}", c_shapeBuilder)
                .Replace("{{imagehelper_hook}}", imageHelper);
        }

        /// <summary>
        /// Logs, at startup, whether the names can be found in a bundle file, so a new Jellyfin version that breaks the
        /// lookup shows up in the server log instead of only as a stock home screen.
        /// </summary>
        public static void LogCheck(string chunk, string fileName, ILogger logger)
        {
            string? resolved = Apply(chunk, "{{cardbuilder_hook}} {{appRouterParent_hook}} {{layoutmanager_hook}} {{imagehelper_hook}}");
            if (resolved == null)
            {
                logger.LogError("Encore: cannot patch `{File}`, the stock home screen will be used: {Reason}", fileName, LastError);
            }
            else
            {
                logger.LogInformation("Encore: names found in `{File}` (cardBuilder, appRouter, layoutManager, imageHelper): {Names}", fileName, resolved);
            }
        }

        private static string? FindLoadSectionsModule(string chunk)
        {
            int marker = chunk.IndexOf(c_loadSectionsMarker, StringComparison.Ordinal);
            if (marker < 0)
            {
                return null;
            }

            MatchCollection starts = s_moduleStart.Matches(chunk);
            Match? start = starts.LastOrDefault(m => m.Index < marker);
            if (start == null)
            {
                return null;
            }

            Match? next = starts.FirstOrDefault(m => m.Index > marker);
            int end = next?.Index ?? chunk.Length;
            return chunk[start.Index..end];
        }

        private static string? MostCommon(Regex regex, string text, Func<Match, string> selector)
        {
            return regex.Matches(text)
                .Select(selector)
                .GroupBy(x => x)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();
        }

        private static string? Fail(string reason)
        {
            LastError = reason;
            return null;
        }
    }
}
