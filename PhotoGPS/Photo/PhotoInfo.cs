using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhotoGPS.Photo
{
    class PhotoInfo
    {
        string path;
        public string Path { get { return path; } }

        DateTime priseDeVue;
        public DateTime PriseDeVue { get { return priseDeVue; } }

        DateTime positionGps;
        double lat, lon;
        public double Lat { get { return lat; } }
        public double Lon { get { return lon; } }

        bool positionValid;
        public bool PositionValid { get { return positionValid; } }

        public override string ToString()
        {
            return PriseDeVue.ToString();
        }

        public PhotoInfo()
        {
            path = String.Empty;
            priseDeVue = DateTime.Now;
            lat = 0;
            lon = 0;
            positionGps = DateTime.Now;
            positionValid = false;
        }

        public PhotoInfo(string path, DateTime priseDeVue)
            : this()
        {
            this.path = path;
            this.priseDeVue = priseDeVue;
        }

        public PhotoInfo(string path, DateTime priseDeVue, double lat, double lon)
            : this()
        {
            this.path = path;
            this.priseDeVue = priseDeVue;
            this.lat = lat;
            this.lon = lon;
            positionValid = true;
        }

        public void setGpsPosition(double lat, double lon, DateTime positionGps)
        {
            this.lat = lat;
            this.lon = lon;
            this.positionGps = positionGps;
            positionValid = true;
        }
    }
}
