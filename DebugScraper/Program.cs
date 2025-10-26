using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

class Program
{
    static async Task Main(string[] args)
    {
        string setNumber = "21061-1";

        Console.WriteLine($"Testing extraction for set {setNumber}...");

        // Load the saved HTML
        if (!File.Exists("setpage.html"))
        {
            Console.WriteLine("setpage.html not found! Downloading...");
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            string setUrl = $"https://www.bricklink.com/v2/catalog/catalogitem.page?S={setNumber}";
            var html = await httpClient.GetStringAsync(setUrl);
            File.WriteAllText("setpage.html", html);
            Console.WriteLine("Saved to setpage.html");
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(File.ReadAllText("setpage.html"));

        // Test ExtractReleaseYear
        Console.WriteLine("\n=== Testing Release Year Extraction ===");
        var releaseYear = ExtractReleaseYear(doc);
        Console.WriteLine($"Release Year: {(releaseYear.HasValue ? releaseYear.Value.ToString() : "NULL")}");

        // Test ExtractPieceCount
        Console.WriteLine("\n=== Testing Piece Count Extraction ===");
        var pieceCount = ExtractPieceCount(doc);
        Console.WriteLine($"Piece Count: {(pieceCount.HasValue ? pieceCount.Value.ToString() : "NULL")}");

        // Show where these fields are in the HTML
        Console.WriteLine("\n=== Looking for 'Year released' in HTML ===");
        var yearNodes = doc.DocumentNode.SelectNodes("//dt[contains(text(), 'Year')]");
        if (yearNodes != null)
        {
            foreach (var node in yearNodes)
            {
                Console.WriteLine($"Found dt: {node.InnerText}");
                var dd = node.SelectSingleNode("following-sibling::dd[1]");
                if (dd != null)
                {
                    Console.WriteLine($"  Value: {dd.InnerText.Trim()}");
                }
            }
        }
        else
        {
            Console.WriteLine("No dt elements with 'Year' found");
        }

        Console.WriteLine("\n=== Looking for 'Pieces' in HTML ===");
        var piecesNodes = doc.DocumentNode.SelectNodes("//dt[contains(text(), 'Piece')]");
        if (piecesNodes != null)
        {
            foreach (var node in piecesNodes)
            {
                Console.WriteLine($"Found dt: {node.InnerText}");
                var dd = node.SelectSingleNode("following-sibling::dd[1]");
                if (dd != null)
                {
                    Console.WriteLine($"  Value: {dd.InnerText.Trim()}");
                }
            }
        }
        else
        {
            Console.WriteLine("No dt elements with 'Piece' found");
        }

        Console.WriteLine("\nDone!");
    }

    static int? ExtractReleaseYear(HtmlDocument doc)
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

    static int? ExtractPieceCount(HtmlDocument doc)
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
}
