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
using System.Xml;
using Caliburn.Micro;
using DomaMove.Doma;

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

                    Target.TagAllExistingMapsAndSetTargetCategories(Source);
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

    public class SourceMap : PropertyChangedBase
    {
        public DomaConnection SourceConnection { get; set; }
        private bool? _existsOnTarget;
        private string _transferStatus;
        private string _sourceUrl;
        private Category _targetCategory;
        private string Version { get; set; }

        public SourceMap(Category category, MapInfo mapInfo, DomaConnection sourceConnection)
        {
            SourceConnection = sourceConnection;

            _sourceUrl = sourceConnection.Url.ToLower().Replace("/webservice.php", string.Empty);

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
            var image = DownloadImageAsync(GetUrl(imageType, "jpg"));

            if (image != null)
            {
                FileExtension = "jpg";
                return image;
            }

            image = DownloadImage(GetUrl(imageType, "png"));

            if (image != null)
            {
                FileExtension = "png";
            }

            return image;
        }

        public string FileExtension { get; set; }

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

        private static byte[] DownloadImageAsync(string uri)
        {           
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(uri);
                //var response = (HttpWebResponse)request.GetResponse();

                var getResponseTask = Task<WebResponse>.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, null);

                getResponseTask.Wait();

                var response = (HttpWebResponse)getResponseTask.Result;

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

        public Category TargetCategory
        {
            get { return _targetCategory; }
            set
            {
                if (Equals(value, _targetCategory)) return;
                _targetCategory = value;
                NotifyOfPropertyChange("TargetCategory");
            }
        }

        public void DownloadImages()
        {
            if (SourceConnection.SupportsPublishWithPreUpload)
            {
                MapImage = DownloadImage();
                BlankImage = DownloadBlankImage();
                ThumbnailImage = DownloadThumbnailImage();
            }
            else
            {
                MapImage = DownloadImage();
            }
        }
    }

    public class DomaConnection : PropertyChangedBase
    {
        public void OverrideVersion(string version)
        {
            Version = version;
        }

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
            if (!ConnectionOk)
                return;

            var doma = CreateDomaClient();

            var getCategoriesTask = Task<GetAllCategoriesResponse>.Factory.FromAsync(doma.BeginGetAllCategories, doma.EndGetAllCategories, new GetAllCategoriesRequest { Username = Username, Password = Password }, null);

            var getMapsTask = Task<GetAllMapsResponse>.Factory.FromAsync(doma.BeginGetAllMaps, doma.EndGetAllMaps, new GetAllMapsRequest { Username = Username, Password = Password }, null);

            Task.WaitAll(getCategoriesTask, getMapsTask);

            Categories = getCategoriesTask.Result.Categories.ToList();

            Maps = (from map in getMapsTask.Result.Maps
                    join category in Categories on map.CategoryID equals category.ID
                    select new SourceMap(category, map, this)).ToList();

            UserId = Categories.First().UserID;
        }

        private bool ConnectionOk
        {
            get
            {
                if (string.IsNullOrEmpty(Status))
                    TestConnection().Wait();

                return Status == "OK";
            }
        }

        public Task TestConnection()
        {
            var doma = CreateDomaClient();

            var connectTask = Task<ConnectResponse>.Factory.FromAsync(doma.BeginConnect, doma.EndConnect, new ConnectRequest { Username = Username, Password = Password }, null);

            var finalTask = connectTask.ContinueWith(t =>
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
                    {
                        Status = "OK";
                        Version = response.Version;
                    }
                    else
                        Status = response.ErrorMessage;
                });

            return finalTask;
        }

        protected string Version
        {
            get { return _version; }
            set
            {
                if (string.IsNullOrEmpty(_version))
                    _version = value;
            }
        }

        private DOMAServicePortTypeClient CreateDomaClient()
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
            if (!ConnectionOk)
                return;

            foreach (var selectedMap in selectedMaps)
            {
                UploadMap(selectedMap);
            }
        }

        private bool? _supportsPublishWithPreUpload;
        private string _version;

        public bool SupportsPublishWithPreUpload
        {
            get
            {
                if (_supportsPublishWithPreUpload == null)
                {
                    _supportsPublishWithPreUpload = Version != null &&
                                                    String.Compare(Version, "3.0", StringComparison.Ordinal) >= 0;
                }

                return _supportsPublishWithPreUpload.Value;
            }
        }

        private void UploadMap(SourceMap sourceMap)
        {
            if (sourceMap.ExistsOnTarget == true)
                return;

            var sourceMapInfo = sourceMap.MapInfo;

            var mapInfo = new MapInfo
                {
                    ID = 0, // Blank value to get new Id on target server
                    UserID = UserId,
                    CategoryID = sourceMap.TargetCategory.ID,
                    Date = sourceMapInfo.Date,
                    Name = sourceMapInfo.Name,
                    Organiser = sourceMapInfo.Organiser,
                    Country = sourceMapInfo.Country,
                    Discipline = sourceMapInfo.Discipline,
                    RelayLeg = sourceMapInfo.RelayLeg,
                    MapName = sourceMapInfo.MapName,
                    ResultListUrl = sourceMapInfo.ResultListUrl,
                    Comment = sourceMap.MapInfo.Comment
                };

            if (sourceMap.SourceConnection.SupportsPublishWithPreUpload && SupportsPublishWithPreUpload)
            {
                PublishWithPreUpload(sourceMap, mapInfo);
            }
            else
            {
                PublishMap(sourceMap, mapInfo);
            }
        }

        private void PublishMap(SourceMap sourceMap, MapInfo mapInfo)
        {
            sourceMap.TransferStatus = "Downloading...";
            sourceMap.DownloadImages();

            var doma = CreateDomaClient();

            mapInfo.MapImageFileExtension = sourceMap.FileExtension;
            mapInfo.MapImageData = sourceMap.MapImage;

            sourceMap.TransferStatus = "Publishing...";

            var publishMapTask = Task<PublishMapResponse>.Factory.FromAsync(doma.BeginPublishMap, doma.EndPublishMap,
                                                                            new PublishMapRequest { MapInfo = mapInfo, Username = Username, Password = Password }, null);

            publishMapTask.Wait();

            if (publishMapTask.Status == TaskStatus.Faulted)
            {
                if (publishMapTask.Exception != null)
                    sourceMap.TransferStatus = publishMapTask.Exception.Message;
                else
                    sourceMap.TransferStatus = "Failed";

                return;
            }

            var response = publishMapTask.Result;

            if (response.Success)
            {
                sourceMap.TransferStatus = "Complete";
                sourceMap.ExistsOnTarget = true;
            }
            else
            {
                sourceMap.TransferStatus = response.ErrorMessage;
            }
        }

        private void PublishWithPreUpload(SourceMap sourceMap, MapInfo mapInfo)
        {
            sourceMap.TransferStatus = "Downloading...";
            sourceMap.DownloadImages();

            var doma = CreateDomaClient();

            mapInfo.MapImageFileExtension = sourceMap.FileExtension;

            try
            {
                sourceMap.TransferStatus = "Uploading...";

                var mapImageResponse = doma.UploadPartialFile(new UploadPartialFileRequest
                    {
                        Data = sourceMap.MapImage,
                        FileName = Guid.NewGuid().ToString() + "." + sourceMap.FileExtension,
                        Username = Username,
                        Password = Password
                    });

                var blankImageResponse = doma.UploadPartialFile(new UploadPartialFileRequest
                    {
                        Data = sourceMap.BlankImage,
                        FileName = Guid.NewGuid().ToString() + "." + sourceMap.FileExtension,
                        Username = Username,
                        Password = Password
                    });

                var thumbnailImageResponse = doma.UploadPartialFile(new UploadPartialFileRequest
                    {
                        Data = sourceMap.ThumbnailImage,
                        FileName = Guid.NewGuid().ToString() + "." + sourceMap.FileExtension,
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
            }
            catch (Exception e)
            {
                sourceMap.TransferStatus = e.Message;
            }
        }

        public void TagAllExistingMapsAndSetTargetCategories(DomaConnection source)
        {
            foreach (var sourceMap in source.Maps)
            {
                sourceMap.ExistsOnTarget = (
                    Maps.Any(
                        x =>
                        x.MapInfo.Date == sourceMap.MapInfo.Date &&
                        string.Compare(x.MapInfo.Name, sourceMap.MapInfo.Name,
                                       StringComparison.InvariantCultureIgnoreCase) == 0));

                // Match Category by name. If missing - take first by ID
                var foundCategory = Categories.FirstOrDefault(x => string.Compare(x.Name, sourceMap.Category.Name, StringComparison.OrdinalIgnoreCase) == 0);

                sourceMap.TargetCategory = foundCategory ?? Categories.OrderBy(x => x.ID).First();
            }
        }
    }
}
