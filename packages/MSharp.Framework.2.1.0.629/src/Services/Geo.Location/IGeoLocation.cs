namespace MSharp.Framework.Services
{
    public interface IGeoLocation
    {
        double Longitude { get; }
        double Latitude { get; }        
    }

    public class GeoLocation : IGeoLocation
    {
        public double Longitude { get; set; }
        public double Latitude { get; set; }
    }
}
