using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;

namespace Helpers
{
    class tools
    {
        static public void Serialize(gpxType gpx, string outFile)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(gpxType));
            //using (TextWriter writer = new StreamWriter(@"C:\Xml.xml"))
            using (TextWriter writer = new StreamWriter(outFile))
            {
                serializer.Serialize(writer, gpx);
            }
        }

        static public gpxType Deserialize(string fileDirectory)
        {
            string fileContent = File.ReadAllText(fileDirectory);
            XmlSerializer x = new XmlSerializer(typeof(gpxType));
            gpxType myTest = (gpxType)x.Deserialize(new StringReader(fileContent));
            return myTest;
        }
    }
}
