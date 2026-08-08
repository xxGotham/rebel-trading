namespace Rebel.Core.Market;

/// <summary>
/// US equity session calendar. Holidays are computed by rule rather than kept as
/// a hardcoded list, so the calendar does not silently expire at the end of a
/// year and start treating Thanksgiving as a trading day.
/// </summary>
public static class MarketCalendar
{
    public static readonly TimeZoneInfo Eastern = ResolveEastern();

    public static readonly TimeOnly RegularOpen = new(9, 30);
    public static readonly TimeOnly RegularClose = new(16, 0);
    public static readonly TimeOnly EarlyClose = new(13, 0);
    public static readonly TimeOnly PreMarketOpen = new(4, 0);

    private static TimeZoneInfo ResolveEastern()
    {
        // "America/New_York" resolves on Linux and on Windows via ICU; the Windows
        // id is the fallback for machines running with globalization invariant data.
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        throw new InvalidOperationException("No US Eastern time zone available on this machine.");
    }

    public static DateTime ToEastern(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            Eastern);

    public static DateTime ToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified),
            Eastern);

    public static DateOnly CurrentTradingDate(DateTime utcNow)
    {
        var eastern = ToEastern(utcNow);
        var date = DateOnly.FromDateTime(eastern);

        // After the close, "today" is done — the next actionable session is the next open day.
        if (TimeOnly.FromDateTime(eastern) >= RegularClose && IsTradingDay(date))
            return NextTradingDay(date);

        return IsTradingDay(date) ? date : NextTradingDay(date);
    }

    public static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static bool IsTradingDay(DateOnly date) => !IsWeekend(date) && !IsHoliday(date);

    public static DateOnly NextTradingDay(DateOnly date)
    {
        var d = date.AddDays(1);
        while (!IsTradingDay(d)) d = d.AddDays(1);
        return d;
    }

    public static DateOnly PreviousTradingDay(DateOnly date)
    {
        var d = date.AddDays(-1);
        while (!IsTradingDay(d)) d = d.AddDays(-1);
        return d;
    }

    /// <summary>Number of sessions between two dates, exclusive of <paramref name="from"/>.</summary>
    public static int TradingDaysBetween(DateOnly from, DateOnly to)
    {
        if (to <= from) return 0;
        var count = 0;
        for (var d = from.AddDays(1); d <= to; d = d.AddDays(1))
            if (IsTradingDay(d)) count++;
        return count;
    }

    public static DateOnly AddTradingDays(DateOnly date, int days)
    {
        var d = date;
        for (var i = 0; i < days; i++) d = NextTradingDay(d);
        return d;
    }

    public static TimeOnly CloseTimeFor(DateOnly date) =>
        IsEarlyClose(date) ? EarlyClose : RegularClose;

    /// <summary>Half sessions: July 3rd, the Friday after Thanksgiving, Christmas Eve.</summary>
    public static bool IsEarlyClose(DateOnly date)
    {
        if (!IsTradingDay(date)) return false;

        if (date is { Month: 7, Day: 3 }) return true;
        if (date is { Month: 12, Day: 24 }) return true;
        if (date.Month == 11 && date.DayOfWeek == DayOfWeek.Friday &&
            date.AddDays(-1) == NthWeekdayOfMonth(date.Year, 11, DayOfWeek.Thursday, 4))
            return true;

        return false;
    }

    public static bool IsHoliday(DateOnly date)
    {
        var year = date.Year;

        if (date == Observed(new DateOnly(year, 1, 1))) return true;                       // New Year's Day
        if (date == NthWeekdayOfMonth(year, 1, DayOfWeek.Monday, 3)) return true;          // MLK Jr.
        if (date == NthWeekdayOfMonth(year, 2, DayOfWeek.Monday, 3)) return true;          // Presidents' Day
        if (date == GoodFriday(year)) return true;
        if (date == LastWeekdayOfMonth(year, 5, DayOfWeek.Monday)) return true;            // Memorial Day
        if (year >= 2022 && date == Observed(new DateOnly(year, 6, 19))) return true;      // Juneteenth
        if (date == Observed(new DateOnly(year, 7, 4))) return true;                       // Independence Day
        if (date == NthWeekdayOfMonth(year, 9, DayOfWeek.Monday, 1)) return true;          // Labor Day
        if (date == NthWeekdayOfMonth(year, 11, DayOfWeek.Thursday, 4)) return true;       // Thanksgiving
        if (date == Observed(new DateOnly(year, 12, 25))) return true;                     // Christmas

        // New Year's Day falling on a Saturday is observed on the prior Dec 31.
        if (date is { Month: 12, Day: 31 } && new DateOnly(year + 1, 1, 1).DayOfWeek == DayOfWeek.Saturday)
            return true;

        return false;
    }

    /// <summary>Weekend holidays shift: Saturday to the Friday before, Sunday to the Monday after.</summary>
    private static DateOnly Observed(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Saturday => date.AddDays(-1),
        DayOfWeek.Sunday => date.AddDays(1),
        _ => date
    };

    private static DateOnly NthWeekdayOfMonth(int year, int month, DayOfWeek weekday, int n)
    {
        var first = new DateOnly(year, month, 1);
        var offset = ((int)weekday - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(offset + 7 * (n - 1));
    }

    private static DateOnly LastWeekdayOfMonth(int year, int month, DayOfWeek weekday)
    {
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var offset = ((int)last.DayOfWeek - (int)weekday + 7) % 7;
        return last.AddDays(-offset);
    }

    /// <summary>Anonymous Gregorian algorithm for Easter, minus two days.</summary>
    private static DateOnly GoodFriday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day).AddDays(-2);
    }
}
