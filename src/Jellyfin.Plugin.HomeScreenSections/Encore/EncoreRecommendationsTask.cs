using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.HomeScreenSections.Encore;

/// <summary>Rebuilds the catalog and the weekly lists, after the Sunday catalog imports.</summary>
public class EncoreRecommendationsTask : IScheduledTask
{
    private readonly EncoreRecommendations m_recommendations;

    public EncoreRecommendationsTask(EncoreRecommendations recommendations)
    {
        m_recommendations = recommendations;
    }

    public string Name => "Encore Weekly Recommendations";

    public string Key => "Jellyfin.Plugin.HomeScreenSections.EncoreRecommendations";

    public string Description => "Rebuilds the list of recommendations of each user. The home rows draw from it every day.";

    public string Category => "Home Screen Sections";

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        m_recommendations.GenerateAll(cancellationToken);
        progress.Report(100);
        return Task.CompletedTask;
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.WeeklyTrigger,
            DayOfWeek = DayOfWeek.Sunday,
            TimeOfDayTicks = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(20)).Ticks
        };
    }
}
