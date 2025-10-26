using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace BrickSetManager.Services
{
    public class ImageDownloader
    {
        private readonly HttpClient _httpClient;

        public ImageDownloader()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task<byte[]> DownloadImageAsync(string imageUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(imageUrl);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading image from {imageUrl}: {ex.Message}");
            }

            return null;
        }
    }
}
