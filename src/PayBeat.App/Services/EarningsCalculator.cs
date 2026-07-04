using PayBeat.App.Models;

namespace PayBeat.App.Services;

/// <summary>
/// Pure static helper that computes real-time earnings and workday progress from <see cref="SalarySettings"/>.
/// Has no side effects and holds no state.
/// </summary>
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

        if (current <= s.WorkStart)
        {
            return 0m;
        }
        if (current >= s.WorkEnd)
        {
            return s.DailySalary;
        }

        var totalSeconds = EffectiveWorkSeconds(s);
        if (totalSeconds <= 0)
        {
            return s.DailySalary;
        }

        var elapsedSeconds = EffectiveElapsedSeconds(s, current);

        return s.DailySalary * (decimal)(elapsedSeconds / totalSeconds);
    }

    /// <summary>
    /// Returns how much of the workday has elapsed as of <paramref name="now"/>, clamped to
    /// <c>[0, WorkEnd - WorkStart]</c> and holding steady during any lunch break.
    /// </summary>
    /// <param name="s">Current salary settings.</param>
    /// <param name="now">Point in time to evaluate.</param>
    public static TimeSpan Elapsed(SalarySettings s, DateTime now)
    {
        var current = TimeOnly.FromDateTime(now);
        if (current <= s.WorkStart)
        {
            return TimeSpan.Zero;
        }
        if (current >= s.WorkEnd)
        {
            return s.WorkEnd - s.WorkStart;
        }
        return current - s.WorkStart;
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
    /// Returns the per-second earnings rate, accounting for any lunch break deduction.
    /// Returns <c>0</c> when the effective work window has zero duration.
    /// </summary>
    /// <param name="s">Current salary settings.</param>
    public static decimal RatePerSecond(SalarySettings s)
    {
        var totalSeconds = EffectiveWorkSeconds(s);
        return totalSeconds > 0 ? s.DailySalary / (decimal)totalSeconds : 0m;
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
        if (current <= s.WorkStart)
        {
            return s.WorkEnd - s.WorkStart;
        }
        if (current >= s.WorkEnd)
        {
            return TimeSpan.Zero;
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
        if (current <= s.WorkStart)
        {
            return 0.0;
        }
        if (current >= s.WorkEnd)
        {
            return 1.0;
        }

        var totalSeconds = EffectiveWorkSeconds(s);
        if (totalSeconds <= 0)
        {
            return 1.0;
        }

        return EffectiveElapsedSeconds(s, current) / totalSeconds;
    }

    /// <summary>
    /// Returns the seconds elapsed since <see cref="SalarySettings.WorkStart"/> up to <paramref name="current"/>,
    /// with any time spent inside the lunch break excluded (elapsed holds steady during the break).
    /// </summary>
    private static double EffectiveElapsedSeconds(SalarySettings s, TimeOnly current)
    {
        var elapsed = (current - s.WorkStart).TotalSeconds;

        if (!s.LunchBreakEnabled)
        {
            return elapsed;
        }

        var breakStart = s.LunchBreakStart;
        var breakEnd = s.LunchBreakEnd;
        if (breakEnd <= breakStart || breakStart < s.WorkStart || breakEnd > s.WorkEnd)
        {
            return elapsed;
        }

        if (current <= breakStart)
        {
            return elapsed;
        }
        if (current < breakEnd)
        {
            return (breakStart - s.WorkStart).TotalSeconds;
        }

        return elapsed - (breakEnd - breakStart).TotalSeconds;
    }

    /// <summary>
    /// Returns the total effective work duration in seconds, i.e. <c>WorkEnd - WorkStart</c>
    /// minus the lunch break duration when enabled and valid.
    /// </summary>
    private static double EffectiveWorkSeconds(SalarySettings s)
    {
        var total = (s.WorkEnd - s.WorkStart).TotalSeconds;

        if (!s.LunchBreakEnabled)
        {
            return total;
        }

        var breakStart = s.LunchBreakStart;
        var breakEnd = s.LunchBreakEnd;
        if (breakEnd <= breakStart || breakStart < s.WorkStart || breakEnd > s.WorkEnd)
        {
            return total;
        }

        return total - (breakEnd - breakStart).TotalSeconds;
    }
}
