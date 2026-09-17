using PayBeat.App.Models;

namespace PayBeat.App.Services;

/// <summary>
/// Pure static helper that computes real-time earnings and workday progress from <see cref="SalarySettings"/>.
/// Has no side effects and holds no state.
/// </summary>
/// <remarks>
/// Supports both same-day shifts (<c>WorkStart &lt; WorkEnd</c>, e.g. 09:00-18:00) and overnight
/// shifts that cross midnight (<c>WorkEnd &lt; WorkStart</c>, e.g. 22:00-06:00). This relies on
/// <see cref="TimeOnly"/>'s subtraction operator, which always returns the non-negative elapsed
/// time between two points on a 24-hour circular clock (e.g. <c>06:00 - 22:00</c> is <c>8h</c>,
/// not <c>-16h</c>) — so measuring everything as an offset from <see cref="SalarySettings.WorkStart"/>
/// naturally wraps around midnight without needing a separate branch for overnight shifts.
/// The lunch break itself is not supported crossing midnight (<c>LunchBreakEnd</c> must be later
/// than <c>LunchBreakStart</c> on the clock), though the break window as a whole may fall anywhere
/// inside an overnight work window.
/// </remarks>
public static class EarningsCalculator
{
    /// <summary>
    /// Returns the amount earned as of <paramref name="now"/> based on linear interpolation
    /// between <see cref="SalarySettings.WorkStart"/> and <see cref="SalarySettings.WorkEnd"/>,
    /// deducting any lunch break. Returns <c>0</c> on non-work days, before work starts, and
    /// <see cref="SalarySettings.DailySalary"/> after work ends.
    /// </summary>
    /// <param name="s">Current salary settings.</param>
    /// <param name="now">Point in time to evaluate.</param>
    public static decimal Calculate(SalarySettings s, DateTime now)
    {
        if (!IsWorkday(s, now))
        {
            return 0m;
        }

        var current = TimeOnly.FromDateTime(now);
        var totalSeconds = (s.WorkEnd - s.WorkStart).TotalSeconds;
        var elapsedSeconds = (current - s.WorkStart).TotalSeconds;

        if (!IsWithinWorkWindow(s, current))
        {
            return IsPastWorkEnd(s, current) ? s.DailySalary : 0m;
        }

        var effectiveTotal = EffectiveWorkSeconds(s, totalSeconds);
        if (effectiveTotal <= 0)
        {
            return s.DailySalary;
        }

        var effectiveElapsed = EffectiveElapsedSeconds(s, totalSeconds, elapsedSeconds);

        return s.DailySalary * (decimal)(effectiveElapsed / effectiveTotal);
    }

