using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using DomaMove.Doma;

namespace DomaMove.Engine
{
    public class DomaConnection : PropertyChangedBase
    {
        private readonly DomaClientFactory _clientFactory;
        private readonly ImageDownloader _imageDownloader;
        private readonly ConnectionSettings _connectionSettings;

        private List<TransferMap> _maps;
        private string _status;
        private bool? _supportsPublishWithPreUpload;

        public DomaConnection(DomaClientFactory clientFactory, ImageDownloader imageDownloader, ConnectionSettings connectionSettings)
        {
            _clientFactory = clientFactory;
            _imageDownloader = imageDownloader;
            _connectionSettings = connectionSettings;

            Url = connectionSettings.Url;
            Username = connectionSettings.User;
            Password = connectionSettings.Password;

            //Maps = new List<TransferMap>();
            //Categories = new List<Category>();
        }
        public ConnectionSettings Settings { get { return _connectionSettings; } }

        private string Url { get { return _connectionSettings.Url; } set { _connectionSettings.Url = value; } }
        private string Username { get { return _connectionSettings.User; } set { _connectionSettings.User = value; } }
        private string Password { get { return _connectionSettings.Password; } set { _connectionSettings.Password = value; } }

        private List<Category> Categories { get; set; }
        private int UserId { get; set; }

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

        public List<TransferMap> Maps
        {
            get { return _maps; }
            private set
            {
                if (Equals(value, _maps)) return;
                _maps = value;
                NotifyOfPropertyChange("Maps");
            }
        }

        private string _lastConnectionSettingsHash;

        private bool IsConnectionOk
        {
            get
            {
                var connectionHash = _connectionSettings.GetHash();

                if (connectionHash != _lastConnectionSettingsHash)
                {
                    TestConnection().Wait();
                    _lastConnectionSettingsHash = connectionHash;
                }

                return Status == "OK";
            }
        }

        public void GetAllMaps()
        {
            if (!IsConnectionOk)
                return;

            var doma = CreateDomaClient();

            var getAllCategoriesRequest = new GetAllCategoriesRequest { Username = Username, Password = Password };
            var getCategoriesTask = Task<GetAllCategoriesResponse>.Factory.FromAsync(doma.BeginGetAllCategories, doma.EndGetAllCategories, getAllCategoriesRequest, null);

            var getAllMapsRequest = new GetAllMapsRequest { Username = Username, Password = Password };
            var getAllMapsTask = Task<GetAllMapsResponse>.Factory.FromAsync(doma.BeginGetAllMaps, doma.EndGetAllMaps, getAllMapsRequest, null);

            Task.WaitAll(getCategoriesTask, getAllMapsTask);

            Categories = getCategoriesTask.Result.Categories.ToList();

            var baseUri = GetBaseUri();
            var supportsBlankMapImage = SupportsPublishWithPreUpload;

            Maps = (from map in getAllMapsTask.Result.Maps
                    join category in Categories on map.CategoryID equals category.ID
                    select new TransferMap(category, map, supportsBlankMapImage, baseUri, _imageDownloader)).ToList();

            UserId = Categories.First().UserID;
        }

        private string GetBaseUri()
        {
            var url = Url.ToLower().Replace("/webservice.php", string.Empty);

            if (url.EndsWith("/"))
                url = url.Substring(0, url.Length - 1);

            return url;
        }

        public Task TestConnection()
        {
            var doma = CreateDomaClient();

            var connectRequest = new ConnectRequest { Username = Username, Password = Password };
            var connectTask = Task<ConnectResponse>.Factory.FromAsync(doma.BeginConnect, doma.EndConnect, connectRequest, null);

            var getResultTask = connectTask.ContinueWith(t =>
                {
                    if (t.Status == TaskStatus.Faulted)
                    {
                        Status = "FAILED";
                        return;
                    }

                    ConnectResponse response = t.Result;

                    if (response.Success)
                    {
                        Status = "OK";
                        _supportsPublishWithPreUpload = response.Version != null &&
                                                    String.Compare(response.Version, "3.0", StringComparison.Ordinal) >= 0;
                    }
                    else
                        Status = response.ErrorMessage;
                });

            return getResultTask;
        }

