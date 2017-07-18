using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhotoGPS.Photo
{
    class PhotoInfo
    {
        string path;
        DateTime priseDeVue, positionGps;
        double lat, lon;

        public PhotoInfo()
        {
            path = String.Empty;
            priseDeVue = DateTime.Now;
            lat = 0;
            lon = 0;
            positionGps = DateTime.Now;
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
        }

        public void setGpsPosition(double lat, double lon, DateTime positionGps)
        {
            this.lat = lat;
            this.lon = lon;
            this.positionGps = positionGps;
        }
    }
}
