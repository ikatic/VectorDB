namespace VectorDb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;

class Program
{
    static async Task Main(string[] args)
    {

        Console.WriteLine("Initializing Vector Database with ollama integration...!");
        var db = new VectorDb();
        
        try
        {
            // Example text to embed
            string text = "This is an example text that we want to embed and store in the vector database.";
            
            // Get embedding from ollama running on localhost:11434
            var embedding = await GetEmbeddingAsync(text);
            
            // Store the embedding in the database
            db.Add("doc1", embedding);
            
            // Search for similar documents
            var results = db.Search(embedding, 5);
            
            foreach (var result in results)
            {
                Console.WriteLine($"Id: {result.Id}, Score: {result.Score}");
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

class VectorDb
{
    private readonly List<(string Id, float[] Embedding)> _vectors = new();
    private long _totalBytes = 0;
    private readonly long _maxBytes = 4L * 1024 * 1024 * 1024; // 4 GB

    public void Add(string id, float[] embedding)
    {
        if (embedding.Length != 768)
        {
            throw new Exception($"Embedding length must be 768. Got: {embedding.Length}");
            
        }
        long vectorSize = embedding.Length * sizeof(float); // 12,288 bytes

        if (_totalBytes + vectorSize > _maxBytes)
        {
            throw new Exception($"Cannot add '{id}': memory limit exceeded.");
        }

        _vectors.Add((id, embedding));
        _totalBytes += vectorSize;
        Console.WriteLine($"Added '{id}', size: {vectorSize} bytes, total used: {_totalBytes} bytes.");
    }

    public List<(string Id, float Score)> Search(float[] queryVector, int topK = 5)
    {
        if (queryVector.Length != 768)
        {
            Console.WriteLine($"Query vector must be 768-dim. Got: {queryVector.Length}");
            return new();
        }

        return _vectors
            .Select(v => (v.Id, Score: CosineSimilarity(queryVector, v.Embedding)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }
}


