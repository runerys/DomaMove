using System;
using System.IO;
using System.Net;

namespace DomaMove.Engine
{
    public class ImageDownloader
    {
        public int ConnectionLimit
        {
            get { return ServicePointManager.DefaultConnectionLimit; }
        }

        public byte[] DownloadImage(string uri)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(uri);
                var response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

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
                        if (inputStream == null)
                            return null;

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
    }
}
