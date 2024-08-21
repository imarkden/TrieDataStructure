
using System;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using query_suggestion.Services;
using System.Text.RegularExpressions;
using System.Text;
using Amazon;
using System.Collections.Concurrent;
using Amazon.Runtime;

namespace query_suggestion.Controllers
{
    [ApiController]
    [Route("trie")]
    public class TrieController : ControllerBase
    {
        private static readonly TrieService _trieService = new TrieService();

        static TrieController()
        {
            // S3 client set up
            var s3Client = new AmazonS3Client(
                "Key",
                "SecretKey",
                new AmazonS3Config
                {
                    RegionEndpoint = RegionEndpoint.Region,
                    UseAccelerateEndpoint = true,
                    Timeout = TimeSpan.FromMinutes(5),
                    ReadWriteTimeout = TimeSpan.FromMinutes(5),
                    RetryMode = RequestRetryMode.Standard,
                    MaxErrorRetry = 3
                });

            ConcurrentDictionary<string, int> mergedTitles = new ConcurrentDictionary<string, int>();

            // Chunk sizes
            int pageviewsChunkSize = 50000;
            int pageCountsChunkSize = 10000;

            // Process files concurrently
            var processPageviews = ProcessFileFromS3InChunks(s3Client, "BucketName", "Assets/pageviews-20240720-automated", pageviewsChunkSize, mergedTitles, isPageViews: true);
            var processPageCounts = ProcessFileFromS3InChunks(s3Client, "BucketName", "Assets/pagecounts-20071209-190000", pageCountsChunkSize, mergedTitles, isPageViews: false);

            Task.WhenAll(processPageviews, processPageCounts).Wait();

            // Insert the titles
            foreach (var item in mergedTitles)
            {
                _trieService.Insert(item.Key, item.Value);
            }
        }

        private static async Task ProcessFileFromS3InChunks(AmazonS3Client s3Client, string bucketName, string key, int chunkSize, ConcurrentDictionary<string, int> mergedTitles, bool isPageViews)
        {
            var titles = new List<string>();
            using (var response = await s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key
            }))

            using (var stream = response.ResponseStream)
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string line;
                int count = 0;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var parts = line.Split(' ');

                    if (isPageViews)
                    {
                        // Pageviews
                        if (Regex.IsMatch(line, "^en\\.[a-zA-Z]+\\s+[a-zA-Z]+\\s+", RegexOptions.IgnoreCase) && parts.Length >= 5)
                        {
                            var title = parts[1].ToLower(); // 2nd column for title
                            if (int.TryParse(parts[4], out int popularity)) // 5th column for popularity
                            {
                                mergedTitles.AddOrUpdate(title, popularity, (key, oldValue) => oldValue + popularity);
                            }
                        }
                    }
                    else
                    {
                        // Pagecounts
                        if (Regex.IsMatch(line, "^en\\s+[a-zA-Z]+\\s+", RegexOptions.IgnoreCase) && parts.Length >= 4)
                        {
                            var title = parts[1].ToLower(); // 2nd column for title
                            if (int.TryParse(parts[3], out int popularity)) // 4th column for popularity
                            {
                                mergedTitles.AddOrUpdate(title, popularity, (key, oldValue) => oldValue + popularity);
                            }
                        }
                    }

                    count++;
                    if (count == chunkSize)
                    {
                        titles.Clear();
                        count = 0;
                    }
                }
            }
        }

        [HttpGet]
        public IActionResult GetSuggestions(string title)
        {
            var suggestions = _trieService.GetSuggestionsWithLevenshtein(title)
                               .Select(s => new { title = s.title, popularity = s.popularity });
            return Ok(suggestions.Take(10));
        }

        [HttpGet("search")]
        public IActionResult GetSearch(string title, int pageNumber = 1, int pageSize = 10)
        {
            var search = _trieService.GetPaginatedSearch(title, pageNumber, pageSize)
                               .Select(s => new { title = s.title, popularity = s.popularity });
            return Ok(search);
        }

        [HttpGet("all")]
        public IActionResult GetAlltitles(int pageNumber = 1, int pageSize = 10)
        {
            var titles = _trieService.GetPaginatedList(pageNumber, pageSize)
                            .Select(s => new { title = s.title, popularity = s.popularity });
            return Ok(titles);
        }

        [HttpGet("name")]
        public IActionResult GettitleByName(string title, int addCount = 1)
        {
            var titles = _trieService.GettitleByName(title);

            if (titles.HasValue)
            {
                _trieService.IncrementPopularity(title, addCount);
                return Ok(new { title = titles.Value.title, popularity = titles.Value.popularity });
            }
            return NotFound();
        }

        [HttpGet("count-all")]
        public int GetAllCount()
        {
            var count = _trieService.GetAllCount();
            return count;
        }

        [HttpGet("count-search")]
        public int GetSearchCount(string title)
        {
            var count = _trieService.GetSearchCount(title);
            return count;
        }
    }
}