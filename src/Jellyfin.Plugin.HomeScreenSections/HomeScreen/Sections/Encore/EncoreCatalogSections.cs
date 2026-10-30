using Jellyfin.Plugin.HomeScreenSections.Encore;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections.Encore;

/// <summary>Movies by genre, under natural names.</summary>
public class EncoreGenreSection : EncoreCatalogRowSection
{
    private static readonly EncoreRow[] s_rows =
    {
        Movie("action", "Filmes de ação"),
        Movie("adventure", "Filmes de aventura"),
        Movie("animation", "Animações"),
        Movie("comedy", "Comédias"),
        Movie("crime", "Filmes de crime"),
        Movie("documentary", "Documentários"),
        Movie("drama", "Dramas"),
        Movie("family", "Para ver em família"),
        Movie("fantasy", "Filmes de fantasia"),
        Movie("scifi", "Ficção científica"),
        Movie("war", "Filmes de guerra"),
        Movie("mystery", "Filmes de mistério"),
        Movie("romance", "Romances"),
        Movie("horror", "Filmes de terror"),
        Movie("thriller", "Suspense"),
    };

    public EncoreGenreSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreCatalog catalog, EncoreHistory history)
        : base(userManager, libraryManager, dtoService, catalog, history)
    {
        DisplayText = "Genre: movies";
    }

    public override string? Section => "EncoreGenre";

    public override string? AdminDescription => "Rows of movies from genres drawn every day, for every account, even without watch history.";

    public override int? Limit => s_rows.Length;

    protected override IReadOnlyList<EncoreRow> Rows => s_rows;

    private static EncoreRow Movie(string genre, string title) => new EncoreRow(genre, title, x => !x.IsSeries && !x.IsAnime && x.Genres.Contains(genre));
}

/// <summary>Series by genre, under natural names.</summary>
public class EncoreSeriesGenreSection : EncoreCatalogRowSection
{
    private static readonly EncoreRow[] s_rows =
    {
        Series("action", "Séries de ação e aventura"),
        Series("comedy", "Séries de comédia"),
        Series("crime", "Séries de crime"),
        Series("drama", "Séries de drama"),
        Series("fantasy", "Séries de fantasia"),
        Series("scifi", "Séries de ficção científica"),
        Series("mystery", "Séries de mistério"),
        Series("documentary", "Séries documentais"),
        Series("horror", "Séries de terror"),
        Series("family", "Séries para a família"),
    };

    public EncoreSeriesGenreSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreCatalog catalog, EncoreHistory history)
        : base(userManager, libraryManager, dtoService, catalog, history)
    {
        DisplayText = "Genre: series";
    }

    public override string? Section => "EncoreSeriesGenre";

    public override string? AdminDescription => "Rows of series from genres drawn every day, for every account, even without watch history.";

    public override int? Limit => s_rows.Length;

    protected override IReadOnlyList<EncoreRow> Rows => s_rows;

    private static EncoreRow Series(string genre, string title) => new EncoreRow(genre, title, x => x.IsSeries && !x.IsAnime && x.Genres.Contains(genre));
}

/// <summary>Anime by genre, series and movies together. "Animation" says nothing here, so it is not a row.</summary>
public class EncoreAnimeGenreSection : EncoreCatalogRowSection
{
    private static readonly EncoreRow[] s_rows =
    {
        Anime("action", "Animes de ação"),
        Anime("adventure", "Animes de aventura"),
        Anime("comedy", "Animes de comédia"),
        Anime("drama", "Animes de drama"),
        Anime("fantasy", "Animes de fantasia"),
        Anime("scifi", "Animes de ficção científica"),
        Anime("mystery", "Animes de mistério"),
        Anime("romance", "Animes de romance"),
        Anime("horror", "Animes de terror"),
    };

    public EncoreAnimeGenreSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreCatalog catalog, EncoreHistory history)
        : base(userManager, libraryManager, dtoService, catalog, history)
    {
        DisplayText = "Genre: anime";
    }

    public override string? Section => "EncoreAnimeGenre";

    public override string? AdminDescription => "Rows of anime from genres drawn every day, for every account, even without watch history.";

    public override int? Limit => s_rows.Length;

    protected override IReadOnlyList<EncoreRow> Rows => s_rows;

    private static EncoreRow Anime(string genre, string title) => new EncoreRow(genre, title, x => x.IsAnime && x.Genres.Contains(genre));
}

