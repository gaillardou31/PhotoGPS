using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using GMap.NET;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Diagnostics;


namespace PhotoGPS
{
    public partial class FormMain : Form
    {
        public FormMain()
        {
            InitializeComponent();

            Properties.Settings.Default.Reload();
            if (Properties.Settings.Default.version != Application.ProductVersion)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.version = Application.ProductVersion;
                Properties.Settings.Default.Save();
            }

            if (String.IsNullOrEmpty(Properties.Settings.Default.lastGPXFile))
            {
                Properties.Settings.Default.lastGPXFile = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                Properties.Settings.Default.Save();
            }

            if (String.IsNullOrEmpty(Properties.Settings.Default.lastPhotoPath))
            {
                Properties.Settings.Default.lastPhotoPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                Properties.Settings.Default.Save();
            }
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            initMap();
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            gmap.Manager.CancelTileCaching();
            gmap.Dispose();
        }

        private void initMap()
        {
            gmap.ShowCenter = false;
            gmap.MapProvider = GMap.NET.MapProviders.BingHybridMapProvider.Instance;
            GMap.NET.GMaps.Instance.Mode = GMap.NET.AccessMode.ServerAndCache;
            gmap.DragButton = System.Windows.Forms.MouseButtons.Left;
            gmap.SetPositionByKeywords("Gavarnie, France");
        }

        private void loadGPXFile(string sFile, string sPhotoPath)
        {
            gpxType type = Helpers.tools.Deserialize(sFile);
            displayTrack(type, sPhotoPath);
        }

        private void displayTrack(gpxType track, string sPhotoPath)
        {
            displayProgressionText("Début du traitement");

            gmap.Overlays.Clear();
            gmap.Refresh();

            #region extraction des points de passage
            if (track == null || track.trk == null)
                return;

            displayProgressionText("Extraction des points de passage");

            List<wptType> listePoints = new List<wptType>();

            trkType[] trk = track.trk;
            for (int trki = 0; trki < trk.Length; trki++)
            {
                for (int trksegi = 0; trksegi < trk[trki].trkseg.Length; trksegi++)
                {
                    for (int trkpti = 0; trkpti < trk[trki].trkseg[trksegi].trkpt.Length; trkpti++)
                    {
                        if (trk[trki].trkseg[trksegi].trkpt[trkpti].timeSpecified)
                            listePoints.Add(trk[trki].trkseg[trksegi].trkpt[trkpti]);
                    }
                }
            }

            if (listePoints.Count == 0)
                return;

            // tri des photos par date de prise de vue
            listePoints = listePoints.OrderBy(x => x.time).ToList();
            #endregion

            #region extraction des images et horodatage
            displayProgressionText("Extraction des images et horodatage");

            List<Photo.PhotoInfo> listPhoto = new List<Photo.PhotoInfo>();
            List<string> photoFiles = Directory.EnumerateFiles(sPhotoPath, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".JPG") || s.EndsWith(".jpg")).ToList<string>();

