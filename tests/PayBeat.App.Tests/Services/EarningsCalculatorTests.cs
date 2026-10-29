using AwesomeAssertions;
using PayBeat.App.Models;
using PayBeat.App.Services;

namespace PayBeat.App.Tests.Services;

/// <summary>
/// Tests for <see cref="EarningsCalculator"/>.
/// </summary>
public class EarningsCalculatorTests
{
    /// <summary>
    /// A Wednesday, so weekend gating does not interfere.
    /// </summary>
    private static readonly DateTime Wednesday = new(2026, 1, 7);

    /// <summary>
    /// A Saturday used for weekend tests.
    /// </summary>
    private static readonly DateTime Saturday = new(2026, 1, 10);

    /// <summary>
    /// Settings with a 09:00-18:00 day and a daily salary of 900 (100 per hour).
    /// </summary>
    private static readonly SalarySettings Standard = new() { DailySalary = 900m };

    /// <summary>
    /// Verifies earnings are zero before the work window opens.
    /// </summary>
    [Fact]
    public void Calculate_BeforeWorkStart_ReturnsZero()
    {
        var result = EarningsCalculator.Calculate(Standard, Wednesday.AddHours(8));

        result.Should().Be(0m);
    }

    /// <summary>
    /// Verifies earnings accrue linearly inside the work window.
    /// </summary>
    [Fact]
    public void Calculate_MidWorkday_ReturnsProportionalAmount()
    {
        var result = EarningsCalculator.Calculate(Standard, Wednesday.AddHours(12));

        result.Should().BeApproximately(300m, 0.0001m);
    }

    /// <summary>
    /// Verifies earnings are capped at the daily salary after work ends.
    /// </summary>
    [Fact]
    public void Calculate_AfterWorkEnd_ReturnsFullSalary()
    {
        var result = EarningsCalculator.Calculate(Standard, Wednesday.AddHours(19));

        result.Should().Be(900m);
    }

    /// <summary>
    /// Verifies weekends earn nothing by default.
    /// </summary>
    [Fact]
    public void Calculate_WeekendWithoutWorkOnWeekends_ReturnsZero()
    {
        var result = EarningsCalculator.Calculate(Standard, Saturday.AddHours(12));

        result.Should().Be(0m);
    }

    /// <summary>
    /// Verifies weekends accrue when <see cref="SalarySettings.WorkOnWeekends"/> is enabled.
    /// </summary>
    [Fact]
    public void Calculate_WeekendWithWorkOnWeekends_Accrues()
    {
        var settings = Standard with { WorkOnWeekends = true };

        var result = EarningsCalculator.Calculate(settings, Saturday.AddHours(12));

        result.Should().BeApproximately(300m, 0.0001m);
    }

    /// <summary>
    /// Verifies elapsed time is held steady during the lunch break.
    /// </summary>
    [Fact]
    public void Calculate_DuringLunchBreak_HoldsSteady()
    {
        var settings = Standard with { LunchBreakEnabled = true };

        var atStart = EarningsCalculator.Calculate(settings, Wednesday.AddHours(12));
        var midBreak = EarningsCalculator.Calculate(settings, Wednesday.AddHours(12).AddMinutes(30));

        midBreak.Should().Be(atStart);
    }

    /// <summary>
    /// Verifies the lunch break is removed from the effective work duration.
    /// </summary>
    [Fact]
    public void RatePerSecond_WithLunchBreak_UsesEffectiveSeconds()
    {
        var settings = Standard with { LunchBreakEnabled = true };

        var rate = EarningsCalculator.RatePerSecond(settings);

        rate.Should().BeApproximately(900m / (8 * 3600), 0.000001m);
    }

    /// <summary>
    /// Verifies overnight shifts wrap around midnight.
    /// </summary>
    [Fact]
    public void Calculate_OvernightShiftAfterMidnight_AccruesAcrossMidnight()
    {
        var settings = new SalarySettings
        {
            DailySalary = 800m,
            WorkStart = new TimeOnly(22, 0),
            WorkEnd = new TimeOnly(6, 0),
        };

        var result = EarningsCalculator.Calculate(settings, Wednesday.AddHours(2));

        result.Should().BeApproximately(400m, 0.0001m);
    }

    /// <summary>
    /// Verifies progress is 0 before and 1 after the workday.
    /// </summary>
    /// <param name="hour">Hour of day to evaluate.</param>
    /// <param name="expected">Expected progress value.</param>
    [Theory]
    [InlineData(8, 0.0)]
    [InlineData(19, 1.0)]
    public void WorkdayProgress_OutsideWindow_ClampsToBounds(int hour, double expected)
    {
        var result = EarningsCalculator.WorkdayProgress(Standard, Wednesday.AddHours(hour));

        result.Should().Be(expected);
    }

    /// <summary>
    /// Verifies a lunch break outside the work window is reported invalid.
    /// </summary>
    [Fact]
    public void IsLunchBreakValid_BreakOutsideWorkWindow_ReturnsFalse()
    {
        var settings = Standard with
        {
            LunchBreakEnabled = true,
            LunchBreakStart = new TimeOnly(19, 0),
            LunchBreakEnd = new TimeOnly(20, 0),
        };

        EarningsCalculator.IsLunchBreakValid(settings).Should().BeFalse();
    }

    /// <summary>
    /// Verifies remaining time is zero after work end.
    /// </summary>
    [Fact]
    public void Remaining_AfterWorkEnd_ReturnsZero()
    {
        var result = EarningsCalculator.Remaining(Standard, Wednesday.AddHours(19));

        result.Should().Be(TimeSpan.Zero);
    }
}
