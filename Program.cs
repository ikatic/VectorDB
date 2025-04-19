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
            
            // Get embeddings from ollama running on localhost:11434
            var embedding1 = await GetEmbeddingAsync(text1);
            var embedding2 = await GetEmbeddingAsync(text2);
            
            // Store the embeddings with user-provided IDs
            string autoId1 = await db.AddAsync("house_description", embedding1);
            string autoId2 = await db.AddAsync("sky_description", embedding2);
            
            Console.WriteLine($"Added vectors with auto-generated IDs: {autoId1}, {autoId2}");
            
            // Search for similar documents
            var results = db.Search(embedding1, 5);
            
            foreach (var result in results)
            {
                Console.WriteLine($"Auto ID: {result.Id}, User ID: {result.DocId}, Score: {result.Score}");
            }
            
            // Remove a vector by user ID
            bool removed = await db.RemoveAsync("house_description");
            if (removed)
            {
                Console.WriteLine("Successfully removed vector with user ID 'house_description'");
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
    private readonly List<(string Id, string DocId, float[] Embedding)> _vectors = new();
    private long _totalBytes = 0;
    private readonly long _maxBytes = 4L * 1024 * 1024 * 1024; // 4 GB
    private readonly string _persistenceFile;
    private int _nextAutoId = 1;

    public VectorDb(string persistenceFile = null)
    {
        _persistenceFile = persistenceFile;
        if (!string.IsNullOrEmpty(_persistenceFile) && File.Exists(_persistenceFile))
        {
            Console.WriteLine($"Loading database from file: {_persistenceFile}");
            LoadAsync().Wait();
            Console.WriteLine($"Loaded {_vectors.Count} vectors from file");
        }
        else if (!string.IsNullOrEmpty(_persistenceFile))
        {
            Console.WriteLine($"No existing database file found at: {_persistenceFile}");
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(_persistenceFile))
        {
            Console.WriteLine("No persistence file specified, skipping save");
            return;
        }

        try
        {
            Console.WriteLine($"Saving {_vectors.Count} vectors to file: {_persistenceFile}");
            var data = new
            {
                Vectors = _vectors.Select(v => new
                {
                    v.Id,
                    v.DocId,
                    Embedding = v.Embedding
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(data, options);
            await File.WriteAllTextAsync(_persistenceFile, json);
            Console.WriteLine($"Successfully saved database to file: {_persistenceFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving database to file: {ex.Message}");
            throw;
        }
    }

    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_persistenceFile) || !File.Exists(_persistenceFile))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(_persistenceFile);
            var data = JsonSerializer.Deserialize<VectorDbData>(json);

            if (data?.Vectors == null)
            {
                Console.WriteLine("Invalid database file format");
                return;
            }

            _vectors.Clear();
            _totalBytes = 0;

            foreach (var vector in data.Vectors)
            {
                if (vector.Embedding?.Length != 768)
                {
                    Console.WriteLine($"Warning: Skipping invalid vector with ID {vector.Id} - incorrect embedding length");
                    continue;
                }
                await AddAsync(vector.DocId, vector.Embedding);
            }

            // Verify loaded data
            if (_vectors.Count != data.Vectors.Count)
            {
                Console.WriteLine($"Warning: Loaded {_vectors.Count} vectors but expected {data.Vectors.Count}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading database from file: {ex.Message}");
            throw;
        }
    }

    public async Task<string> AddAsync(string docId, float[] embedding)
    {
        if (embedding.Length != 768)
        {
            throw new Exception($"Embedding length must be 768. Got: {embedding.Length}");
        }
        long vectorSize = embedding.Length * sizeof(float); // 12,288 bytes

        if (_totalBytes + vectorSize > _maxBytes)
        {
            throw new Exception($"Cannot add vector: memory limit exceeded.");
        }

        string autoId = _nextAutoId.ToString();
        _vectors.Add((autoId, docId, embedding));
        _totalBytes += vectorSize;
        _nextAutoId++;
        
        Console.WriteLine($"Added vector with ID: {autoId}, Doc ID: {docId}, size: {vectorSize} bytes, total used: {_totalBytes} bytes.");
        
        try
        {
            await SaveAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving after adding vector: {ex.Message}");
            // Don't throw here to allow the operation to complete even if save fails
        }
        
        return autoId;
    }

    public async Task<bool> RemoveAsync(string docId)
    {
        var item = _vectors.FirstOrDefault(v => v.DocId == docId);
        if (item != default)
        {
            long vectorSize = item.Embedding.Length * sizeof(float);
            _vectors.Remove(item);
            _totalBytes -= vectorSize;
            Console.WriteLine($"Removed vector with Doc ID: '{docId}', freed: {vectorSize} bytes, total used: {_totalBytes} bytes.");
            
            try
            {
                await SaveAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving after removing vector: {ex.Message}");
                // Don't throw here to allow the operation to complete even if save fails
            }
            
            return true;
        }
        Console.WriteLine($"Vector with Doc ID '{docId}' not found.");
        return false;
    }

    public List<(string Id, string DocId, float Score)> Search(float[] queryVector, int topK = 5)
    {
        if (queryVector.Length != 768)
        {
            Console.WriteLine($"Query vector must be 768-dim. Got: {queryVector.Length}");
            return new();
        }

        return _vectors
            .Select(v => (v.Id, v.DocId, Score: CosineSimilarity(queryVector, v.Embedding)))
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

// Helper class for JSON serialization
class VectorDbData
{
    public List<VectorData> Vectors { get; set; }
}

class VectorData
{
    public string Id { get; set; }
    public string DocId { get; set; }
    public float[] Embedding { get; set; }
}


