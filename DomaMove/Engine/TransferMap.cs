﻿using System;
using Caliburn.Micro;
using DomaMove.Doma;

namespace DomaMove.Engine
{
    public class TransferMap : PropertyChangedBase, IComparable<TransferMap>
    {
        private readonly bool _sourceSupportsBlankImage;
        private readonly ImageDownloader _imageDownloader;
        private bool? _existsOnTarget;
        private string _transferStatus;
        private readonly string _sourceBaseUri;
        private Category _targetCategory;

        public TransferMap(Category category, MapInfo mapInfo,  bool sourceSupportsBlankImage, string sourceBaseUri, ImageDownloader imageDownloader)
        {
            _sourceSupportsBlankImage = sourceSupportsBlankImage;
            _imageDownloader = imageDownloader;

            _sourceBaseUri = sourceBaseUri;

            Category = category;
            MapInfo = mapInfo;
        }

        public bool? ExistsOnTarget
        {
            get { return _existsOnTarget; }
            set { _existsOnTarget = value; NotifyOfPropertyChange("ExistsOnTarget"); }
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

        public byte[] DownloadThumbnailImage()
        {
            return DownloadImage(GetUrl(".thumbnail", "jpg"));
        }

        private byte[] DownloadJpgWithFallbackToPng(string imageType = "")
        {
            var image = DownloadImage(GetUrl(imageType, "jpg"));

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
        
        private string GetUrl(string imageType, string format)
        {
            return string.Format("{0}/map_images/{1}{2}.{3}", _sourceBaseUri, MapInfo.ID, imageType, format);
        }

        private byte[] DownloadImage(string uri)
        {
            return _imageDownloader.DownloadImage(uri);
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

        public bool HasBlankMapImage { get { return BlankImage != null; } }

        public void DownloadImages()
        {
            TransferStatus = "Downloading...";

            if (_sourceSupportsBlankImage)
            {
                MapImage = DownloadImage();
                BlankImage = DownloadBlankImage();
                ThumbnailImage = DownloadThumbnailImage();
            }
            else
            {
                MapImage = DownloadImage();
            }

            TransferStatus = "Downloaded.";
        }

        public int CompareTo(TransferMap other)
        {
            var my = string.Format("{0}-{1}", MapInfo.Date, MapInfo.Name);
            var their = string.Format("{0}-{1}", other.MapInfo.Date, other.MapInfo.Name);

            return String.Compare(my, their, StringComparison.Ordinal);
        }
    }
}