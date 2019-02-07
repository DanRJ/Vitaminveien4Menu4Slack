﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Config;
using Microsoft.Azure.CognitiveServices.Search.ImageSearch;
using Microsoft.Azure.CognitiveServices.Search.ImageSearch.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;

namespace Api.ImageSearch
{
    public class ImageSearcher : IImageSearcher
    {
        private readonly AzureCognitiveConfig _config;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ImageSearcher> _logger;



        public ImageSearcher(
            IOptions<AzureCognitiveConfig> azureCognitiveOptions,
            IMemoryCache memoryCachecache,
            ILoggerFactory loggerFactory)
        {
            _cache = memoryCachecache;
            _logger = loggerFactory.CreateLogger<ImageSearcher>();
            _config = azureCognitiveOptions.Value;

            _logger.LogWarning($"Config values: '{_config.FaceApi}' & '{_config.Endpoint}'.");
        }

        public async Task<string> SearchForMeal(string meal)
        {
            var searchTerm = meal;

            while (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var url = await Search(searchTerm);

                if (!string.IsNullOrWhiteSpace(url)) return url;

                searchTerm = RemoveLastWord(searchTerm);
            }

            return null;
        }

        private async Task<string> Search(string searchTerm)
        {
            var cacheKey = $"search-{searchTerm}";
            if (_cache.TryGetValue(cacheKey, out string cacheEntry)) return cacheEntry;

            var client = new ImageSearchClient(new ApiKeyServiceClientCredentials(_config.FaceApi))
            {
                Endpoint = _config.Endpoint
            };

            HttpOperationResponse<Images> results;
            try
            {
                _logger.LogInformation($"Image search for '{searchTerm}'.");
                results = await client.Images.SearchWithHttpMessagesAsync(searchTerm, safeSearch: "Moderate");
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Image search failed.");
                return null;
            }
            cacheEntry = results?.Body?.Value?.FirstOrDefault()?.ContentUrl;

            _cache.Set(cacheKey, cacheEntry, DateTime.Now.AddDays(1));

            return cacheEntry;
        }

        private static string RemoveLastWord(string searchTerm)
        {
            var index = searchTerm.LastIndexOf(' ');
            return index == -1 ? null : searchTerm.Substring(0, index).Trim();
        }
    }
}
