using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ClassLibrary_Core.Common
{
    public class GPSPosition
    {
        [JsonPropertyName("latitude_x")]
        public double Latitude_x { get; set; }
        [JsonPropertyName("longitude_y")]
        public double Longitude_y { get; set; }
        public GPSPosition() { }
        public GPSPosition(double Latitude_x, double Longitude_y)
        {
            this.Latitude_x = Latitude_x;
            this.Longitude_y = Longitude_y;
        }
        public override string ToString()
        {
            return $"{Latitude_x:N3}, {Longitude_y:N3}";
        }
        public double DistanceTo(GPSPosition other)
        {
            var dx = this.Latitude_x - other.Latitude_x;
            var dy = this.Longitude_y - other.Longitude_y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