    /// <summary>
    /// Returns how much of the workday has elapsed as of <paramref name="now"/>, clamped to
    /// <c>[0, WorkEnd - WorkStart]</c>.
    /// </summary>
    /// <param name="s">Current salary settings.</param>
    /// <param name="now">Point in time to evaluate.</param>
    public static TimeSpan Elapsed(SalarySettings s, DateTime now)
    {
        var current = TimeOnly.FromDateTime(now);
        var elapsedSeconds = (current - s.WorkStart).TotalSeconds;

        if (!IsWithinWorkWindow(s, current))
        {
            return IsPastWorkEnd(s, current) ? s.WorkEnd - s.WorkStart : TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(elapsedSeconds);
    }

    /// <summary>
    /// Returns <c>true</c> when earnings should accrue on the date of <paramref name="now"/>.
    /// Weekends are excluded unless <see cref="SalarySettings.WorkOnWeekends"/> is set.
    /// </summary>
    /// <param name="s">Current salary settings.</param>
    /// <param name="now">Point in time to evaluate.</param>
    public static bool IsWorkday(SalarySettings s, DateTime now)
    {
        if (s.WorkOnWeekends)
        {
            return true;
        }
        return now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="current"/> falls inside the work window
    /// defined by <see cref="SalarySettings.WorkStart"/>/<see cref="SalarySettings.WorkEnd"/>
    /// (wrapping past midnight for overnight shifts).
    /// </summary>
    /// <param name="s">Current salary settings.</param>
    /// <param name="current">Time of day to evaluate.</param>
    public static bool IsWithinWorkWindow(SalarySettings s, TimeOnly current)
    {
        var totalSeconds = (s.WorkEnd - s.WorkStart).TotalSeconds;
        var elapsedSeconds = (current - s.WorkStart).TotalSeconds;
        return elapsedSeconds < totalSeconds;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <see cref="SalarySettings.LunchBreakEnabled"/> is
    /// <see langword="false"/>, or the configured lunch break doesn't cross midnight and falls
    /// entirely inside the (possibly overnight) work window. Shared by settings validation and
    /// the earnings calculations below so the two can't drift apart.
    /// </summary>
    /// <param name="s">Current salary settings.</param>
    public static bool IsLunchBreakValid(SalarySettings s)
    {
        if (!s.LunchBreakEnabled)
        {
            return true;
        }

        var totalSeconds = (s.WorkEnd - s.WorkStart).TotalSeconds;
        return TryGetLunchBreakOffsets(s, totalSeconds, out _, out _);
    }

    /// <summary>
    /// Returns the per-second earnings rate, accounting for any lunch break deduction.
    /// Returns <c>0</c> when the effective work window has zero duration.
    /// </summary>
    /// <param name="s">Current salary settings.</param>
    public static decimal RatePerSecond(SalarySettings s)
    {
        var totalSeconds = (s.WorkEnd - s.WorkStart).TotalSeconds;
        var effectiveTotal = EffectiveWorkSeconds(s, totalSeconds);
        return effectiveTotal > 0 ? s.DailySalary / (decimal)effectiveTotal : 0m;
    }

    /// <summary>
    /// Returns how much of the workday remains as of <paramref name="now"/>, clamped to
    /// <c>[0, WorkEnd - WorkStart]</c>.
    /// </summary>
    /// <param name="s">Current salary settings.</param>
    /// <param name="now">Point in time to evaluate.</param>
    public static TimeSpan Remaining(SalarySettings s, DateTime now)
    {
        var current = TimeOnly.FromDateTime(now);

        if (!IsWithinWorkWindow(s, current))
        {
            return IsPastWorkEnd(s, current) ? TimeSpan.Zero : s.WorkEnd - s.WorkStart;
        }

        return s.WorkEnd - current;
    }

    /// <summary>
    /// Returns a value in [0.0, 1.0] representing how far through the workday <paramref name="now"/> is,
    /// holding steady during any lunch break.
    /// </summary>
    /// <param name="s">Current salary settings.</param>
    /// <param name="now">Point in time to evaluate.</param>
    public static double WorkdayProgress(SalarySettings s, DateTime now)
    {
        var current = TimeOnly.FromDateTime(now);

        if (!IsWithinWorkWindow(s, current))
        {
            return IsPastWorkEnd(s, current) ? 1.0 : 0.0;
        }

        var totalSeconds = (s.WorkEnd - s.WorkStart).TotalSeconds;
        var elapsedSeconds = (current - s.WorkStart).TotalSeconds;
        var effectiveTotal = EffectiveWorkSeconds(s, totalSeconds);
        if (effectiveTotal <= 0)
        {
            return 1.0;
        }

        return EffectiveElapsedSeconds(s, totalSeconds, elapsedSeconds) / effectiveTotal;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="current"/> is outside the work window
    /// and closer to (or exactly at) <see cref="SalarySettings.WorkEnd"/> than to the next
    /// <see cref="SalarySettings.WorkStart"/> — i.e. the most recent shift just finished (show the
    /// full daily salary) rather than the next shift being about to start (show <c>0</c>). Only
    /// meaningful when <paramref name="current"/> is already known to be outside the work window.
    /// </summary>
    private static bool IsPastWorkEnd(SalarySettings s, TimeOnly current)
    {
        var sinceEnd = (current - s.WorkEnd).TotalSeconds;
        var untilStart = (s.WorkStart - current).TotalSeconds;
        return sinceEnd <= untilStart;
    }

    /// <summary>
    /// Returns the lunch break's start/end as offsets in seconds from <see cref="SalarySettings.WorkStart"/>
    /// (so they naturally wrap for an overnight work window), or <see langword="false"/> if the break
    /// is disabled, crosses midnight itself, or doesn't fall entirely inside the work window.
    /// </summary>
    private static bool TryGetLunchBreakOffsets(SalarySettings s, double totalSeconds, out double startOffset, out double endOffset)
    {
        startOffset = 0;
        endOffset = 0;

        if (!s.LunchBreakEnabled)
        {
            return false;
        }

        var breakStart = s.LunchBreakStart;
        var breakEnd = s.LunchBreakEnd;
        if (breakEnd <= breakStart)
        {
            // The lunch break itself isn't supported crossing midnight.
            return false;
        }

        startOffset = (breakStart - s.WorkStart).TotalSeconds;
        endOffset = (breakEnd - s.WorkStart).TotalSeconds;
        return startOffset < endOffset && endOffset <= totalSeconds;
    }

    /// <summary>
    /// Returns <paramref name="elapsedSeconds"/> (seconds elapsed since <see cref="SalarySettings.WorkStart"/>)
    /// with any time spent inside a valid lunch break excluded (elapsed holds steady during the break).
    /// </summary>
    private static double EffectiveElapsedSeconds(SalarySettings s, double totalSeconds, double elapsedSeconds)
    {
        if (!TryGetLunchBreakOffsets(s, totalSeconds, out var startOffset, out var endOffset))
        {
            return elapsedSeconds;
        }

        if (elapsedSeconds <= startOffset)
        {
            return elapsedSeconds;
        }
        if (elapsedSeconds < endOffset)
        {
            return startOffset;
        }

        return elapsedSeconds - (endOffset - startOffset);
    }

    /// <summary>
    /// Returns <paramref name="totalSeconds"/> (<c>WorkEnd - WorkStart</c>) minus the lunch break
    /// duration when enabled and valid.
    /// </summary>
    private static double EffectiveWorkSeconds(SalarySettings s, double totalSeconds)
    {
        if (!TryGetLunchBreakOffsets(s, totalSeconds, out var startOffset, out var endOffset))
        {
            return totalSeconds;
        }

        return totalSeconds - (endOffset - startOffset);
    }
}
