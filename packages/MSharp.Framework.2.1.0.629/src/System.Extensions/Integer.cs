using System.Runtime.CompilerServices;

namespace System
{
    partial class MSharpExtensions
    {
        /// <summary>
        /// Rounds up to nearest value with the intervals specified.
        /// </summary>
        public static int RoundUpToNearest(this int value, int roundIntervals)
        {
            var difference = roundIntervals - (value % roundIntervals);
            return value + difference;
        }

        /// <summary>
        /// Rounds down to nearest value with the intervals specified.
        /// </summary>
        public static int RoundDownToNearest(this int value, int roundIntervals)
        {
            return value - (value % roundIntervals);
        }

        /// <summary>
        /// Converts this number to a short textual representation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Base32Integer ToBase32(this int value)
        {
            return new Base32Integer(value);
        }


        public static string ToString(this int? value, string format)
        {
            return ("{0:" + format + "}").FormatWith(value);
        }

        /// <summary>
        /// To the word string.
        /// </summary>
        /// <remarks>
        /// Some awesome code from http://stackoverflow.com/questions/2729752/converting-numbers-in-to-words-c-sharp
        /// </remarks>
        /// <param name="number">The number.</param>
        public static string ToWordString(this int number)
        {
            if (number == 0)
                return "zero";

            if (number < 0)
                return "minus " + ToWordString(Math.Abs(number));

            string words = "";

            if ((number / 1000000) > 0)
            {
                words += ToWordString(number / 1000000) + " million ";
                number %= 1000000;
            }

            if ((number / 1000) > 0)
            {
                words += ToWordString(number / 1000) + " thousand ";
                number %= 1000;
            }

            if ((number / 100) > 0)
            {
                words += ToWordString(number / 100) + " hundred ";
                number %= 100;
            }

            if (number > 0)
            {
                if (words != "")
                    words += "and ";

                if (number < 20)
                    words += NumberWordsUnits[number];
                else
                {
                    words += NumberWordsTens[number / 10];
                    if ((number % 10) > 0)
                        words += "-" + NumberWordsUnits[number % 10];
                }
            }

            return words;
        }

        /// <summary>
        /// Emits a user readable file size (including units).
        /// </summary>
        public static string ToFileSizeString(this long fileSize, string units, int round)
        {
            if ("MB".Equals(units, StringComparison.OrdinalIgnoreCase))
                return string.Format("{0:0.0} MB", Math.Round((double)fileSize / 0x100000, round));

            var suffix = new[] { "B", "KB", "MB", "GB", "TB" };
            long index = 0;

            while (fileSize > 0x400 && index < suffix.Length)
            {
                fileSize = fileSize / 0x400;
                index++;
            }

            return string.Concat(fileSize, " ", suffix[index]);
        }

        /// <summary>
        /// Gets the size text for the given number of bytes. E.g. 4.5MB or 11KB.
        /// </summary>
        public static string ToFileSizeString(this int size, int round = 1)
        {
            return ToFileSizeString((long)size, round);
        }

        /// <summary>
        /// Gets the size text for the given number of bytes.
        /// </summary>
        public static string ToFileSizeString(this long size, int round = 1)
        {
            var scale = new[] { "B", "KB", "MB", "GB", "TB" };
            if (size == 0) return "0" + scale[0];

            var sign = Math.Sign(size);
            size = Math.Abs(size);

            var place = Convert.ToInt32(Math.Floor(Math.Log(size, 1024)));
            var num = Math.Round(size / Math.Pow(1024, place), round);

            return (sign * num) + scale[place];
            //return (Math.Sign(size) * num).ToString() + scale[place];
        }

        /// <summary>
        /// Emits a user readable file size (including units).
        /// </summary>
        public static string ToFileSizeString(this int fileSize, string units, int round)
        {
            return ToFileSizeString((long)fileSize, units, round);
        }


        /// <summary>
        /// Return this value as a percentages the of the given total.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="total">The total.</param>
        /// <param name="multiplyBy100">Multiply this by 100.</param>
        /// <param name="roundTo">Rounding decimals to.</param>
        public static double AsPercentageOf(this int value, int total, bool multiplyBy100 = true, int? roundTo = 0)
        {
            return AsPercentageOf((double)value, total, multiplyBy100, roundTo);
        }

        /// <summary> Static mapping array, used by ToWordString for Units. </summary>
        private static readonly string[] NumberWordsUnits = new[]
                                                            {
                                                                "zero", "one", "two", "three", "four", "five", "six",
                                                                "seven", "eight", "nine", "ten",
                                                                "eleven", "twelve", "thirteen", "fourteen", "fifteen",
                                                                "sixteen", "seventeen",
                                                                "eighteen", "nineteen"
                                                            };

        /// <summary> Static mapping array, used by ToWordString for Tens. </summary>
        private static readonly string[] NumberWordsTens = new[]
                                                           {
                                                               "zero", "ten", "twenty", "thirty", "forty", "fifty",
                                                               "sixty", "seventy", "eighty",
                                                               "ninety"
                                                           };


        /// <summary>
        /// E.g. converts 1 to 1st. Or converts 13 to 13th.
        /// </summary>
        public static string ToOrdinal(this int number)
        {
            switch (number % 100)
            {
                case 11:
                case 12:
                case 13:
                    return number.ToString() + "th";
                default:
                    // Other numbers are fine.
                    break;
            }

            switch (number % 10)
            {
                case 1:
                    return number.ToString() + "st";
                case 2:
                    return number.ToString() + "nd";
                case 3:
                    return number.ToString() + "rd";
                default:
                    return number.ToString() + "th";
            }
        }

        /// <summary>
        /// Concerts this integer value to GUID.
        /// </summary>
        public static Guid ToGuid(this int value)
        {
            var bytes = new byte[16];
            BitConverter.GetBytes(value).CopyTo(bytes, 0);
            return new Guid(bytes);
        }
    }
}