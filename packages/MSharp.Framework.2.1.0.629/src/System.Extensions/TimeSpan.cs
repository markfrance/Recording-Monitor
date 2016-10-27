using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System
{
    partial class MSharpExtensions
    {
        public static TimeSpan Days(this int number)
        {
            return TimeSpan.FromDays(number);
        }

        public static TimeSpan Hours(this int number)
        {
            return TimeSpan.FromHours(number);
        }

        public static TimeSpan Minutes(this int number)
        {
            return TimeSpan.FromMinutes(number);
        }

        public static TimeSpan Seconds(this int number)
        {
            return TimeSpan.FromSeconds(number);
        }

        public static TimeSpan Milliseconds(this int number)
        {
            return TimeSpan.FromMilliseconds(number);
        }

        public static TimeSpan Ticks(this int number)
        {
            return TimeSpan.FromTicks(number);

        }

        /// <summary>
        /// Converts this time to the date time on date of 1900-01-01.
        /// </summary>
        public static DateTime ToDate(this TimeSpan time) => new DateTime(1900, 1, 1).Add(time);

        /// <summary>
        /// Converts this time to the date time on date of 1900-01-01.
        /// </summary>
        public static DateTime? ToDate(this TimeSpan? time) => time?.ToDate();

        /// <summary>
        /// Gets the natural text for this timespan. For example "2 days, 4 hours and 3 minutes".
        /// </summary>
        public static string ToNaturalTime(this TimeSpan period)
        {
            return ToNaturalTime(period, longForm: true);
        }

        public static string ToNaturalTime(this TimeSpan period, bool longForm)
        {
            return ToNaturalTime(period, 2, longForm);
        }

        public static string ToNaturalTime(this TimeSpan period, int precisionParts)
        {
            return ToNaturalTime(period, precisionParts, longForm: true);
        }

        /// <summary>
        /// Gets the natural text for this timespan. For example "2 days, 4 hours and 3 minutes".
        /// </summary>
        public static string ToNaturalTime(this TimeSpan period, int precisionParts, bool longForm)
        {
            // TODO: Support months and years.
            // Hint: Assume the timespan shows a time in the past of NOW. Count years and months from there.
            //          i.e. count years and go back. Then count months and go back...

            var names = new Dictionary<string, string> { { "year", "y" }, { "month", "M" }, { "week", "w" }, { "day", "d" }, { "hour", "h" }, { "minute", "m" }, { "second", "s" }, { " and ", " " }, { ", ", " " } };

            Func<string, string> name = (k) => (longForm) ? k : names[k];

            var parts = new Dictionary<string, double>();

            if (period.TotalDays >= 365)
            {
                var years = (int)Math.Floor(period.TotalDays / 365);
                parts.Add(name("year"), years);
                period -= TimeSpan.FromDays(365 * years);
            }

            if (period.TotalDays >= 30)
            {
                var months = (int)Math.Floor(period.TotalDays / 30);
                parts.Add(name("month"), months);
                period -= TimeSpan.FromDays(30 * months);
            }

            if (period.TotalDays >= 7)
            {
                var weeks = (int)Math.Floor(period.TotalDays / 7);
                parts.Add(name("week"), weeks);
                period -= TimeSpan.FromDays(7 * weeks);
            }

            if (period.TotalDays >= 1)
            {
                parts.Add(name("day"), period.Days);
                period -= TimeSpan.FromDays(period.Days);
            }

            if (period.TotalHours >= 1 && period.Hours > 0)
            {
                parts.Add(name("hour"), period.Hours);
                period = period.Subtract(TimeSpan.FromHours(period.Hours));
            }

            if (period.TotalMinutes >= 1 && period.Minutes > 0)
            {
                parts.Add(name("minute"), period.Minutes);
                period = period.Subtract(TimeSpan.FromMinutes(period.Minutes));
            }

            if (period.TotalSeconds >= 1 && period.Seconds > 0)
            {
                parts.Add(name("second"), period.Seconds);
                period = period.Subtract(TimeSpan.FromSeconds(period.Seconds));
            }

            else if (period.TotalSeconds > 0)
            {
                parts.Add(name("second"), period.TotalSeconds.Round(3));
                period = TimeSpan.Zero;
            }

            var outputParts = parts.Take(precisionParts);
            var r = new StringBuilder();

            foreach (var part in outputParts)
            {
                r.Append(part.Value);

                if (longForm)
                    r.Append(" ");

                r.Append(part.Key);

                if (part.Value > 1 && longForm) r.Append("s");

                if (outputParts.IndexOf(part) == outputParts.Count() - 2)
                    r.Append(name(" and "));
                else if (outputParts.IndexOf(part) < outputParts.Count() - 2)
                    r.Append(name(", "));
            }

            return r.ToString();
        }

        public static string ToString(this TimeSpan? value, string format)
        {
            return ("{0:" + format + "}").FormatWith(value);
        }
    }
}
