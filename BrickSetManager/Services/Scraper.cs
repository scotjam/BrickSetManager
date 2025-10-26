using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using BrickSetManager.Models;

namespace BrickSetManager.Services
{
    public class Scraper
    {
        private readonly HttpClient _httpClient;
        private readonly ImageDownloader _imageDownloader;
        private readonly Random _random;

        public Scraper()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _imageDownloader = new ImageDownloader();
            _random = new Random();
        }

        public async Task<(BrickSet Set, List<InventoryItem> Inventory)> ScrapeSetAsync(string setNumber, int inventoryVersion = 2)
        {
            try
            {
                // STEP 1: Download HTML pages like a normal browser visit
                string setUrl = $"https://www.bricklink.com/v2/catalog/catalogitem.page?S={setNumber}";

                // Select the correct inventory URL based on version
                // Version 1 = Old version (v=1), Version 2+ = Latest/New version (no v parameter)
                string inventoryUrl = inventoryVersion == 1
                    ? $"https://www.bricklink.com/catalogItemInv.asp?S={setNumber}&v=1"
                    : $"https://www.bricklink.com/catalogItemInv.asp?S={setNumber}";

                // Visit the set page first
                string setHtml = await _httpClient.GetStringAsync(setUrl);

                // Natural delay before navigating to inventory page (like a human clicking)
                await Task.Delay(_random.Next(500, 1500));

                // Now visit the inventory page
                string inventoryHtml = await _httpClient.GetStringAsync(inventoryUrl);

                // STEP 2: Parse set information from HTML
                var setDoc = new HtmlDocument();
                setDoc.LoadHtml(setHtml);

                // The set name is in an element with id='item-name-title' (could be h1 or span)
                var setNameNode = setDoc.DocumentNode.SelectSingleNode("//*[@id='item-name-title']");
                // Decode HTML entities like &amp; (&) and &aacute; (á)
                string setName = setNameNode != null
                    ? WebUtility.HtmlDecode(setNameNode.InnerText.Trim())
                    : setNumber;

                // Extract release year and piece count from the item details section
                int? releaseYear = ExtractReleaseYear(setDoc);
                int? pieceCount = ExtractPieceCount(setDoc);

                // Check if this set has multiple inventory versions
                bool hasMultipleVersions = await HasMultipleVersionsAsync(setNumber);

                // STEP 3: Parse inventory table from HTML (all local processing)
                var inventoryDoc = new HtmlDocument();
                inventoryDoc.LoadHtml(inventoryHtml);

                var inventoryData = ParseInventoryTableFromHtml(inventoryDoc, setNumber);

                // STEP 4: Collect all image URLs (like browser discovering resources on page)
                var imageUrls = new List<(string key, string url)>();

                // Add set image
                string setImageUrl = $"https://img.bricklink.com/ItemImage/SN/0/{setNumber}.png";
                imageUrls.Add(("set", setImageUrl));

                // Add all part images
                foreach (var item in inventoryData)
                {
                    string imageUrl = $"https://img.bricklink.com/ItemImage/PT/{item.ColorID}/{item.PartNumber}.t1.png";
                    string key = $"{item.PartNumber}_{item.ColorID}";
                    imageUrls.Add((key, imageUrl));
                }

                // STEP 5: Download all images like a browser would load page resources
                // Browsers typically load multiple images concurrently (usually 6-8 at a time)
                // and there's natural variation in timing
                var imageDict = await DownloadImagesLikeBrowser(imageUrls);

                // STEP 6: Build final data structures
                var brickSet = new BrickSet
                {
                    SetNumber = setNumber,
                    SetName = setName,
                    SetURL = setUrl,
                    DateAdded = DateTime.Now,
                    SetImageData = imageDict.ContainsKey("set") ? imageDict["set"] : null,
                    ReleaseYear = releaseYear,
                    PieceCount = pieceCount,
                    HasMultipleVersions = hasMultipleVersions
                };

                var inventory = new List<InventoryItem>();
                foreach (var item in inventoryData)
                {
                    string key = $"{item.PartNumber}_{item.ColorID}";
                    item.BrickDetail.PartImageData = imageDict.ContainsKey(key) ? imageDict[key] : null;
                    inventory.Add(item);
                }

                return (brickSet, inventory);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error scraping set {setNumber}: {ex.Message}", ex);
            }
        }