            foreach (string sPhoto in photoFiles)
            {
                string dateString = recupDatePriseDeVueImage(sPhoto);
                if (String.IsNullOrEmpty(dateString) == false)
                {
                    DateTime date = DateTime.ParseExact(dateString, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
                    listPhoto.Add(new Photo.PhotoInfo(sPhoto, date));
                }
            }

            // tri des photos par date de prise de vue
            listPhoto = listPhoto.OrderBy(x => x.PriseDeVue).ToList();
            #endregion

            #region fait correspondre les heures des photos à des positions GPX
            displayProgressionText("Recherche de correspondance entre les photos et les points GPS");

            foreach (Photo.PhotoInfo photo in listPhoto)
            {
                wptType type = listePoints.Find(t => (t.timeSpecified && t.time.ToLocalTime() >= photo.PriseDeVue && (t.time.ToLocalTime() - photo.PriseDeVue).TotalMinutes < 10));
                if (type != null)
                    photo.setGpsPosition((double)type.lat, (double)type.lon, type.time.ToLocalTime());
            }
            #endregion

            // http://www.independent-software.com/gmap-net-beginners-tutorial-adding-clickable-markers-to-your-map-updated-for-visual-studio-2015-and-gmap-net-1-7/
            GMapOverlay routes = new GMapOverlay("chemin");
            List<PointLatLng> pointsRoute = new List<PointLatLng>();

            GMapOverlay markers = new GMapOverlay("markers");

            foreach (wptType pt in listePoints)
            {
                pointsRoute.Add(new PointLatLng((double)pt.lat, (double)pt.lon));
            }

            displayProgressionText("Redimension des photos");

            // temp directory to store thumbs
            string tempPath = System.IO.Path.GetTempPath() + Application.ProductName + Path.DirectorySeparatorChar + "thumbs";
            if (Directory.Exists(tempPath) == false)
            {
                try
                {
                    Directory.CreateDirectory(tempPath);
                }
                catch { }
            }

            int index = 0;
            List<Photo.PhotoInfo> listCorrespondante = listPhoto.FindAll(p => p.PositionValid);
            foreach (Photo.PhotoInfo photo in listCorrespondante)
            {
                index++;

                displayProgression(index, listCorrespondante.Count);
                displayProgressionText("Redimension des photos " + index + "/" + listCorrespondante.Count);

                //C:\Users\Nicolas\Documents\Visual Studio 2010\Projects\PhotoGPS\Examples\JPG\_A9A1192.jpg
                // PhotoGPS_Examples_JPG__A9A1192.jpg
                List<string> partNames = photo.Path.Split('\\').ToList<string>();
                bool bContinue = true;
                int partNumber = partNames.Count - 1;
                string newName = partNames[partNumber];
                partNumber--;
                while (bContinue)
                {
                    if (partNames.Count - partNumber > 3)
                        bContinue = false;
                    if (partNumber < 0)
                        bContinue = false;

                    newName = partNames[partNumber] + "_" + newName;
                    partNumber--;
                }

                string newDirectory = tempPath + Path.DirectorySeparatorChar + newName;
                if (File.Exists(newDirectory) == false)
                {
                    // création de la miniature
                    Bitmap bmp = ResizeWidth(photo.Path, 100);
                    bmp.Save(newDirectory);
                }

                GMapMarker marker = new GMarkerGoogle(new PointLatLng((double)photo.Lat, (double)photo.Lon), new Bitmap(newDirectory));
                marker.Tag = photo;
                markers.Markers.Add(marker);
            }

            GMapRoute route = new GMapRoute(pointsRoute, "A walk in the park");
            route.Stroke = new Pen(Color.Red, 3);
            routes.Routes.Add(route);
            gmap.Overlays.Add(routes);

            gmap.Overlays.Add(markers);

            gmap.ZoomAndCenterRoute(route);

            displayProgressionText(String.Empty);
            displayProgression(0, 0);
        }

        Bitmap ResizeWidth(string fileSource, int largeur)
        {
            Bitmap bmpResize = null;
            using (Bitmap bmpSource = new Bitmap(fileSource))
            {

                int nNewLargeur, nNewHauteur;
                if (bmpSource.Width > largeur)
                {
                    nNewLargeur = largeur;
                    float ratio = (float)(largeur) / (float)bmpSource.Width;
                    nNewHauteur = (int)(bmpSource.Height * ratio);
                }
                else
                {
                    nNewLargeur = bmpSource.Width;
                    nNewHauteur = bmpSource.Height;
                }

                bmpResize = new Bitmap(bmpSource, nNewLargeur, nNewHauteur);
            }

            return bmpResize;
        }


        /// <summary>
        /// Fonction permettant de récupérer la date de création d'une image.
        /// </summary>
        /// <param name="fichier">filePath: Fichier image dans lequel on va chercher la date de création</param>
        /// <returns></returns>
        private String recupDatePriseDeVueImage(string filePath)
        {
            // On se connecte à l'image sans avoir à l'ouvrir
            using (var fStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (Image monImage = Image.FromStream(fStream, false, false))
            {
                // On récupère l'information recherchée.
                // Si elle n'est pas trouvée, on renvoie une chaine vide.
                try
                {
                    PropertyItem propItem = monImage.GetPropertyItem(36867);
                    String datePDV = Encoding.ASCII.GetString(propItem.Value).TrimEnd('\0');
                    return datePDV;
                }
                catch
                {
                    return "";
                }
            }
        }

        private void quitterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void ouvrirGPXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = Properties.Settings.Default.lastGPXFile;
            ofd.Multiselect = false;
            ofd.Filter = "GPX|*.gpx";
            DialogResult dr = ofd.ShowDialog(this);
            if (dr != System.Windows.Forms.DialogResult.OK)
                return;

            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = Properties.Settings.Default.lastPhotoPath;
            fbd.ShowNewFolderButton = false;
            DialogResult dr2 = fbd.ShowDialog(this);
            if (dr2 != System.Windows.Forms.DialogResult.OK)
                return;

            Properties.Settings.Default.lastGPXFile = ofd.FileName;
            Properties.Settings.Default.lastPhotoPath = fbd.SelectedPath;
            Properties.Settings.Default.Save();

            loadGPXFile(ofd.FileName, fbd.SelectedPath);
        }

