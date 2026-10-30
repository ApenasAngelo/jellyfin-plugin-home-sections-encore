using System.Security.Cryptography;
using System.Text;

namespace Jellyfin.Plugin.HomeScreenSections.Encore;

/// <summary>
/// A shuffle that stays the same for a user during a whole day and changes on the next one. The seed comes from a hash
/// of the user, the day and the row, because string.GetHashCode() changes every time the server restarts.
/// </summary>
public static class EncoreDraw
{
    // The container usually runs in UTC, where the day would turn at 21:00 in Brazil.
    private static readonly TimeZoneInfo s_timeZone = FindTimeZone();

    public static DateTime Today => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, s_timeZone).Date;

    public static List<T> Shuffle<T>(IEnumerable<T> items, Guid userId, string key)
    {
        List<T> result = items.ToList();
        int day = DateOnly.FromDateTime(Today).DayNumber;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}|{day}|{key}"));
        Random random = new Random(BitConverter.ToInt32(hash, 0));

        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return result;
    }

    private static TimeZoneInfo FindTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