/// <summary>Rows from TMDB keywords. The rarer ones are joined, because a row needs a minimum of titles.</summary>
public class EncoreKeywordSection : EncoreCatalogRowSection
{
    private static readonly EncoreRow[] s_rows =
    {
        new EncoreRow("manga", "Animes baseados em mangá", x => x.IsAnime && x.Tags.Contains("based on manga")),
        new EncoreRow("shounen", "Shounen", x => x.IsAnime && x.Tags.Contains("shounen")),
        new EncoreRow("seinen", "Seinen", x => x.IsAnime && x.Tags.Contains("seinen")),
        new EncoreRow("slice-of-life", "Animes do dia a dia", x => x.IsAnime && x.Tags.Contains("slice of life")),
        new EncoreRow("books", "Baseados em livros", x => !x.IsAnime && x.Tags.Contains("based on novel or book")),
        new EncoreRow("true-story", "Baseados em histórias reais", x => x.Tags.Contains("based on true story")),
        new EncoreRow("comics", "Saídos dos quadrinhos", x => !x.IsAnime && (x.Tags.Contains("based on comic") || x.Tags.Contains("superhero"))),
        new EncoreRow("standalone", "Episódios independentes", x => x.IsSeries && !x.IsAnime && new[] { "sitcom", "police procedural", "medical drama", "anthology" }.Any(x.Tags.Contains)),
        new EncoreRow("time-travel", "Viagens no tempo", Tag("time travel")),
        new EncoreRow("dark-comedy", "Comédias de humor ácido", x => !x.IsAnime && x.Tags.Contains("dark comedy")),
        new EncoreRow("dystopia", "Futuros distópicos", Tag("dystopia")),
        new EncoreRow("serial-killer", "Na pista do assassino", x => !x.IsAnime && (x.Tags.Contains("serial killer") || x.Tags.Contains("detective"))),
        new EncoreRow("sports", "Histórias de esporte", Tag("sports")),
        new EncoreRow("suspenseful", "Para roer as unhas", x => !x.IsAnime && (x.Tags.Contains("suspenseful") || x.Tags.Contains("intense"))),
        new EncoreRow("inspirational", "Para sair inspirado", x => !x.IsAnime && x.Tags.Contains("inspirational")),
    };

    public EncoreKeywordSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreCatalog catalog, EncoreHistory history)
        : base(userManager, libraryManager, dtoService, catalog, history)
    {
        DisplayText = "Keywords";
    }

    public override string? Section => "EncoreKeyword";

    public override string? AdminDescription => "Rows from keywords such as based on manga, based on books or true stories, drawn every day.";

    public override int? Limit => s_rows.Length;

    protected override IReadOnlyList<EncoreRow> Rows => s_rows;
}

/// <summary>Titles rated 7.5 or more, of every kind.</summary>
public class EncoreDailyPicksSection : EncoreCatalogRowSection
{
    private static readonly EncoreRow[] s_rows = { new EncoreRow("daily-picks", "Escolhas do dia", x => x.Rating >= 7.5f) };

    public EncoreDailyPicksSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreCatalog catalog, EncoreHistory history)
        : base(userManager, libraryManager, dtoService, catalog, history)
    {
        DisplayText = "Daily picks";
    }

    public override string? Section => "EncoreDailyPicks";

    public override string? AdminDescription => "Well rated titles drawn for each user, the same during the whole day.";

    protected override IReadOnlyList<EncoreRow> Rows => s_rows;
}

/// <summary>Titles produced in Brazil.</summary>
public class EncoreNationalSection : EncoreCatalogRowSection
{
    private static readonly EncoreRow[] s_rows = { new EncoreRow("national", "Nacionais", x => x.IsBrazilian) };

    public EncoreNationalSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreCatalog catalog, EncoreHistory history)
        : base(userManager, libraryManager, dtoService, catalog, history)
    {
        DisplayText = "National titles";
    }

    public override string? Section => "EncoreNational";

    public override string? AdminDescription => "Titles produced in Brazil.";

    protected override IReadOnlyList<EncoreRow> Rows => s_rows;
}

/// <summary>A themed row that only exists in its season. Out of season the section has no row at all.</summary>
public class EncoreSeasonalSection : EncoreCatalogRowSection
{
    private static readonly (int FromMonth, int FromDay, int ToMonth, int ToDay, EncoreRow Row)[] s_seasons =
    {
        (6, 1, 6, 12, new EncoreRow("valentines", "Para o Dia dos Namorados", x => !x.IsAnime && x.Genres.Contains("romance") && x.Rating >= 7f)),
        (10, 1, 11, 2, new EncoreRow("halloween", "Especial de Halloween", x => x.Tags.Contains("halloween") || (!x.IsAnime && x.Genres.Contains("horror") && x.Rating >= 7f))),
        (12, 1, 12, 26, new EncoreRow("christmas", "Clima de Natal", x => new[] { "christmas", "christmas party", "christmas eve", "santa claus" }.Any(x.Tags.Contains))),
    };

    public EncoreSeasonalSection(IUserManager userManager, ILibraryManager libraryManager, IDtoService dtoService, EncoreCatalog catalog, EncoreHistory history)
        : base(userManager, libraryManager, dtoService, catalog, history)
    {
        DisplayText = "Seasonal";
    }

    public override string? Section => "EncoreSeasonal";

    public override string? AdminDescription => "Halloween, Christmas and Valentine's Day rows, each one only in its season.";

    protected override IReadOnlyList<EncoreRow> Rows
    {
        get
        {
            DateTime today = EncoreDraw.Today;
            int date = (today.Month * 100) + today.Day;
            return s_seasons
                .Where(x => date >= (x.FromMonth * 100) + x.FromDay && date <= (x.ToMonth * 100) + x.ToDay)
                .Select(x => x.Row)
                .ToArray();
        }
    }
}
