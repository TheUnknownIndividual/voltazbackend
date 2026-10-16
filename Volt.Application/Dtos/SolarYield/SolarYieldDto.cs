namespace Volt.Application.Dtos.SolarYield
{
    public sealed class SolarYieldDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        /// <summary>Panel tilt in degrees from horizontal (PVGIS "slope").</summary>
        public double Tilt { get; set; }

        /// <summary>PVGIS aspect: 0 = south, -90 = east, 90 = west.</summary>
        public double Azimuth { get; set; }

        /// <summary>True when PVGIS chose the optimal tilt/azimuth itself.</summary>
        public bool OptimalAngles { get; set; }

        public double SystemLossPercent { get; set; }

        /// <summary>Annual AC energy per installed kWp (PVGIS E_y), losses already applied.</summary>
        public double SpecificYieldKwhPerKwp { get; set; }

        public string RadiationDatabase { get; set; } = string.Empty;
        public string Source { get; set; } = "PVGIS";
    }
}