        private async Task<Dictionary<string, byte[]>> DownloadImagesLikeBrowser(List<(string key, string url)> imageUrls)
        {
            var results = new Dictionary<string, byte[]>();

            // Browsers typically load 6-8 resources concurrently
            const int concurrentDownloads = 6;

            // Process images in batches like a browser would
            for (int i = 0; i < imageUrls.Count; i += concurrentDownloads)
            {
                var batch = imageUrls.Skip(i).Take(concurrentDownloads).ToList();
                var batchTasks = new List<Task<(string key, byte[] data)>>();

                foreach (var (key, url) in batch)
                {
                    batchTasks.Add(DownloadImageWithKeyAsync(key, url));
                }

                // Download this batch concurrently (like browser parallel connections)
                var batchResults = await Task.WhenAll(batchTasks);

                // Add results to dictionary
                foreach (var (key, data) in batchResults)
                {
                    results[key] = data;
                }

                // Small natural delay before next batch (browser processing time)
                // Only delay if there are more batches to process
                if (i + concurrentDownloads < imageUrls.Count)
                {
                    await Task.Delay(_random.Next(100, 300));
                }
            }

            return results;
        }

        private async Task<(string key, byte[] data)> DownloadImageWithKeyAsync(string key, string imageUrl)
        {
            var imageData = await _imageDownloader.DownloadImageAsync(imageUrl);
            return (key, imageData);
        }

        private List<InventoryItem> ParseInventoryTableFromHtml(HtmlDocument doc, string setNumber)
        {
            var inventory = new List<InventoryItem>();

            // Find the inventory table
            var inventoryTable = doc.DocumentNode.SelectSingleNode("//table[@id='id-main-legacy-table']");
            if (inventoryTable == null)
                return inventory;

            var rows = inventoryTable.SelectNodes(".//tr[position()>1]"); // Skip header row
            if (rows == null)
                return inventory;

            foreach (var row in rows)
            {
                try
                {
                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 4)
                        continue;

                    // Cell 2 contains the part number link
                    var partLink = cells[2].SelectSingleNode(".//a");
                    if (partLink == null)
                        continue;

                    string partHref = partLink.GetAttributeValue("href", "");
                    var partMatch = Regex.Match(partHref, @"P=([^&]+)");
                    if (!partMatch.Success)
                        continue;

                    string partNumber = partMatch.Groups[1].Value;

                    // Extract color ID from the link
                    var colorMatch = Regex.Match(partHref, @"idColor=(\d+)");
                    if (!colorMatch.Success)
                        continue;

                    int colorID = int.Parse(colorMatch.Groups[1].Value);

                    // Cell 1 contains the quantity
                    var qtyCell = cells[1];
                    // Decode HTML entities (like &nbsp;) before parsing
                    string qtyText = WebUtility.HtmlDecode(qtyCell.InnerText).Trim();
                    int quantity = int.TryParse(qtyText, out int qty) ? qty : 0;

                    // Cell 3 contains description: "Color Part Name"
                    // Decode HTML entities (like &nbsp;) before processing
                    string description = WebUtility.HtmlDecode(cells[3].InnerText).Trim();
                    // Remove "Catalog:" and everything after it
                    int catalogIndex = description.IndexOf("Catalog:");
                    if (catalogIndex > 0)
                        description = description.Substring(0, catalogIndex).Trim();

                    // Extract color name and part name
                    var (colorName, partName) = ParseColorAndPartName(description);

                    // Parse dimensions from part name
                    var (length, width) = ParseDimensions(partName);

                    // Create brick detail (image will be assigned later)
                    var brickDetail = new BrickDetail
                    {
                        PartNumber = partNumber,
                        ColorID = colorID,
                        ColorName = colorName,
                        PartName = partName,
                        PartURL = $"https://www.bricklink.com/v2/catalog/catalogitem.page?P={partNumber}&idColor={colorID}",
                        PriceGuideURL = $"https://www.bricklink.com/v2/catalog/catalogitem.page?P={partNumber}&idColor={colorID}#T=P",
                        Length = length,
                        Width = width
                    };

                    // Create inventory item
                    var inventoryItem = new InventoryItem
                    {
                        SetNumber = setNumber,
                        PartNumber = partNumber,
                        ColorID = colorID,
                        Quantity = quantity,
                        BrickDetail = brickDetail
                    };

                    inventory.Add(inventoryItem);
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other parts
                    Console.WriteLine($"Error parsing inventory row: {ex.Message}");
                }
            }

