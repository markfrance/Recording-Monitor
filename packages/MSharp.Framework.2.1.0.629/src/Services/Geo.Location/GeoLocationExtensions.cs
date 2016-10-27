using System;
using System.ComponentModel;
using MSharp.Framework.Services;

namespace MSharp.Framework
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class GeoLocationExtensions
    {
        const int EARTH_RADIUS = 3963;

        /// <summary>
        /// Gets the geo distance in miles between this and another specified location.
        /// </summary>
        public static double? GetDistance(this IGeoLocation from, IGeoLocation to)
        {
            if (from == null) return null;

            if (to == null) return null;

            var dLat = (to.Latitude - from.Latitude).ToRadians();
            var dLon = (to.Longitude - from.Longitude).ToRadians();

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(from.Latitude.ToRadians()) * Math.Cos(to.Latitude.ToRadians()) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            var result = EARTH_RADIUS * c;

            if (result > 100)
                return result.Round(0);
            else
                return result.Round(1);
        }

        /// <summary>
        /// Gets the geo distance in miles between this located object and a specified location.
        /// </summary>
        public static double? GetDistance(this IGeoLocated from, IGeoLocation to)
        {
            return GetDistance(from.Get(l => l.GetLocation()), to);
        }

        /// <summary>
        /// Gets the geo distance in miles between this location and a specified located object.
        /// </summary>
        public static double? GetDistance(this IGeoLocation from, IGeoLocated to)
        {
            return GetDistance(from, to.Get(l => l.GetLocation()));
        }

        /// <summary>
        /// Gets the geo distance in miles between this and another specified located object.
        /// </summary>
        public static double? GetDistance(this IGeoLocated from, IGeoLocated to)
        {
            return GetDistance(from.Get(l => l.GetLocation()), to.Get(l => l.GetLocation()));
        }
    }
}