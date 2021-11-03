﻿using System;
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

            Maps = new List<TransferMap>();
            Categories = new List<Category>();
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

        private async Task<bool> IsConnectionOk()
        {
           
            var connectionHash = _connectionSettings.GetHash();

            if (connectionHash != _lastConnectionSettingsHash)
            {
                await TestConnection();
                _lastConnectionSettingsHash = connectionHash;
            }

            return Status == "OK";
            
        }

        public async Task GetAllMaps()
        {
            if (!await IsConnectionOk())
                return;

            var doma = CreateDomaClient();

            var getAllCategoriesRequest = new GetAllCategoriesRequest { Username = Username, Password = Password };
            var getCategoriesTask = doma.GetAllCategoriesAsync(getAllCategoriesRequest);

            var getAllMapsRequest = new GetAllMapsRequest { Username = Username, Password = Password };
            var getAllMapsTask = doma.GetAllMapsAsync(getAllMapsRequest);

            await Task.WhenAll(getCategoriesTask, getAllMapsTask);

            Categories = (await getCategoriesTask).Categories.ToList();

            var baseUri = GetBaseUri();
            var supportsBlankMapImage = SupportsPublishWithPreUpload;

            Maps = (from map in (await getAllMapsTask).Maps
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

        public async Task TestConnection()
        {
            var doma = CreateDomaClient();

            var connectRequest = new ConnectRequest { Username = Username, Password = Password };

            try
            {
                var response = await doma.ConnectAsync(connectRequest);

                if (response.Success)
                {
                    Status = "OK";
                    _supportsPublishWithPreUpload = response.Version != null && string.Compare(response.Version, "3.0", StringComparison.Ordinal) >= 0;
                }
                else
                {
                    Status = response.ErrorMessage;
                }
            }
            catch (Exception)
            {
                Status = "FAILED";
                return;
            }                                  
        }

        private DOMAServicePortType CreateDomaClient()
        {
            return _clientFactory.Create(GetBaseUri() + "/webservice.php");
        }        

        public async Task UploadMaps(List<TransferMap> selectedMaps)
        {
            if (!await IsConnectionOk())
                return;

            _transferSuccessCount = 0;
            _transferFailedCount = selectedMaps.Count();
            _transferExceptions = new ConcurrentBag<Exception>();

            foreach (var map in selectedMaps)  
                await UploadMap(map);
        }

        private int _transferSuccessCount;

        public int TransferSuccessCount { get { return _transferSuccessCount; } }
        public int TransferSuccessFailed { get { return _transferFailedCount; } }

        private int _transferFailedCount;

        private ConcurrentBag<Exception> _transferExceptions = new ConcurrentBag<Exception>();

        public IEnumerable<Exception> TransferExceptions { get { return _transferExceptions.ToList(); } }

        private async Task UploadMap(TransferMap transferMap)
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

            await transferMap.DownloadImages();

            if (transferMap.MapImage == null)
            {
                transferMap.TransferStatus = "Map download failed.";
            }

            if (SupportsPublishWithPreUpload)
            {
                await PublishWithPreUpload(transferMap, mapInfo);
            }
            else
            {
                await PublishMap(transferMap, mapInfo);
            }
        }

        private bool SupportsPublishWithPreUpload
        {
            get { return _supportsPublishWithPreUpload.HasValue && _supportsPublishWithPreUpload == true; }
        }

        private async Task PublishMap(TransferMap transferMap, MapInfo mapInfo)
        {         
            try
            {
                var doma = CreateDomaClient();

                mapInfo.MapImageFileExtension = transferMap.FileExtension;
                mapInfo.MapImageData = transferMap.MapImage;
                mapInfo.BlankMapImageData = new byte[0];

                transferMap.TransferStatus = "Publishing...";

                var publishMapRequest = new PublishMapRequest
                    {
                        MapInfo = mapInfo,
                        Username = Username,
                        Password = Password
                    };

                try
                {
                    var response = await doma.PublishMapAsync(publishMapRequest);
                   
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
                catch (Exception ex)
                {
                    _transferExceptions.Add(ex);

                    transferMap.TransferStatus = ex.Message;
                    return;
                }
            }
            catch (AggregateException ae)
            {
                var innerExceptions = ae.Flatten().InnerExceptions;

                foreach (var innerException in innerExceptions)
                {
                    _transferExceptions.Add(innerException);
                }

                transferMap.TransferStatus = "Publishing failed badly: " +
                                             string.Join(", ", innerExceptions.Select(x => x.Message));
            }
            catch (Exception e)
            {
                _transferExceptions.Add(e);

                transferMap.TransferStatus = "Publishing failed badly: " + e.Message;
            }
        }

        private async Task PublishWithPreUpload(TransferMap transferMap, MapInfo mapInfo)
        {          
            try
            {
                var doma = CreateDomaClient();

                mapInfo.MapImageFileExtension = transferMap.FileExtension;

                transferMap.TransferStatus = "Uploading...";
               
                var uploadedMap = await UploadPartialFile(transferMap.MapImage, transferMap.FileExtension, doma);

                UploadPartialFileResponse uploadedBlankMap = null;
                if (transferMap.HasBlankMapImage)
                    uploadedBlankMap = await UploadPartialFile(transferMap.BlankImage, transferMap.FileExtension, doma);

                var uploadedThumbnail = await UploadPartialFile(transferMap.ThumbnailImage, transferMap.FileExtension, doma);

                if (new [] {uploadedMap, uploadedBlankMap, uploadedThumbnail }.Where(x => x != null).Any(x => !x.Success))
                {
                    transferMap.TransferStatus = "Image upload failed";
                    return;
                }

                transferMap.TransferStatus = "Publishing...";

                var publishRequest = new PublishPreUploadedMapRequest
                    {
                        MapInfo = mapInfo,
                        PreUploadedMapImageFileName = uploadedMap.FileName,
                        PreUploadedThumbnailImageFileName = uploadedThumbnail.FileName,
                        Username = Username,
                        Password = Password
                    };

                if(uploadedBlankMap != null)
                {
                    publishRequest.PreUploadedBlankMapImageFileName = uploadedBlankMap.FileName;
                }

                publishRequest.MapInfo.BlankMapImageData = new byte[0];
                publishRequest.MapInfo.MapImageData = new byte[0];

                var publishMapResponse = await doma.PublishPreUploadedMapAsync(publishRequest);

                if (publishMapResponse.Success)
                {
                    transferMap.TransferStatus = "Complete";
                    transferMap.ExistsOnTarget = true;

                    Interlocked.Increment(ref _transferSuccessCount);
                    Interlocked.Decrement(ref _transferFailedCount);
                }
                else
                {
                    transferMap.TransferStatus = publishMapResponse.ErrorMessage;
                }
            }
            catch (AggregateException ae)
            {
                var innerExceptions = ae.Flatten().InnerExceptions;

                foreach (var innerException in innerExceptions)
                {
                    _transferExceptions.Add(innerException);
                }

                transferMap.TransferStatus = "Publishing failed badly: " + string.Join(", ", innerExceptions.Select(x => x.Message));
            }
            catch (Exception e)
            {
                _transferExceptions.Add(e);

                transferMap.TransferStatus = "Publishing failed badly: " + e.Message;
            }
        }

        private async Task<UploadPartialFileResponse> UploadPartialFile(byte[] imageData, string fileExtension, DOMAServicePortType doma)
        {
            const int chunkSize = 512 * 1024; // 512 KB

            string fileName = Guid.NewGuid().ToString() + "." + fileExtension;
            int position = 0;

            while (position < imageData.Length)
            {
                int length = Math.Min(chunkSize, imageData.Length - position);
                var buffer = new byte[length];
                Array.Copy(imageData, position, buffer, 0, length);
                position += length;
                var uploadPartialFileRequest = new UploadPartialFileRequest
                {
                    Username = Username,
                    Password = Password,
                    FileName = fileName,
                    Data = buffer
                };
                var uploadPartialFileResponse = await doma.UploadPartialFileAsync(uploadPartialFileRequest);
                
                if (!uploadPartialFileResponse.Success)
                {
                    return uploadPartialFileResponse;
                }
            }

            return new UploadPartialFileResponse
            {
                Success = true,
                FileName = fileName
            };
        }

        public void TagAllExistingMapsAndSetTargetCategories(DomaConnection source)
        {
            if (source.Status != "OK")
                return;

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