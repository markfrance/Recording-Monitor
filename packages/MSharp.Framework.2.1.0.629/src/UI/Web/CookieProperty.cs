namespace MSharp.Framework.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web;

    /// <summary>
    /// Provides easy access to HTTP cookie data.
    /// </summary>
    public class CookieProperty
    {
        const string BAR_SCAPE = "[#*^BAR_SCAPE^*#]";

        /// <summary>
        /// Gets the value of the property sent from the client browser as a cookie.
        /// </summary>
        public static T Get<T>()
        {
            return Get<T>(null, default(T));
        }

        /// <summary>
        /// Gets the value of a string property sent from the client browser as a cookie.
        /// </summary>
        public static string Get(string key)
        {
            return Get<string>(key, null);
        }

        /// <summary>
        /// Gets the value of the property sent from the client browser as a cookie.
        /// </summary>
        public static T Get<T>(T defaultValue)
        {
            return Get<T>(null, defaultValue);
        }

        /// <summary>
        /// Gets the value of the property sent from the client browser as a cookie.
        /// </summary>
        public static T Get<T>(string propertyName)
        {
            return Get<T>(propertyName, default(T));
        }

        public static IEnumerable<string> GetStrings(string propertyName)
        {
            return Get<IEnumerable<string>>(propertyName, null);
        }

        /// <summary>
        /// Gets the value of the property sent from the client browser as a cookie.
        /// </summary>
        public static T Get<T>(string propertyName, T defaultValue)
        {
            var key = propertyName.Or("Default.Value.For." + typeof(T).FullName);

            var cookie = HttpContext.Current?.Request.Cookies[key];

            if (cookie == null)
            {
                return defaultValue;
            }
            else if (typeof(T).Implements<IEntity>())
            {
                //return (T)(object)Database.GetOrDefault(cookie.Value, typeof(T));

                var id = cookie.Value.Contains('/') ? cookie.Value.Split('/')[1] : cookie.Value; // Remove class name prefix if exists
                return (T)(object)Database.GetOrDefault(id, typeof(T));
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)cookie.Value;
            }
            else if (typeof(T) == typeof(IEnumerable<string>) || typeof(T) == typeof(string[]))
            {
                return (T)(object)cookie.Value.Or("").Split('|').Trim().Select(p => p.Replace(BAR_SCAPE, "|")).ToArray();
            }
            else if (typeof(T).Namespace.StartsWith("System"))
            {
                return (T)cookie.Value.To(typeof(T));
            }

            throw new Exception("CookieProperty.Get<T>() does not support T type of " + typeof(T).FullName);
        }

        /// <summary>
        /// Sets a specified value in the response cookie as well as request cookie.
        /// </summary>
        /// <param name="isHttpOnly">Specifies whether the cookie should be accessible via Javascript too, or Server (http) only.</param>
        public static HttpCookie Set<T>(T value, bool isHttpOnly = true)
        {
            return Set<T>(null, value, isHttpOnly);
        }

        /// <summary>
        /// Sets a specified value in the response cookie as well as request cookie.
        /// </summary>
        /// <param name="isHttpOnly">Specifies whether the cookie should be accessible via Javascript too, or Server (http) only.</param>
        public static HttpCookie Set<T>(string propertyName, T value, bool isHttpOnly = true)
        {
            var key = propertyName.Or("Default.Value.For." + typeof(T).FullName);

            var stringValue = value?.ToString();

            if (value is IEntity) stringValue = (value as IEntity).GetFullIdentifierString();
            return Set(key, stringValue, isHttpOnly);
        }

        /// <summary>
        /// Sets a specified list in the response cookie as well as request cookie.
        /// </summary>
        /// <param name="isHttpOnly">Specifies whether the cookie should be accessible via Javascript too, or Server (http) only.</param>
        public static HttpCookie SetList<T>(string propertyName, IEnumerable<T> list, bool isHttpOnly = true) where T : IEntity
        {
            var key = propertyName.Or("Default.List.For." + typeof(T).FullName);

            if (list == null)
            {
                return Set(key, string.Empty, isHttpOnly);
            }
            else
            {
                var stringValue = list.Except(n => n == null).Select(i => i.GetFullIdentifierString()).ToString("|");
                return Set(key, stringValue, isHttpOnly);
            }
        }

        /// <summary>
        /// Sets a specified list in the response cookie as well as request cookie.
        /// </summary>
        public static IEnumerable<T> GetList<T>() where T : IEntity
        {
            return GetList<T>(null);
        }

        /// <summary>
        /// Gets a specified list in the response cookie as well as request cookie.
        /// </summary>
        public static T[] GetList<T>(string propertyName) where T : IEntity
        {
            var key = propertyName.Or("Default.List.For." + typeof(T).FullName);

            var result = Get(key);
            if (result.IsEmpty())
                return new T[0];

            return result.Split('|').Select(x => ExtractItem<T>(x)).Except(n => n == null).ToArray();
        }

        static T ExtractItem<T>(string valueExpression) where T : IEntity
        {
            var id = valueExpression.Contains('/') ? valueExpression.Split('/')[1] : valueExpression; // Remove class name prefix if exists
            return (T)(object)Database.GetOrDefault(id, typeof(T));
        }

        /// <summary>
        /// Removes the specified cookie property.
        /// </summary>
        public static void Remove<T>()
        {
            Set<T>(default(T));
        }

        /// <summary>
        /// Removes the specified cookie property.
        /// </summary>
        public static void Remove<T>(string propertyName)
        {
            Set<T>(propertyName, default(T));
        }

        /// <summary>
        /// Removes the specified cookie property.
        /// </summary>
        public static void Remove(string propertyName)
        {
            Set(propertyName, (string)null);
        }

        /// <summary>
        /// Sets a specified value in the response cookie as well as request cookie.
        /// </summary>
        /// <param name="isHttpOnly">Specifies whether the cookie should be accessible via Javascript too, or Server (http) only.</param>
        public static HttpCookie Set(string propertyName, IEnumerable<string> strings, bool isHttpOnly = true)
        {
            strings = strings ?? new string[0];
            return Set(propertyName, strings.Trim().Select(s => s.Replace("|", BAR_SCAPE)).ToString("|"), isHttpOnly);
        }

        /// <summary>
        /// Sets a specified value in the response cookie as well as request cookie.
        /// </summary>
        /// <param name="isHttpOnly">Specifies whether the cookie should be accessible via Javascript too, or Server (http) only.</param>
        public static HttpCookie Set(string key, string value, bool isHttpOnly = true)
        {
            if (key.IsEmpty())
                throw new ArgumentNullException("key");

            var cookies = HttpContext.Current?.Response?.Cookies;

            if (cookies == null)
                return null;

            var result = new HttpCookie(key, value) { Expires = DateTime.Now.AddYears(10), Secure = HttpContext.Current.Request.IsHttps(), HttpOnly = isHttpOnly };
            cookies.Add(result);

            if (value == null)
            {
                result.Expires = DateTime.Now.AddYears(-10);
                HttpContext.Current.Request.Cookies.Remove(key);
            }
            else
            {
                var requestCookie = HttpContext.Current.Request.Cookies[key];
                if (requestCookie == null)
                    HttpContext.Current.Request.Cookies.Add(result);
                else requestCookie.Value = result.Value;
            }

            return result;
        }
    }
}