namespace VectorDb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.IO;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Initializing Vector Database with ollama nomic-embed-text integration...!");
        
        // Initialize with persistence file
        var db = new VectorDb("vectordb.json");
        
        try
        {
            // Example text to embed
            string text1 = "My house is blue.";
            string text2 = "The sky is blue.";
            string text3 = "My house has a blue door.";
            
            // Get embeddings from ollama running on localhost:11434
            var embedding1 = await GetEmbeddingAsync(text1);
            var embedding2 = await GetEmbeddingAsync(text2);
            var embedding3 = await GetEmbeddingAsync(text3);
            
            // Store multiple embeddings with the same document ID
            string autoId1 = await db.AddAsync("doc_house", embedding1);
            string autoId2 = await db.AddAsync("doc_sky", embedding2);
            string autoId3 = await db.AddAsync("doc_house", embedding3); // Same doc_house ID
            
            Console.WriteLine($"Added vectors with auto-generated IDs: {autoId1}, {autoId2}, {autoId3}");
            
            // Search for similar documents
            var results = db.Search(embedding1, 5);
            
            foreach (var result in results)
            {
                Console.WriteLine($"Auto ID: {result.Id}, Doc ID: {result.DocId}, Score: {result.Score}");
            }
            
            // Remove all vectors with the same document ID
            bool removed = await db.RemoveAsync("doc_house");
            if (removed)
            {
                Console.WriteLine("Successfully removed all vectors with doc_house ID");
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