        private void gmap_OnMarkerClick(GMapMarker item, MouseEventArgs e)
        {
            // Photo.PhotoInfo photo
            if (item.Tag.GetType() == typeof(Photo.PhotoInfo))
            {
                Photo.PhotoInfo photo = (Photo.PhotoInfo)item.Tag;
                //MessageBox.Show(this, photo.Path);
            }
        }

        delegate void displayProgressionTextDelegate(string texte);
        void displayProgressionText(string texte)
        {
            if (this.InvokeRequired)
            {
                displayProgressionTextDelegate d = new displayProgressionTextDelegate(displayProgressionText);
                this.Invoke(d, new object[] { texte });
            }
            else
            {
                toolStripStatusLabelProgression.Text = texte;
                statusStrip1.Refresh();
            }
        }

        delegate void displayProgressionDelegate(int number, int total);
        void displayProgression(int number, int total)
        {
            if (this.InvokeRequired)
            {
                displayProgressionDelegate d = new displayProgressionDelegate(displayProgression);
                this.Invoke(d, new object[] { number, total });
            }
            else
            {
                toolStripProgressBarProgression.Minimum = 0;
                toolStripProgressBarProgression.Maximum = total;
                toolStripProgressBarProgression.Value = number;
                statusStrip1.Refresh();
            }
        }

        private void gmap_OnMarkerEnter(GMapMarker item)
        {
            if (item.Tag.GetType() == typeof(Photo.PhotoInfo))
            {
                Photo.PhotoInfo photo = (Photo.PhotoInfo)item.Tag;
                pictureBoxSelected.ImageLocation = photo.Path;
            }
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            /*
            foreach (Photo.PhotoInfo photo in listPhoto)
            {
            }
            */

        }

        void writeGPSonImage(List<Photo.PhotoInfo> liste)
        {

            //https://code.google.com/archive/p/exiflibrary/wikis/ExifLibrary.wiki


            /*
Image mage = Image.FromFile(Path);
PropertyItem item = mage.GetPropertyItem(306);
item.Value = System.Text.ASCIIEncoding.ASCII.GetBytes("2012:12:12 17:17:17");
image.SetPropertyItem(item);
image.Save();
*/
        }

        private void compileMultiPgxToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = Properties.Settings.Default.lastPhotoPath;
            fbd.ShowNewFolderButton = false;
            DialogResult dr2 = fbd.ShowDialog(this);
            if (dr2 != System.Windows.Forms.DialogResult.OK)
                return;

            Properties.Settings.Default.lastPhotoPath = fbd.SelectedPath;
            Properties.Settings.Default.Save();

            compileMultiGpx(fbd.SelectedPath);
        }

        private void compileMultiGpx(string path)
        {
            List<string> directories = Directory.GetDirectories(path).ToList();
            List<gpxType> tracesGPX = new List<gpxType>();
            List<trksegType> listSegments = new List<trksegType>();

            foreach (string dir in directories)
            {
                List<string> files = Directory.GetFiles(dir, "*.gpx").ToList();
                foreach (string file in files)
                {
                    tracesGPX.Add(Helpers.tools.Deserialize(file));
                }
            }

            foreach (gpxType trace in tracesGPX)
            {
                if (trace.trk == null || trace.trk.Length == 0)
                    break;

                List<trkType> trkTypes = trace.trk.ToList();
                foreach (trkType trkType in trkTypes)
                {
                    if (trkType.trkseg == null || trkType.trkseg.Length == 0)
                        break;

                    foreach (trksegType t in trkType.trkseg.ToList())
                        listSegments.Add(t);
                }
            }

            gpxType gpxOut = new gpxType();
            gpxOut.creator = "Nicolas PERDU";
            gpxOut.trk = new trkType[1] { new trkType() };
            gpxOut.trk[0].desc = "Complile multi files";
            gpxOut.trk[0].trkseg = listSegments.ToArray();

            Helpers.tools.Serialize(gpxOut, path + Path.DirectorySeparatorChar + "compile multi gpx.gpx");
        }

    }
}
