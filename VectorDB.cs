namespace VectorDb;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.IO;


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
        var itemsToRemove = _vectors.Where(v => v.DocId == docId).ToList();
        if (itemsToRemove.Any())
        {
            long totalFreedBytes = 0;
            foreach (var item in itemsToRemove)
            {
                long vectorSize = item.Embedding.Length * sizeof(float);
                _vectors.Remove(item);
                _totalBytes -= vectorSize;
                totalFreedBytes += vectorSize;
            }
            Console.WriteLine($"Removed {itemsToRemove.Count} vector(s) with Doc ID: '{docId}', freed: {totalFreedBytes} bytes, total used: {_totalBytes} bytes.");
            
            try
            {
                await SaveAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving after removing vectors: {ex.Message}");
                // Don't throw here to allow the operation to complete even if save fails
            }
            
            return true;
        }
        Console.WriteLine($"No vectors found with Doc ID '{docId}'.");
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