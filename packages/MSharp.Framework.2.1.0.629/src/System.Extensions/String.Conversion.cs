namespace System
{
    using System.Drawing;
    using System.Globalization;
    using System.Linq;
    using MSharp.Framework;
    partial class MSharpExtensions
    {
        /// <summary>
        /// Determines whether this string can be converted to the specified type.
        /// </summary>
        public static bool Is<T>(this string text) where T : struct
        {
            return text.TryParseAs<T>().HasValue;
        }

        /// <summary>
        /// Tries to parse this text to the specified type.
        /// Returns null if parsing is not possible.
        /// </summary>
        public static T? TryParseAs<T>(this string text) where T : struct
        {
            if (text.IsEmpty()) return default(T?);

            // Check common types first, for performance:
            if (typeof(T) == typeof(int))
            {
                int result; if (int.TryParse(text, out result)) return (T)(object)result; else return null;
            }

            if (typeof(T) == typeof(double))
            {
                double result; if (double.TryParse(text, out result)) return (T)(object)result; else return null;
            }

            if (typeof(T) == typeof(decimal))
            {
                decimal result; if (decimal.TryParse(text, out result)) return (T)(object)result; else return null;
            }

            if (typeof(T) == typeof(bool))
            {
                bool result; if (bool.TryParse(text, out result)) return (T)(object)result; else return null;
            }

            if (typeof(T) == typeof(DateTime))
            {
                DateTime result; if (DateTime.TryParse(text, out result)) return (T)(object)result; else return null;
            }

            if (typeof(T) == typeof(TimeSpan))
            {
                TimeSpan result; if (TimeSpan.TryParse(text, out result)) return (T)(object)result; else return null;
            }

            if (typeof(T) == typeof(Guid))
            {
                Guid result; if (Guid.TryParse(text, out result)) return (T)(object)result; else return null;
            }

            if (typeof(T).IsEnum)
            {
                T result; return Enum.TryParse(text, ignoreCase: true, result: out result) ? (T?)result : null;
            }

            if (typeof(T) == typeof(ShortGuid))
                try { return (T)(object)ShortGuid.Parse(text); }
                catch
                {
                    return null;
                    // No logging is needed
                }

            if (typeof(T).IsA<IEntity>()) return (T)Database.GetOrDefault(text, typeof(T));

            try { return (T)Convert.ChangeType(text, typeof(T)); }
            catch { return null; }
        }


        /// <summary>
        /// It converts this text to the specified data type. 
        /// It supports all primitive types, Enums, Guid, XElement, XDocument, Color, ...
        /// </summary>
        public static T To<T>(this string text)
        {
            return (T)To(text, typeof(T));
        }

        /// <summary>
        /// Converts the value of this string object into the specified target type.
        /// It supports all primitive types, Enums, Guid, XElement, XDocument, Color, ...
        /// </summary>
        public static object To(this string text, Type targetType)
        {
            try
            {
                return ChangeType(text, targetType);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not convert \"{text}\" to type { targetType.FullName}.", ex);
            }
        }

        static object ChangeType(string text, Type targetType)
        {
            if (targetType == typeof(string)) return text;

            if (text.IsEmpty())
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (targetType.IsNullable())
                targetType = targetType.GetGenericArguments().Single();

            // Check common types first, for performance:
            if (targetType == typeof(int)) return int.Parse(text);
            if (targetType == typeof(long)) return long.Parse(text);
            if (targetType == typeof(double)) return double.Parse(text);
            if (targetType == typeof(decimal)) return decimal.Parse(text);
            if (targetType == typeof(bool)) return bool.Parse(text);
            if (targetType == typeof(DateTime)) return DateTime.Parse(text);
            if (targetType == typeof(Guid)) return new Guid(text);
            if (targetType == typeof(TimeSpan))
            {
                if (text.Is<long>()) return TimeSpan.FromTicks(text.To<long>());
                else return TimeSpan.Parse(text);
            }

            if (targetType.IsEnum) return Enum.Parse(targetType, text);

            if (targetType == typeof(Xml.Linq.XElement)) return Xml.Linq.XElement.Parse(text);

            if (targetType == typeof(Xml.Linq.XDocument)) return Xml.Linq.XDocument.Parse(text);

            if (targetType == typeof(ShortGuid)) return ShortGuid.Parse(text);

            if (targetType.IsA<IEntity>()) return Database.GetOrDefault(text, targetType);

            if (targetType == typeof(Color))
            {
                if (!text.StartsWith("#") || text.Length != 7)
                    throw new Exception("Invalid color text. Expected format is #RRGGBB.");

                return Color.FromArgb(int.Parse(text.TrimStart("#").WithPrefix("FF"), NumberStyles.HexNumber));
            }

            return Convert.ChangeType(text, targetType);
        }
    }
}
