using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml;
using Caliburn.Micro;
using DomaMove.Doma;
using Mouse = System.Windows.Input.Mouse;

namespace DomaMove
{
    public class MoveViewModel : Screen
    {
        public MoveViewModel(DomaConnection source, DomaConnection target)
        {
            Source = source;
            Target = target;
        }

        public DomaConnection Source { get; set; }
        public DomaConnection Target { get; set; }

        public void TestSourceConnection()
        {
            Source.TestConnection();
        }

        public void TestTargetConnection()
        {
            Target.TestConnection();
        }

        public void GetMaps()
        {
            var waitCursor = WaitCursor.Start();

            Task.Factory.StartNew(() =>
                {
                    Task.WaitAll(Task.Factory.StartNew(() => Source.GetAllMaps()),
                                 Task.Factory.StartNew(() => Target.GetAllMaps()));

                    Target.TagAllExistingMapsAndSetCategories(Source);
                })
                .ContinueWith(t => Execute.OnUIThread(waitCursor.Dispose));
        }

        public List<SourceMap> SelectedMaps { get; set; }

        public void Transfer()
        {
            var window = GetView() as Window;

            IList selectedItems = ((MoveView)window.Content).Source_Maps.SelectedItems;

            SelectedMaps = new List<SourceMap>();

            foreach (var selectedItem in selectedItems)
            {
                SelectedMaps.Add(selectedItem as SourceMap);
            }

            if (SelectedMaps != null && SelectedMaps.Any())
            {
                var waitCursor = WaitCursor.Start();

                Task.Factory.StartNew(() => Target.UploadMaps(SelectedMaps))
                            .ContinueWith(t => Execute.OnUIThread(waitCursor.Dispose));
            }
        }
    }

    public class WaitCursor : IDisposable
    {
        public static WaitCursor Start()
        {
            return new WaitCursor();
        }

        private WaitCursor()
        {
            Mouse.OverrideCursor = Cursors.Wait;
        }

        public void Dispose()
        {
            Mouse.OverrideCursor = null;
        }
    }

    public class SourceMap : PropertyChangedBase
    {
        private readonly string _sourceUrl;
        private bool? _existsOnTarget;
        private string _transferStatus;

        public SourceMap(Category category, MapInfo mapInfo, string sourceUrl)
        {
            _sourceUrl = sourceUrl;

            _sourceUrl = _sourceUrl.ToLower().Replace("/webservice.php", string.Empty);

            Category = category;
            MapInfo = mapInfo;
            ExistsOnTarget = false;
        }

        public bool? ExistsOnTarget
        {
            get { return _existsOnTarget; }
            set { _existsOnTarget = value; NotifyOfPropertyChange(() => ExistsOnTarget); }
        }

        public Category Category { get; private set; }
        public MapInfo MapInfo { get; private set; }

        public byte[] DownloadImage()
        {
            return DownloadJpgWithFallbackToPng();
        }

        public byte[] DownloadBlankImage()
        {
            return DownloadJpgWithFallbackToPng(".blank");
        }

        private byte[] DownloadJpgWithFallbackToPng(string imageType = "")
        {
            var image = DownloadImage(GetUrl(imageType, "jpg"));

            if (image == null)
            {
                image = DownloadImage(GetUrl(imageType, "png"));
            }

            return image;
        }

        public byte[] DownloadThumbnailImage()
        {
            return DownloadImage(GetUrl(".thumbnail", "jpg"));
        }

        private string GetUrl(string imageType, string format)
        {
            return string.Format("{0}/map_images/{1}{2}.{3}", _sourceUrl, MapInfo.ID, imageType, format);
        }

