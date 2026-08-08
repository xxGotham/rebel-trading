using Rebel.Core.Market;
using Xunit;

namespace Rebel.Core.Tests;

public class MarketCalendarTests
{
    [Theory]
    [InlineData(2026, 1, 1)]    // New Year's Day
    [InlineData(2026, 1, 19)]   // MLK Jr. Day
    [InlineData(2026, 2, 16)]   // Presidents' Day
    [InlineData(2026, 4, 3)]    // Good Friday
    [InlineData(2026, 5, 25)]   // Memorial Day
    [InlineData(2026, 6, 19)]   // Juneteenth
    [InlineData(2026, 9, 7)]    // Labor Day
    [InlineData(2026, 11, 26)]  // Thanksgiving
    [InlineData(2026, 12, 25)]  // Christmas
    public void Market_holidays_are_computed_by_rule(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);

        Assert.True(MarketCalendar.IsHoliday(date));
        Assert.False(MarketCalendar.IsTradingDay(date));
    }

    [Fact]
    public void Weekend_holidays_shift_to_the_adjacent_weekday()
    {
        // July 4th 2026 lands on a Saturday, so the market closes on Friday the 3rd.
        Assert.Equal(DayOfWeek.Saturday, new DateOnly(2026, 7, 4).DayOfWeek);
        Assert.True(MarketCalendar.IsHoliday(new DateOnly(2026, 7, 3)));
        Assert.False(MarketCalendar.IsTradingDay(new DateOnly(2026, 7, 3)));
    }

    [Fact]
    public void Ordinary_weekdays_are_trading_days()
    {
        Assert.True(MarketCalendar.IsTradingDay(new DateOnly(2026, 3, 10)));
        Assert.False(MarketCalendar.IsTradingDay(new DateOnly(2026, 3, 14)));  // Saturday
        Assert.False(MarketCalendar.IsTradingDay(new DateOnly(2026, 3, 15)));  // Sunday
    }

    [Fact]
    public void Next_and_previous_trading_day_step_over_weekends_and_holidays()
    {
        // Friday 2026-12-24 is a half day, the 25th is Christmas, then the weekend.
        Assert.Equal(new DateOnly(2026, 12, 28), MarketCalendar.NextTradingDay(new DateOnly(2026, 12, 24)));
        Assert.Equal(new DateOnly(2026, 12, 24), MarketCalendar.PreviousTradingDay(new DateOnly(2026, 12, 28)));
    }

    [Fact]
    public void Trading_days_between_counts_sessions_not_calendar_days()
    {
        // Monday to the following Monday: four sessions in between plus the Monday itself.
        var from = new DateOnly(2026, 3, 2);
        var to = new DateOnly(2026, 3, 9);

        Assert.Equal(5, MarketCalendar.TradingDaysBetween(from, to));
        Assert.Equal(0, MarketCalendar.TradingDaysBetween(to, from));
    }

    [Fact]
    public void Add_trading_days_skips_non_sessions()
    {
        Assert.Equal(new DateOnly(2026, 3, 9), MarketCalendar.AddTradingDays(new DateOnly(2026, 3, 2), 5));
    }

    [Fact]
    public void Current_trading_date_rolls_forward_after_the_close()
    {
        // 21:00 UTC on Monday 2026-03-02 is 16:00 in New York — the bell.
        var afterClose = new DateTime(2026, 3, 2, 21, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 3, 3), MarketCalendar.CurrentTradingDate(afterClose));

        // 14:00 UTC the same day is 09:00 in New York, still pre-market.
        var preMarket = new DateTime(2026, 3, 2, 14, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 3, 2), MarketCalendar.CurrentTradingDate(preMarket));
    }

    [Fact]
    public void Current_trading_date_skips_a_weekend()
    {
        // Saturday — the next actionable session is Monday.
        var saturday = new DateTime(2026, 3, 7, 15, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 3, 9), MarketCalendar.CurrentTradingDate(saturday));
    }

    [Fact]
    public void Half_sessions_close_early()
    {
        Assert.True(MarketCalendar.IsEarlyClose(new DateOnly(2026, 11, 27)));  // day after Thanksgiving
        Assert.True(MarketCalendar.IsEarlyClose(new DateOnly(2026, 12, 24)));
        Assert.Equal(MarketCalendar.EarlyClose, MarketCalendar.CloseTimeFor(new DateOnly(2026, 12, 24)));
        Assert.Equal(MarketCalendar.RegularClose, MarketCalendar.CloseTimeFor(new DateOnly(2026, 3, 10)));
    }

    [Fact]
    public void Eastern_conversion_handles_daylight_saving()
    {
        // 2026-03-08 is the DST switch: 14:30 UTC is 09:30 EDT, not 09:30 EST.
        var beforeDst = MarketCalendar.ToEastern(new DateTime(2026, 3, 2, 14, 30, 0, DateTimeKind.Utc));
        var afterDst = MarketCalendar.ToEastern(new DateTime(2026, 3, 9, 13, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new TimeOnly(9, 30), TimeOnly.FromDateTime(beforeDst));
        Assert.Equal(new TimeOnly(9, 30), TimeOnly.FromDateTime(afterDst));
    }
}