        private DOMAServicePortType CreateDomaClient()
        {
            return _clientFactory.Create(GetBaseUri() + "/webservice.php");
        }

        public void UploadMaps(List<TransferMap> selectedMaps)
        {
            if (!IsConnectionOk)
                return;

            _transferSuccessCount = 0;
            _transferFailedCount = selectedMaps.Count();
            _transferExceptions = new ConcurrentBag<Exception>();

            Parallel.ForEach(selectedMaps, new ParallelOptions { MaxDegreeOfParallelism = _imageDownloader.ConnectionLimit }, UploadMap);
        }

        private int _transferSuccessCount;

        public int TransferSuccessCount { get { return _transferSuccessCount; } }
        public int TransferSuccessFailed { get { return _transferFailedCount; } }

        private int _transferFailedCount;

        private ConcurrentBag<Exception> _transferExceptions = new ConcurrentBag<Exception>();

        public IEnumerable<Exception> TransferExceptions { get { return _transferExceptions.ToList(); } }

        private void UploadMap(TransferMap transferMap)
        {
            if (transferMap.ExistsOnTarget == true)
                return;

            var sourceMapInfo = transferMap.MapInfo;

            var mapInfo = new MapInfo
                {
                    ID = 0, // Blank value to get new Id on target server
                    UserID = UserId,
                    CategoryID = transferMap.TargetCategory.ID,
                    Date = sourceMapInfo.Date,
                    Name = sourceMapInfo.Name,
                    Organiser = sourceMapInfo.Organiser,
                    Country = sourceMapInfo.Country,
                    Discipline = sourceMapInfo.Discipline,
                    RelayLeg = sourceMapInfo.RelayLeg,
                    MapName = sourceMapInfo.MapName,
                    ResultListUrl = sourceMapInfo.ResultListUrl,
                    Comment = transferMap.MapInfo.Comment
                };

            transferMap.DownloadImages();

            if (transferMap.MapImage == null)
            {
                transferMap.TransferStatus = "Map download failed.";
            }

            if (transferMap.HasBlankMapImage && SupportsPublishWithPreUpload)
            {
                PublishWithPreUpload(transferMap, mapInfo);
            }
            else
            {
                PublishMap(transferMap, mapInfo);
            }
        }

        private bool SupportsPublishWithPreUpload
        {
            get { return _supportsPublishWithPreUpload.HasValue && _supportsPublishWithPreUpload == true; }
        }

        private void PublishMap(TransferMap transferMap, MapInfo mapInfo)
        {
            var doma = CreateDomaClient();

            mapInfo.MapImageFileExtension = transferMap.FileExtension;
            mapInfo.MapImageData = transferMap.MapImage;

            transferMap.TransferStatus = "Publishing...";

            try
            {
                var publishMapRequest = new PublishMapRequest { MapInfo = mapInfo, Username = Username, Password = Password };
                var publishMapTask = Task<PublishMapResponse>.Factory.FromAsync(doma.BeginPublishMap, doma.EndPublishMap, publishMapRequest, null);

                publishMapTask.Wait();

                if (publishMapTask.Status == TaskStatus.Faulted)
                {
                    _transferExceptions.Add(publishMapTask.Exception);

                    transferMap.TransferStatus = publishMapTask.Exception != null ? publishMapTask.Exception.Message : "Failed";
                    return;
                }

                var response = publishMapTask.Result;

                if (response.Success)
                {
                    transferMap.TransferStatus = "Complete";
                    transferMap.ExistsOnTarget = true;
                }
                else
                {
                    transferMap.TransferStatus = response.ErrorMessage;
                }
            }
            catch (AggregateException ae)
            {
                foreach (var innerException in ae.Flatten().InnerExceptions)
                {
                    _transferExceptions.Add(innerException);
                }

                transferMap.TransferStatus = "Publishing failed badly. " + ae.Message;
            }
        }

