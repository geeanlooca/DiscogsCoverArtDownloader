using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiscogsNet.Api;
using DiscogsNet.Model;
using DiscogsNet.Model.Search;
using System.Net;

namespace ConsoleApplication1
{    
    class Program
    {
        #region Internal Classes

        public class ImageDLHelper
        {
            public string Artist
            {
                get;
                private set;
            }

            public string Release
            {
                get;
                private set;
            }

            public string OutputFile
            {
                get;
                private set;
            }

            public ImageDLHelper(string artist, string release, string outputFile)
            {
                Artist = artist;
                Release = release;
                OutputFile = outputFile;
            }

            public ImageDLHelper()
            {

            }
        }

        #endregion

        public static string directoryPath;

        static void Main(string[] args)
        {
             if (args.Length == 0)
            {
                Console.WriteLine("Missing arguments");
                return;
            }

            bool scanOnly = false;

            directoryPath = args[0];

            //for (int i = 0; i < args.Length; i++)
            //{
            //    if (args[i] == "--scanonly")
            //    {
            //        scanOnly = true;
            //    }
            //    else
            //    {
            //        directoryPath = args[i];
            //    }
            //}


            if (!Directory.Exists(directoryPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid directory: " + directoryPath);
                Console.ResetColor();
                return;
            }

            Console.Write("Scanning ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(directoryPath + "\n");
            Console.ResetColor();

            Task scanDirectory = Task.Factory.StartNew(() => ScanDirectory(directoryPath));
                        
            //if (!scanOnly)
            //{
            //    FileSystemWatcher fsWatcher = new FileSystemWatcher(directoryPath);
            //    fsWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName;
            //    fsWatcher.IncludeSubdirectories = true;
            //    fsWatcher.Created += fsWatcher_Created;
            //    fsWatcher.Renamed += fsWatcher_Renamed;
            //    fsWatcher.EnableRaisingEvents = true;
            //}

            Console.ReadLine();
        }


        #region Watchers Methods

        private static void fsWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            string path = e.FullPath;
            string[] tokens = path.Split('\\');
            string coverPath = path + "\\cover.jpg";

            if (tokens.Length == directoryPath.Split('\\').Length + 2)
            {
                string artist = tokens[tokens.Length - 2];
                string release = tokens[tokens.Length - 1];

                Task.Factory.StartNew(() => DownloadImage(new ImageDLHelper(artist, release, coverPath)));
            }
        }

        static void fsWatcher_Created(object sender, FileSystemEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(e.ChangeType + " " + e.FullPath);

            string path = e.FullPath;
            string[] tokens = path.Split('\\');
            string coverPath = path + "\\cover.jpg";

            if (tokens.Length == directoryPath.Split('\\').Length + 2)
            {
                //new album folder
                string artist = tokens[tokens.Length - 2];
                string release = tokens[tokens.Length - 1];

                if (release.ToLower() != "nuova cartella")
                    Task.Factory.StartNew(() => DownloadImage(new ImageDLHelper(artist, release, coverPath)));
            }
        }

        #endregion

        public static void ScanDirectory(string path)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(directoryPath);
            DirectoryInfo[] artists = dirInfo.GetDirectories();
            List<Task> tasks = new List<Task>();

            foreach (DirectoryInfo artist in artists)
            {
                DirectoryInfo[] albums = artist.GetDirectories();

                string[] tokens = artist.FullName.Split('\\');

                string artistName = tokens[tokens.Length - 1];

                if (artistName != "Compilations")
                foreach (DirectoryInfo album in albums)
                {
                    string cover = album.FullName + "\\cover.jpg";

                    if (!File.Exists(cover))
                    {
                        string albumName = album.FullName.Split('\\')[tokens.Length];
                        tasks.Add(Task.Factory.StartNew(() => DownloadImage(new ImageDLHelper(artistName, albumName, cover))));
                    }
                }
            }


            Task.WhenAll(tasks).Wait();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("My work is done");
            Console.ResetColor();
        }

        
        public static void DownloadImage(ImageDLHelper imageInfo)
        {
            //
            //  TODO: implement a way to search by release and with a different / similar artist name 
            //         (Example Aesop Rock's None Shall Pass search gives back no results 
            //          because the correct artist name on discogs for this release is Aesop)
            //          


            Console.WriteLine("Examining " + imageInfo.Artist + '/' + imageInfo.Release);

            Discogs3 discogs = new Discogs3("DiscogsImageDownloader/1.0");
            
            SearchQuery query = new SearchQuery() { Artist = imageInfo.Artist, ReleaseTitle = imageInfo.Release, Type = SearchItemType.Release};
            SearchResults results = discogs.Search(query);
                   
            if (results.Results.Length != 0)
            {
                string textOut = "\nFetching " + imageInfo.Artist + " - " + imageInfo.Release;
                Release fetchedRelease = null;
                string coverImage = String.Empty;

                try
                {
                    //select the release with the highest number of images
                    int maxIm = 0;
                    Release valid = null;
                    for (int i = 0; i < results.Results.Length; i++)
                    {
                        Release rel = discogs.GetRelease(results.Results[0].Id);
                        if (rel.Images.Length > maxIm)
                        {
                            maxIm = rel.Images.Length;
                            valid = rel;
                        }
                    }

                    fetchedRelease = valid;
                    coverImage = fetchedRelease.Images[0].Uri.Replace("api.discogs", "s.pixogs");
                    WebClient client = new WebClient();
                    client.Headers.Add("user-agent", "Folder-monitoring-image-downloader");
                    client.DownloadFile(coverImage, imageInfo.OutputFile);

                    textOut += "\tOK";
                }
                catch(NullReferenceException nullRefEx)
                {
                    //try again with only releasealbum name
                    
                    textOut += "\tNo images found";
                }
                catch(System.Net.WebException webEx)
                {
                    textOut += "\t" + webEx.Message;
                }
                catch (Exception ex)
                {
                    textOut += "\tGeneric error";
                }
                finally
                {
                    Console.Write(textOut);
                }
            }
        }
    }
}
