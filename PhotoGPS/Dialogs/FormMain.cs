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
            gmap.MapProvider = GMap.NET.MapProviders.OviHybridMapProvider.Instance;
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
            gmap.Overlays.Clear();
            gmap.Refresh();

            #region extraction des points de passage
            if (track == null || track.trk == null)
                return;

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

            foreach (Photo.PhotoInfo photo in listPhoto)
            {
                if (photo.PositionValid)
                {
                    GMapMarker marker = new GMarkerGoogle(new PointLatLng((double)photo.Lat, (double)photo.Lon), ResizeWidth(photo.Path, 100));
                    markers.Markers.Add(marker);
                }
            }

            GMapRoute route = new GMapRoute(pointsRoute, "A walk in the park");
            route.Stroke = new Pen(Color.Red, 3);
            routes.Routes.Add(route);
            gmap.Overlays.Add(routes);

            gmap.Overlays.Add(markers);

            gmap.ZoomAndCenterRoute(route);
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

        }
    }
}