        private void PublishWithPreUpload(TransferMap transferMap, MapInfo mapInfo)
        {
            var doma = CreateDomaClient();

            mapInfo.MapImageFileExtension = transferMap.FileExtension;

            try
            {
                transferMap.TransferStatus = "Uploading...";

                var uploadMapTask = UploadPartialFile(transferMap.MapImage, transferMap.FileExtension, doma);
                var uploadBlankMapTask = UploadPartialFile(transferMap.BlankImage, transferMap.FileExtension, doma);
                var uploadThumbnailTask = UploadPartialFile(transferMap.ThumbnailImage, transferMap.FileExtension, doma);

                var tasks = new Task[] { uploadMapTask, uploadBlankMapTask, uploadThumbnailTask };

                Task.WaitAll(tasks);

                if (tasks.OfType<Task<UploadPartialFileResponse>>().Any(x => !x.Result.Success))
                {
                    transferMap.TransferStatus = "Image upload failed";
                    return;
                }

                transferMap.TransferStatus = "Publishing...";

                var publishRequest = new PublishPreUploadedMapRequest
                    {
                        MapInfo = mapInfo,
                        PreUploadedMapImageFileName = uploadMapTask.Result.FileName,
                        PreUploadedBlankMapImageFileName = uploadBlankMapTask.Result.FileName,
                        PreUploadedThumbnailImageFileName = uploadThumbnailTask.Result.FileName,
                        Username = Username,
                        Password = Password
                    };

                var publishMapTask = Task<PublishPreUploadedMapResponse>.Factory.FromAsync(doma.BeginPublishPreUploadedMap, doma.EndPublishPreUploadedMap, publishRequest, null);

                publishMapTask.Wait();

                if (publishMapTask.Result.Success)
                {
                    transferMap.TransferStatus = "Complete";
                    transferMap.ExistsOnTarget = true;

                    Interlocked.Increment(ref _transferSuccessCount);
                    Interlocked.Decrement(ref _transferFailedCount);
                }
                else
                {
                    transferMap.TransferStatus = publishMapTask.Result.ErrorMessage;
                }
            }
            catch (AggregateException ae)
            {
                foreach (var innerException in ae.Flatten().InnerExceptions)
                {
                    _transferExceptions.Add(innerException);
                }

                transferMap.TransferStatus = "Publishing failed badly. " + ae.Message;
            }
        }

        private Task<UploadPartialFileResponse> UploadPartialFile(byte[] image, string fileExtension, DOMAServicePortType doma)
        {
            var request = new UploadPartialFileRequest
                {
                    Data = image,
                    FileName = Guid.NewGuid().ToString() + "." + fileExtension,
                    Username = Username,
                    Password = Password
                };

            return Task<UploadPartialFileResponse>.Factory.FromAsync(doma.BeginUploadPartialFile, doma.EndUploadPartialFile, request, null);
        }

        public void TagAllExistingMapsAndSetTargetCategories(DomaConnection source)
        {
            foreach (TransferMap sourceMap in source.Maps)
            {
                sourceMap.ExistsOnTarget = (
                                               Maps.Any(
                                                   x =>
                                                   x.MapInfo.Date == sourceMap.MapInfo.Date &&
                                                   string.Compare(x.MapInfo.Name, sourceMap.MapInfo.Name,
                                                                  StringComparison.InvariantCultureIgnoreCase) == 0));

                // Match Category by name. If missing - take first by ID
                Category foundCategory =
                    Categories.FirstOrDefault(
                        x => string.Compare(x.Name, sourceMap.Category.Name, StringComparison.OrdinalIgnoreCase) == 0);

                sourceMap.TargetCategory = foundCategory ?? Categories.OrderBy(x => x.ID).First();
            }
        }

        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Found {0} maps, ", Maps.Count);

            var fallbackCount = Maps.Count(x => x.Category.Name != x.TargetCategory.Name);

            if (fallbackCount > 0)
                sb.AppendFormat("but had category problems on {0} of them.", fallbackCount);
            else if (Maps.Count > 0)
                sb.Append("and did perfect category matching.");
            else
                sb.Append("check your connection settings.");

            return sb.ToString();
        }
    }
}