namespace MSharp.Framework.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Xml.Linq;

    /// <summary>
    /// Provides location services.
    /// </summary>
    public class GeoLocationService
    {
        public static string GoogleClientKey = Config.Get("Google.Maps.Api.Client.Key");
        public static string GoogleSignatureKey = Config.Get("Google.Maps.Api.Signature");

        static ConcurrentDictionary<string, GeoLocation> CachedLocations = new ConcurrentDictionary<string, GeoLocation>();

        /// <summary>
        ///  Gets the Geo Location of a specified postcode using Google API.
        ///  This method has daily usage limit of 25000 calls.
        /// </summary>
        public static GeoLocation GetPostcodeLocation(string postcode, string countryCode = "GB")
        {
            var fullAddress = postcode + "," + countryCode;

            return CachedLocations.GetOrAdd(fullAddress, address =>
                {
                    var clientParameter = "key".OnlyWhen(GoogleSignatureKey.IsEmpty()).Or("client");
                   
                    var url = "https://maps.googleapis.com/maps/api/geocode/xml?address=" + address + "&sensor=false" +
                        GoogleClientKey.UrlEncode().WithPrefix("&" + clientParameter +"=") +
                        GoogleSignatureKey.UrlEncode().WithPrefix("&signature=");

                    var response = new WebClient().DownloadString(url).To<XElement>();

                    var status = response.GetValue<string>("status");

                    if (status == "ZERO_RESULTS") return null;
                    if (status != "OK") throw new Exception("Google API Error: " + status + "\r\n\r\n" + response);

                    var location = response.Element("result").Get(x => x.Element("geometry")).Get(x => x.Element("location"));

                    if (location == null) throw new Exception("Unexpected result from Google API: \r\n\r\n" + response);

                    return new GeoLocation
                    {
                        Latitude = location.GetValue<string>("lat").To<double>(),
                        Longitude = location.GetValue<string>("lng").To<double>()
                    };
                });
        }

        /// <summary>
        /// Gets the distance between 2 locations.
        /// </summary>
        public static double? CalculateDistance(string postcode1, string postcode2, string countryCode = "GB")
        {
            var location1 = GetPostcodeLocation(postcode1, countryCode);
            if (location1 == null) return null;

            var location2 = GetPostcodeLocation(postcode2, countryCode);
            if (location2 == null) return null;

            return location1.GetDistance(location2);
        }      
    }
}