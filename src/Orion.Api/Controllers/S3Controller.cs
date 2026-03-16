using Microsoft.AspNetCore.Mvc;
using Orion.Core.Services;
using System.Xml.Linq;
using System.Globalization;

namespace Orion.Api.Controllers
{
    [Route("s3")]
    [ApiController]
    public class S3Controller : ControllerBase
    {
        private readonly IStorageService _storage;
        private readonly ILogger<S3Controller> _logger;

        public S3Controller(IStorageService storage, ILogger<S3Controller> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ListBuckets()
        {
            // S3 standard response for ListBuckets
            XNamespace ns = "http://s3.amazonaws.com/doc/2006-03-01/";
            var doc = new XDocument(
                new XElement(ns + "ListAllMyBucketsResult",
                    new XElement(ns + "Owner",
                        new XElement(ns + "ID", "orion-owner-id"),
                        new XElement(ns + "DisplayName", "orion")
                    ),
                    new XElement(ns + "Buckets",
                        new XElement(ns + "Bucket",
                            new XElement(ns + "Name", "default"),
                            new XElement(ns + "CreationDate", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))
                        )
                    )
                )
            );

            return Content(doc.ToString(), "application/xml");
        }

        [HttpGet("{bucket}")]
        public async Task<IActionResult> ListObjects(string bucket, [FromQuery] string? prefix)
        {
            var objects = await _storage.ListBlobsAsync(prefix ?? "");
            
            XNamespace ns = "http://s3.amazonaws.com/doc/2006-03-01/";
            var contentElements = new List<XElement>();

            foreach (var path in objects)
            {
                var meta = await _storage.GetBlobMetadataAsync(path);
                if (meta == null) continue;

                contentElements.Add(new XElement(ns + "Contents",
                    new XElement(ns + "Key", path),
                    new XElement(ns + "LastModified", meta.LastModified.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")),
                    new XElement(ns + "ETag", meta.ETag),
                    new XElement(ns + "Size", meta.SizeBytes),
                    new XElement(ns + "StorageClass", "STANDARD")
                ));
            }

            var doc = new XDocument(
                new XElement(ns + "ListBucketResult",
                    new XElement(ns + "Name", bucket),
                    new XElement(ns + "Prefix", prefix ?? ""),
                    new XElement(ns + "Marker", ""),
                    new XElement(ns + "MaxKeys", 1000),
                    new XElement(ns + "IsTruncated", false),
                    contentElements
                )
            );

            return Content(doc.ToString(), "application/xml");
        }

        [HttpGet("{bucket}/{*key}")]
        public async Task<IActionResult> GetObject(string bucket, string key)
        {
            try
            {
                var data = await _storage.GetBlobAsync(key);
                var meta = await _storage.GetBlobMetadataAsync(key);
                
                if (meta?.ContentType != null)
                {
                    return File(data, meta.ContentType);
                }
                return File(data, "application/octet-stream");
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPut("{bucket}/{*key}")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> PutObject(string bucket, string key)
        {
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            var data = ms.ToArray();
            
            var contentType = Request.ContentType ?? "application/octet-stream";
            await _storage.SaveBlobAsync(key, data, contentType);

            Response.Headers.Append("ETag", $"\"{Guid.NewGuid()}\"");
            return Ok();
        }

        [HttpDelete("{bucket}/{*key}")]
        public async Task<IActionResult> DeleteObject(string bucket, string key)
        {
            await _storage.DeleteBlobAsync(key);
            return NoContent();
        }

        [HttpHead("{bucket}/{*key}")]
        public async Task<IActionResult> HeadObject(string bucket, string key)
        {
            var meta = await _storage.GetBlobMetadataAsync(key);
            if (meta == null) return NotFound();

            Response.Headers.Append("Content-Length", meta.SizeBytes.ToString());
            Response.Headers.Append("Last-Modified", meta.LastModified.ToString("R"));
            Response.Headers.Append("ETag", meta.ETag);
            
            return Ok();
        }
    }
}