            return inventory;
        }

        private (string colorName, string partName) ParseColorAndPartName(string description)
        {
            string colorName = "";
            string partName = description; // Keep full description as part name

            // List of "final color words" - the last word that appears in color names
            // Based on analyzing the comprehensive color list
            // NOTE: "Trans" and "Opaque" are NOT included because they are prefixes, not final words
            // (e.g., "Trans-Black" ends in "Black", not "Trans")
            string[] finalColorWords = {
                "Brass", "Silver", "Gold", "Copper", // Metallic endings
                "Gray", "Grey", // Grayscale
                "Green", "Blue", "Red", "Yellow", "Orange", "Pink", "Purple", "Violet", "Brown", // Basic colors
                "Tan", "Black", "White", // Neutrals
                "Aqua", "Azure", "Lavender", "Magenta", "Lime", "Coral", "Turquoise", "Nougat", // Special colors
                "Salmon", "Lilac", "Sienna", "Umber", "Rust", // More special colors
                "Clear", // Transparency (Trans-Clear, Satin Trans-Clear, etc.)
            };

            // Split the description into words, treating both spaces AND hyphens as separators
            var words = description.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);

            // Find the first word that is a "final color word"
            int colorEndIndex = -1;
            for (int i = 0; i < words.Length; i++)
            {
                if (finalColorWords.Any(fcw => fcw.Equals(words[i], StringComparison.OrdinalIgnoreCase)))
                {
                    colorEndIndex = i;
                    break; // Found the color ending, stop here
                }
            }

            if (colorEndIndex >= 0)
            {
                // Find where this word ends in the original description
                int charPosition = 0;
                for (int i = 0; i <= colorEndIndex; i++)
                {
                    int wordStart = description.IndexOf(words[i], charPosition, StringComparison.OrdinalIgnoreCase);
                    if (wordStart >= 0)
                    {
                        charPosition = wordStart + words[i].Length;
                    }
                }

                // Extract just the color (for the Color column)
                colorName = description.Substring(0, charPosition).Trim();

                // Part name remains the full description including the color
                partName = description;
            }

            return (colorName, partName);
        }

        private (int length, int width) ParseDimensions(string partName)
        {
            // Try to extract dimensions from part name like "Brick 2 x 2" or "Plate 1 x 4"
            var match = Regex.Match(partName, @"(\d+)\s*x\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                int length = int.Parse(match.Groups[1].Value);
                int width = int.Parse(match.Groups[2].Value);
                return (length, width);
            }

            return (0, 0);
        }

        private int? ExtractReleaseYear(HtmlDocument doc)
        {
            try
            {
                // Look for "Year Released:" text followed by a link
                var textNodes = doc.DocumentNode.SelectNodes("//text()[contains(., 'Year Released:')]");
                if (textNodes != null)
                {
                    foreach (var textNode in textNodes)
                    {
                        // Get the parent element which should have the year link as a sibling
                        var parent = textNode.ParentNode;
                        if (parent != null)
                        {
                            // Look for the year in a link immediately after the text
                            var linkNode = parent.SelectSingleNode(".//a[@class='links' and contains(@href, 'itemYear=')]");
                            if (linkNode != null)
                            {
                                string yearText = linkNode.InnerText.Trim();
                                if (int.TryParse(yearText, out int year))
                                {
                                    return year;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // If extraction fails, return null
            }
            return null;
        }

        private int? ExtractPieceCount(HtmlDocument doc)
        {
            try
            {
                // Look for a link containing "Parts" text (e.g. "4383 Parts")
                var partsLink = doc.DocumentNode.SelectSingleNode("//a[@class='links' and contains(@href, 'catalogItemInv.asp') and contains(text(), 'Parts')]");
                if (partsLink != null)
                {
                    string partsText = partsLink.InnerText.Trim();
                    // Extract the number before "Parts"
                    var match = Regex.Match(partsText, @"(\d+)\s*Parts", RegexOptions.IgnoreCase);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int pieces))
                    {
                        return pieces;
                    }
                }
            }
            catch
            {
                // If extraction fails, return null
            }
            return null;
        }

        public async Task<bool> HasMultipleVersionsAsync(string setNumber)
        {
            try
            {
                // Check the regular inventory page for "Older version" link
                string inventoryUrl = $"https://www.bricklink.com/catalogItemInv.asp?S={setNumber}";
                string inventoryHtml = await _httpClient.GetStringAsync(inventoryUrl);

                var doc = new HtmlDocument();
                doc.LoadHtml(inventoryHtml);

                // Look for "Older version" link
                // The link typically has text like "Older version" or contains "v=1" in the href
                var olderVersionLink = doc.DocumentNode.SelectSingleNode("//a[contains(text(), 'Older') or contains(@href, 'v=1')]");

                return olderVersionLink != null;
            }
            catch
            {
                // If we can't determine, default to false (single version)
                return false;
            }
        }
    }
}
