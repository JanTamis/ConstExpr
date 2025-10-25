using ConstExpr.Core.Attributes;
using System;

namespace ConstExpr.SourceGenerator.Sample;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class DateTimeOperations
{
	public static int DaysBetweenDates(int year1, int month1, int day1, int year2, int month2, int day2)
	{
		var date1 = new DateTime(year1, month1, day1);
		var date2 = new DateTime(year2, month2, day2);
		return Math.Abs((date2 - date1).Days);
	}

	// Additional date/time operations
	public static bool IsLeapYear(int year)
	{
		return (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
	}

	public static int DaysInMonth(int year, int month)
	{
		return DateTime.DaysInMonth(year, month);
	}

	public static int DaysInYear(int year)
	{
		return IsLeapYear(year) ? 366 : 365;
	}

	public static int GetWeekNumber(int year, int month, int day)
	{
		var date = new DateTime(year, month, day);
		var dayOfYear = date.DayOfYear;
		var firstDayOfYear = new DateTime(year, 1, 1);
		var daysOffset = (int)firstDayOfYear.DayOfWeek;

		return (dayOfYear + daysOffset - 1) / 7 + 1;
	}

	public static DayOfWeek GetDayOfWeek(int year, int month, int day)
	{
		var date = new DateTime(year, month, day);
		return date.DayOfWeek;
	}

	public static int GetQuarter(int month)
	{
		if (month < 1 || month > 12)
		{
			throw new ArgumentException("Month must be between 1 and 12");
		}

		return (month - 1) / 3 + 1;
	}

	public static DateTime AddBusinessDays(int year, int month, int day, int businessDays)
	{
		var date = new DateTime(year, month, day);
		var direction = businessDays < 0 ? -1 : 1;
		var daysToAdd = Math.Abs(businessDays);

		while (daysToAdd > 0)
		{
			date = date.AddDays(direction);

			if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
			{
				daysToAdd--;
			}
		}

		return date;
	}

	public static int CountBusinessDays(int year1, int month1, int day1, int year2, int month2, int day2)
	{
		var startDate = new DateTime(year1, month1, day1);
		var endDate = new DateTime(year2, month2, day2);

		if (startDate > endDate)
		{
			(startDate, endDate) = (endDate, startDate);
		}

		var businessDays = 0;

		for (var date = startDate; date <= endDate; date = date.AddDays(1))
		{
			if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
			{
				businessDays++;
			}
		}

		return businessDays;
	}

	public static int GetAge(int birthYear, int birthMonth, int birthDay, int currentYear, int currentMonth, int currentDay)
	{
		var age = currentYear - birthYear;

		if (currentMonth < birthMonth || (currentMonth == birthMonth && currentDay < birthDay))
		{
			age--;
		}

		return age;
	}

	public static bool IsDateValid(int year, int month, int day)
	{
		if (year < 1 || year > 9999)
		{
			return false;
		}

		if (month < 1 || month > 12)
		{
			return false;
		}

		if (day < 1)
		{
			return false;
		}

		return day <= DaysInMonth(year, month);
	}

	public static DateTime GetEasterSunday(int year)
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
		var day = ((h + l - 7 * m + 114) % 31) + 1;

		return new DateTime(year, month, day);
	}

	public static long UnixTimestamp(int year, int month, int day, int hour, int minute, int second)
	{
		var date = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
		var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		return (long)(date - epoch).TotalSeconds;
	}

	public static DateTime FromUnixTimestamp(long timestamp)
	{
		var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		return epoch.AddSeconds(timestamp);
	}

	public static int DayOfYear(int year, int month, int day)
	{
		var date = new DateTime(year, month, day);
		return date.DayOfYear;
	}
}