        private static byte[] DownloadImage(string uri)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                // Check that the remote file was found. The ContentType
                // check is performed since a request for a non-existent
                // image file might be redirected to a 404-page, which would
                // yield the StatusCode "OK", even though the image was not
                // found.
                if ((response.StatusCode == HttpStatusCode.OK ||
                     response.StatusCode == HttpStatusCode.Moved ||
                     response.StatusCode == HttpStatusCode.Redirect) &&
                    response.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                {
                    // if the remote file was found, download it
                    using (var inputStream = response.GetResponseStream())
                    using (var outputStream = new MemoryStream())
                    {
                        var buffer = new byte[4096];

                        int bytesRead;
                        do
                        {
                            bytesRead = inputStream.Read(buffer, 0, buffer.Length);
                            outputStream.Write(buffer, 0, bytesRead);
                        } while (bytesRead != 0);
                        return outputStream.ToArray();
                    }
                }
            }
            catch (WebException e)
            {
                var response = e.Response as HttpWebResponse;

                if (response != null)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        // Expected - will retry another filetype
                        return null;
                    }
                }

                throw;
            }

            return null;
        }

        public byte[] MapImage { get; private set; }
        public byte[] BlankImage { get; private set; }
        public byte[] ThumbnailImage { get; private set; }

        public string TransferStatus
        {
            get { return _transferStatus; }
            set { _transferStatus = value; NotifyOfPropertyChange(() => TransferStatus); }
        }

        public void DownloadAllImages()
        {
            MapImage = DownloadImage();
            BlankImage = DownloadBlankImage();
            ThumbnailImage = DownloadThumbnailImage();
        }
    }

    public class DomaConnection : PropertyChangedBase
    {
        private string _status;
        private List<SourceMap> _maps;
        public string Url { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public string Status
        {
            get { return _status; }
            private set
            {
                if (value == _status) return;
                _status = value;
                NotifyOfPropertyChange("Status");
            }
        }

        public List<SourceMap> Maps
        {
            get { return _maps; }
            private set
            {
                if (Equals(value, _maps)) return;
                _maps = value;
                NotifyOfPropertyChange("Maps");
            }
        }

        private List<Category> Categories { get; set; }

        private int UserId { get; set; }

        public DomaConnection()
        {
            Maps = new List<SourceMap>();
            Categories = new List<Category>();
        }

        public void GetAllMaps()
        {
            if (!CheckConnection)
                return;

            var doma = GetDomaClient();

            var getCategoriesTask = Task<GetAllCategoriesResponse>.Factory.FromAsync(doma.BeginGetAllCategories, doma.EndGetAllCategories, new GetAllCategoriesRequest { Username = Username, Password = Password }, null);

            var getMapsTask = Task<GetAllMapsResponse>.Factory.FromAsync(doma.BeginGetAllMaps, doma.EndGetAllMaps, new GetAllMapsRequest { Username = Username, Password = Password }, null);

            Task.WaitAll(getCategoriesTask, getMapsTask);

            Categories = getCategoriesTask.Result.Categories.ToList();

            Maps = (from map in getMapsTask.Result.Maps
                    join category in Categories on map.CategoryID equals category.ID
                    select new SourceMap(category, map, Url)).ToList();

            UserId = Categories.First().UserID;
        }

        private bool CheckConnection
        {
            get
            {
                if (string.IsNullOrEmpty(Status))
                    TestConnection(); 
                
                return Status == "OK";
            }
        }

        public void TestConnection()
        {
            var doma = GetDomaClient();

            var connectTask = Task<ConnectResponse>.Factory.FromAsync(doma.BeginConnect, doma.EndConnect, new ConnectRequest { Username = Username, Password = Password }, null);

            connectTask.ContinueWith(t =>
                {
                    if (t.Status == TaskStatus.Faulted)
                    {
                        Status = "FAILED";
                        doma.Abort();
                        return;
                    }

                    var response = t.Result;

                    doma.Close();

                    if (response.Success)
                        Status = "OK";
                    else
                        Status = response.ErrorMessage;
                });
        }

        private DOMAServicePortTypeClient GetDomaClient()
        {
            var binding = new BasicHttpBinding
            {
                MaxBufferPoolSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue,
                Security = new BasicHttpSecurity { Mode = BasicHttpSecurityMode.None },
                ReaderQuotas = new XmlDictionaryReaderQuotas
                {
                    MaxArrayLength = int.MaxValue,
                    MaxDepth = int.MaxValue,
                    MaxStringContentLength = int.MaxValue
                }
            };
            var serviceUrl = Url;

            if (!serviceUrl.EndsWith("/webservice.php"))
                serviceUrl += "/webservice.php";

            var endpointAddress = new EndpointAddress(serviceUrl);
            var doma = new DOMAServicePortTypeClient(binding, endpointAddress);
            return doma;
        }

        public void UploadMaps(List<SourceMap> selectedMaps)
        {
            if (!CheckConnection)
                return;

            foreach (var selectedMap in selectedMaps)
            {
                UploadMap(selectedMap);
            }
        }

        private void UploadMap(SourceMap sourceMap)
        {
            if (sourceMap.ExistsOnTarget == true)
                return;

            sourceMap.TransferStatus = "Downloading...";

            sourceMap.DownloadAllImages();

            var sourceMapInfo = sourceMap.MapInfo;

            var mapInfo = new MapInfo
                {
                    ID = 0, // Blank value to get new Id on target server
                    UserID = UserId,
                    CategoryID = Categories.Single(x => string.Compare(x.Name, sourceMap.Category.Name, StringComparison.OrdinalIgnoreCase) == 0).ID,
                    Date = sourceMapInfo.Date,
                    Name = sourceMapInfo.Name,
                    Organiser = sourceMapInfo.Organiser,
                    Country = sourceMapInfo.Country,
                    Discipline = sourceMapInfo.Discipline,
                    RelayLeg = sourceMapInfo.RelayLeg,
                    MapName = sourceMapInfo.MapName,
                    ResultListUrl = sourceMapInfo.ResultListUrl,
                    Comment = sourceMap.MapInfo.Comment,
                };

            var doma = GetDomaClient();

            try
            {
                sourceMap.TransferStatus = "Uploading...";
                
                var mapImageResponse = doma.UploadPartialFile(new UploadPartialFileRequest
                    {
                        Data = sourceMap.MapImage,
                        FileName = Guid.NewGuid().ToString() + ".jpg",
                        Username = Username,
                        Password = Password
                    });

                var blankImageResponse = doma.UploadPartialFile(new UploadPartialFileRequest
                   {
                       Data = sourceMap.BlankImage,
                       FileName = Guid.NewGuid().ToString() + ".jpg",
                       Username = Username,
                       Password = Password
                   });

                var thumbnailImageResponse = doma.UploadPartialFile(new UploadPartialFileRequest
                {
                    Data = sourceMap.ThumbnailImage,
                    FileName = Guid.NewGuid().ToString() + ".jpg",
                    Username = Username,
                    Password = Password
                });

                if (!mapImageResponse.Success || !blankImageResponse.Success || !thumbnailImageResponse.Success)
                    return;

                sourceMap.TransferStatus = "Publishing...";
                var response = doma.PublishPreUploadedMap(new PublishPreUploadedMapRequest
                {
                    MapInfo = mapInfo,
                    PreUploadedMapImageFileName = mapImageResponse.FileName,
                    PreUploadedBlankMapImageFileName = blankImageResponse.FileName,
                    PreUploadedThumbnailImageFileName = thumbnailImageResponse.FileName,
                    Username = Username,
                    Password = Password
                });

                if (response.Success)
                {
                    sourceMap.TransferStatus = "Complete";
                    sourceMap.ExistsOnTarget = true;
                }
                else
                {
                    sourceMap.TransferStatus = response.ErrorMessage;
                }

                doma.Close();
            }
            catch (Exception e)
            {
                sourceMap.TransferStatus = e.Message;
                doma.Abort();
            }
        }

        public void TagAllExistingMapsAndSetCategories(DomaConnection source)
        {
            var sourceMaps = source.Maps;

            foreach (var sourceMap in sourceMaps)
            {
                sourceMap.ExistsOnTarget = (
                    Maps.Any(
                        x =>
                        x.MapInfo.Date == sourceMap.MapInfo.Date &&
                        string.Compare(x.MapInfo.Name, sourceMap.MapInfo.Name,
                                       StringComparison.InvariantCultureIgnoreCase) == 0));
            }
        }
    }
}
