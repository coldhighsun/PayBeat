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
    /// between <see cref="SalarySettings.WorkStart"/> and <see cref="SalarySettings.WorkEnd"/>.
    /// Returns <c>0</c> before work starts and <see cref="SalarySettings.DailySalary"/> after work ends.
    /// </summary>
    /// <param name="s">Current salary settings.</param>
    /// <param name="now">Point in time to evaluate.</param>
    public static decimal Calculate(SalarySettings s, DateTime now)
    {
        var current = TimeOnly.FromDateTime(now);

        if (current <= s.WorkStart)
        {
            return 0m;
        }
        if (current >= s.WorkEnd)
        {
            return s.DailySalary;
        }

        var totalSeconds = (s.WorkEnd - s.WorkStart).TotalSeconds;
        var elapsedSeconds = (current - s.WorkStart).TotalSeconds;

        return s.DailySalary * (decimal)(elapsedSeconds / totalSeconds);
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
    /// Returns the per-second earnings rate. Returns <c>0</c> when the work window has zero duration.
    /// </summary>
    /// <param name="s">Current salary settings.</param>
    public static decimal RatePerSecond(SalarySettings s)
    {
        var totalSeconds = (s.WorkEnd - s.WorkStart).TotalSeconds;
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
    /// Returns a value in [0.0, 1.0] representing how far through the workday <paramref name="now"/> is.
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
        return (current - s.WorkStart).TotalSeconds /
               (s.WorkEnd - s.WorkStart).TotalSeconds;
    }
}