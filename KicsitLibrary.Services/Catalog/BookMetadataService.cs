using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Services.Catalog
{
    public class BookMetadataService : IBookMetadataService
    {
        private readonly HttpClient _httpClient;

        public BookMetadataService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Ilm-o-Kutub-System/1.0 (academic application)");
            }
        }

        public async Task<BookMetadataResult?> FetchByIsbnAsync(string isbn)
        {
            if (string.IsNullOrWhiteSpace(isbn))
            {
                return null;
            }

            var cleanIsbn = new string(isbn.Where(char.IsDigit).ToArray());
            if (cleanIsbn.Length != 10 && cleanIsbn.Length != 13)
            {
                cleanIsbn = isbn.Trim();
            }

            try
            {
                var googleResult = await FetchFromGoogleBooksAsync(cleanIsbn);
                if (googleResult != null)
                {
                    return googleResult;
                }
            }
            catch
            {
                // Fallback on failure
            }

            try
            {
                var openLibraryResult = await FetchFromOpenLibraryAsync(cleanIsbn);
                if (openLibraryResult != null)
                {
                    return openLibraryResult;
                }
            }
            catch
            {
                // Fail silently
            }

            return null;
        }

        private async Task<BookMetadataResult?> FetchFromGoogleBooksAsync(string isbn)
        {
            var url = $"https://www.googleapis.com/books/v1/volumes?q=isbn:{isbn}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            {
                return null;
            }

            var volumeInfo = items[0].GetProperty("volumeInfo");
            var result = new BookMetadataResult();

            if (volumeInfo.TryGetProperty("title", out var titleProp))
            {
                result.Title = titleProp.GetString() ?? string.Empty;
            }

            if (volumeInfo.TryGetProperty("subtitle", out var subtitleProp))
            {
                result.SubTitle = subtitleProp.GetString() ?? string.Empty;
            }

            if (volumeInfo.TryGetProperty("description", out var descProp))
            {
                result.Description = descProp.GetString() ?? string.Empty;
            }

            if (volumeInfo.TryGetProperty("publisher", out var pubProp))
            {
                result.Publisher = pubProp.GetString() ?? string.Empty;
            }

            if (volumeInfo.TryGetProperty("publishedDate", out var dateProp))
            {
                var dateStr = dateProp.GetString();
                if (!string.IsNullOrEmpty(dateStr) && dateStr.Length >= 4)
                {
                    if (int.TryParse(dateStr.Substring(0, 4), out var year))
                    {
                        result.PublicationYear = year;
                    }
                }
            }

            if (volumeInfo.TryGetProperty("authors", out var authorsProp))
            {
                foreach (var author in authorsProp.EnumerateArray())
                {
                    var authorStr = author.GetString();
                    if (!string.IsNullOrWhiteSpace(authorStr))
                    {
                        result.Authors.Add(authorStr.Trim());
                    }
                }
            }

            if (volumeInfo.TryGetProperty("imageLinks", out var imageLinksProp))
            {
                if (imageLinksProp.TryGetProperty("thumbnail", out var thumbnailProp))
                {
                    result.CoverImageUrl = thumbnailProp.GetString() ?? string.Empty;
                    if (result.CoverImageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        result.CoverImageUrl = "https://" + result.CoverImageUrl.Substring(7);
                    }
                }
            }

            if (volumeInfo.TryGetProperty("language", out var langProp))
            {
                var lang = langProp.GetString();
                result.Language = string.Equals(lang, "ur", StringComparison.OrdinalIgnoreCase) ? "Urdu" : "English";
            }

            return result;
        }

        private async Task<BookMetadataResult?> FetchFromOpenLibraryAsync(string isbn)
        {
            var bibKey = $"ISBN:{isbn}";
            var url = $"https://openlibrary.org/api/books?bibkeys={bibKey}&format=json&jscmd=data";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty(bibKey, out var bookData))
            {
                return null;
            }

            var result = new BookMetadataResult();

            if (bookData.TryGetProperty("title", out var titleProp))
            {
                result.Title = titleProp.GetString() ?? string.Empty;
            }

            if (bookData.TryGetProperty("subtitle", out var subtitleProp))
            {
                result.SubTitle = subtitleProp.GetString() ?? string.Empty;
            }

            if (bookData.TryGetProperty("notes", out var notesProp))
            {
                result.Description = notesProp.GetString() ?? string.Empty;
            }

            if (bookData.TryGetProperty("publishers", out var publishersProp))
            {
                var firstPub = publishersProp.EnumerateArray().FirstOrDefault();
                if (firstPub.ValueKind == JsonValueKind.Object && firstPub.TryGetProperty("name", out var nameProp))
                {
                    result.Publisher = nameProp.GetString() ?? string.Empty;
                }
            }

            if (bookData.TryGetProperty("publish_date", out var dateProp))
            {
                var dateStr = dateProp.GetString();
                if (!string.IsNullOrEmpty(dateStr))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(dateStr, @"\b\d{4}\b");
                    if (match.Success && int.TryParse(match.Value, out var year))
                    {
                        result.PublicationYear = year;
                    }
                }
            }

            if (bookData.TryGetProperty("authors", out var authorsProp))
            {
                foreach (var author in authorsProp.EnumerateArray())
                {
                    if (author.TryGetProperty("name", out var nameProp))
                    {
                        var name = nameProp.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            result.Authors.Add(name.Trim());
                        }
                    }
                }
            }

            if (bookData.TryGetProperty("cover", out var coverProp))
            {
                if (coverProp.TryGetProperty("large", out var largeProp))
                {
                    result.CoverImageUrl = largeProp.GetString() ?? string.Empty;
                }
                else if (coverProp.TryGetProperty("medium", out var mediumProp))
                {
                    result.CoverImageUrl = mediumProp.GetString() ?? string.Empty;
                }
            }

            return result;
        }
    }
}
