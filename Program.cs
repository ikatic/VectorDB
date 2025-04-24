namespace VectorDb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.IO;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Initializing Vector Database with ollama nomic-embed-text integration and collection support!");
        
        // Initialize the vector database with a base directory for collections
        var db = new VectorDb("collections");
        
        try
        {
            // Example texts for different collections
            var houseTexts = new[] {
                "My house is blue.",
                "The house has a red roof.",
                "The house has a beautiful garden."
            };
            
            var skyTexts = new[] {
                "The sky is blue.",
                "Stars twinkle in the night sky.",
                "The sky is cloudy today."
            };

            Console.WriteLine("\nAdding vectors to 'houses' collection...");
            foreach (var text in houseTexts)
            {
                var embedding = await GetEmbeddingAsync(text);
                var id = await db.AddAsync("houses", text, embedding);
                Console.WriteLine($"Added text: '{text}' with ID: {id}");
            }

            Console.WriteLine("\nAdding vectors to 'sky' collection...");
            foreach (var text in skyTexts)
            {
                var embedding = await GetEmbeddingAsync(text);
                var id = await db.AddAsync("sky", text, embedding);
                Console.WriteLine($"Added text: '{text}' with ID: {id}");
            }

            // List all collections
            Console.WriteLine("\nAvailable collections:");
            foreach (var collection in db.ListCollections())
            {
                Console.WriteLine($"- {collection}");
            }

            // Search in houses collection
            Console.WriteLine("\nSearching in 'houses' collection for 'house with garden'...");
            var queryEmbedding = await GetEmbeddingAsync("house with garden");
            var houseResults = db.AnnSearch("houses", queryEmbedding, 2);
            foreach (var result in houseResults)
            {
                Console.WriteLine($"Doc ID: {result.DocId}, Score: {result.Score}");
            }

            // Search in sky collection
            Console.WriteLine("\nSearching in 'sky' collection for 'night stars'...");
            queryEmbedding = await GetEmbeddingAsync("night stars");
            var skyResults = db.AnnSearch("sky", queryEmbedding, 2);
           
            foreach (var result in skyResults)
            {
                Console.WriteLine($"Doc ID: {result.DocId}, Score: {result.Score}");
            }

            // Remove a document from houses collection
            Console.WriteLine("\nRemoving 'My house is blue' from 'houses' collection...");
            await db.RemoveAsync("houses", "My house is blue.");

            // Delete sky collection
            Console.WriteLine("\nDeleting 'sky' collection...");
            db.DeleteCollection("sky");

            // List remaining collections
            Console.WriteLine("\nRemaining collections:");
            foreach (var collection in db.ListCollections())
            {
                Console.WriteLine($"- {collection}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static async Task<float[]> GetEmbeddingAsync(string prompt)
    {
        using var client = new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
        var payload = JsonSerializer.Serialize(new { model = "nomic-embed-text", prompt });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync("/api/embeddings", content);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"HTTP {response.StatusCode}; Body: {errorBody}");
            }

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            // support both "embeddings" and "embedding"
            if (!doc.RootElement.TryGetProperty("embeddings", out var arr) &&
                !doc.RootElement.TryGetProperty("embedding", out arr))
            {
                throw new FormatException(
                    "Invalid response: missing 'embedding' or 'embeddings' array.");
            }

            if (arr.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException(
                    "Invalid response: 'embedding' is not an array.");
            }

            int len = arr.GetArrayLength();
            var result = new float[len];
            for (int i = 0; i < len; i++)
            {
                result[i] = arr[i].GetSingle();
            }
            return result;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Request error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"JSON parse error: {ex.Message}");
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine($"Format error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
        }

        return Array.Empty<float>();
    }
}




