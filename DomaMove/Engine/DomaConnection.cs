using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Caliburn.Micro;
using DomaMove.Doma;

namespace DomaMove.Engine
{
    public class DomaConnection : PropertyChangedBase
    {
        private readonly DomaClientFactory _clientFactory;
        private readonly ImageDownloader _imageDownloader;

        private List<SourceMap> _maps;
        private string _status;
        private bool? _supportsPublishWithPreUpload;

        public DomaConnection(DomaClientFactory clientFactory, ImageDownloader imageDownloader, ConnectionParameters connectionParameters)
        {
            _clientFactory = clientFactory;
            _imageDownloader = imageDownloader;

            Url = connectionParameters.Url;
            Username = connectionParameters.User;
            Password = connectionParameters.Password;

            Maps = new List<SourceMap>();
            Categories = new List<Category>();
        }

        public string Url { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
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

        private bool IsConnectionOk
        {
            get
            {
                if (string.IsNullOrEmpty(Status))
                    TestConnection().Wait();

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
                    select new SourceMap(category, map, supportsBlankMapImage, baseUri, _imageDownloader)).ToList();

            UserId = Categories.First().UserID;
        }

        private string GetBaseUri()
        {
            return Url.ToLower().Replace("/webservice.php", string.Empty);
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
                        doma.Abort();
                        return;
                    }

                    ConnectResponse response = t.Result;

                    doma.Close();

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

        private DOMAServicePortTypeClient CreateDomaClient()
        {
            return _clientFactory.Create(GetBaseUri());
        }

        public void UploadMaps(IEnumerable<SourceMap> selectedMaps)
        {
            if (!IsConnectionOk)
                return;

            Parallel.ForEach(selectedMaps, new ParallelOptions { MaxDegreeOfParallelism = _imageDownloader.ConnectionLimit }, UploadMap);
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

            sourceMap.DownloadImages();

            if (sourceMap.HasBlankMapImage && SupportsPublishWithPreUpload)
            {
                PublishWithPreUpload(sourceMap, mapInfo);
            }
            else
            {
                PublishMap(sourceMap, mapInfo);
            }
        }

        private bool SupportsPublishWithPreUpload
        {
            get { return _supportsPublishWithPreUpload.HasValue && _supportsPublishWithPreUpload == true; }
        }

        private void PublishMap(SourceMap sourceMap, MapInfo mapInfo)
        {
            var doma = CreateDomaClient();

            mapInfo.MapImageFileExtension = sourceMap.FileExtension;
            mapInfo.MapImageData = sourceMap.MapImage;

            sourceMap.TransferStatus = "Publishing...";

            var publishMapRequest = new PublishMapRequest { MapInfo = mapInfo, Username = Username, Password = Password };
            var publishMapTask = Task<PublishMapResponse>.Factory.FromAsync(doma.BeginPublishMap, doma.EndPublishMap, publishMapRequest, null);

            publishMapTask.Wait();

            if (publishMapTask.Status == TaskStatus.Faulted)
            {
                sourceMap.TransferStatus = publishMapTask.Exception != null ? publishMapTask.Exception.Message : "Failed";
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
            var doma = CreateDomaClient();

            mapInfo.MapImageFileExtension = sourceMap.FileExtension;

            try
            {
                sourceMap.TransferStatus = "Uploading...";

                var uploadMapTask = UploadPartialFile(sourceMap.MapImage, sourceMap.FileExtension, doma);
                var uploadBlankMapTask = UploadPartialFile(sourceMap.BlankImage, sourceMap.FileExtension, doma);
                var uploadThumbnailTask = UploadPartialFile(sourceMap.ThumbnailImage, sourceMap.FileExtension, doma);

                var tasks = new Task[] { uploadMapTask, uploadBlankMapTask, uploadThumbnailTask };

                try
                {
                    Task.WaitAll(tasks);
                }
                catch (Exception e)
                {
                    sourceMap.TransferStatus = "Image upload failed badly. " + e.Message;
                    return;
                }

                if (tasks.OfType<Task<UploadPartialFileResponse>>().Any(x => !x.Result.Success))
                {
                    sourceMap.TransferStatus = "Image upload failed";
                    return;
                }

                sourceMap.TransferStatus = "Publishing...";

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
                    sourceMap.TransferStatus = "Complete";
                    sourceMap.ExistsOnTarget = true;
                }
                else
                {
                    sourceMap.TransferStatus = publishMapTask.Result.ErrorMessage;
                }
            }
            catch (Exception e)
            {
                sourceMap.TransferStatus = e.Message;
            }
        }

        private Task<UploadPartialFileResponse> UploadPartialFile(byte[] image, string fileExtension, DOMAServicePortTypeClient doma)
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
            foreach (SourceMap sourceMap in source.Maps)
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
    }
}