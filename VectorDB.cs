namespace VectorDb;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.IO;

public class Collection
{
    private readonly List<(string Id, string DocId, float[] Embedding)> _vectors = new();
    private long _totalBytes = 0;
    private readonly long _maxBytes = 1L * 1024 * 1024 * 1024; // 1 GB per collection
    private readonly string _persistenceFile;
    private int _nextAutoId = 1;
    public string Name { get; }

    private readonly Dictionary<string, List<(string Id, string DocId, float[] Embedding)>> _lshBuckets = new();
    private readonly int _numPlanes = 10;
    private readonly List<float[]> _randomPlanes;


    public Collection(string name, string baseDir)
    {
        _randomPlanes = new();
        var rng = new Random();
        for (int i = 0; i < _numPlanes; i++)
        {
            var plane = new float[768];
            for (int j = 0; j < 768; j++)
                plane[j] = (float)(rng.NextDouble() * 2 - 1);
            _randomPlanes.Add(plane);
        }

        Name = name;
        _persistenceFile = Path.Combine(baseDir, $"{name}.json");
        if (File.Exists(_persistenceFile))
        {
            Console.WriteLine($"Loading collection '{name}' from file: {_persistenceFile}");
            LoadAsync().Wait();
            Console.WriteLine($"Loaded {_vectors.Count} vectors from collection '{name}'");
        }
        else
        {
            Console.WriteLine($"Creating new collection '{name}'");
        }
    }

        private string ComputeHash(float[] vector)
        {
            var bits = _randomPlanes
                .Select(p => DotProduct(p, vector) >= 0 ? '1' : '0')
                .ToArray();
            return new string(bits);
        }

        private float DotProduct(float[] a, float[] b)
        {
            float sum = 0;
            for (int i = 0; i < a.Length; i++)
                sum += a[i] * b[i];
            return sum;
        }
    private async Task SaveAsync()
    {
        try
        {
            Console.WriteLine($"Saving {_vectors.Count} vectors to collection file: {_persistenceFile}");
            var data = new
            {
                Name = Name,
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
            Console.WriteLine($"Successfully saved collection '{Name}' to file");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving collection '{Name}' to file: {ex.Message}");
            throw;
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(_persistenceFile);
            var data = JsonSerializer.Deserialize<CollectionData>(json);

            if (data?.Vectors == null)
            {
                Console.WriteLine($"Invalid collection file format for '{Name}'");
                return;
            }

            _vectors.Clear();
            _totalBytes = 0;

            foreach (var vector in data.Vectors)
            {
                if (vector.Embedding?.Length != 768)
                {
                    Console.WriteLine($"Warning: Skipping invalid vector with ID {vector.Id} in collection '{Name}' - incorrect embedding length");
                    continue;
                }
                await AddAsync(vector.DocId, vector.Embedding);
            }

            if (_vectors.Count != data.Vectors.Count)
            {
                Console.WriteLine($"Warning: Loaded {_vectors.Count} vectors but expected {data.Vectors.Count} in collection '{Name}'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading collection '{Name}' from file: {ex.Message}");
            throw;
        }
    }

    public async Task<string> AddAsync(string docId, float[] embedding)
    {
        if (embedding.Length != 768)
        {
            throw new Exception($"Embedding length must be 768. Got: {embedding.Length}");
        }
        long vectorSize = embedding.Length * sizeof(float);

        if (_totalBytes + vectorSize > _maxBytes)
        {
            throw new Exception($"Cannot add vector: collection '{Name}' memory limit exceeded.");
        }

        string autoId = _nextAutoId.ToString();
        _vectors.Add((autoId, docId, embedding));
        _totalBytes += vectorSize;
        _nextAutoId++;
        
        string hash = ComputeHash(embedding);
        if (!_lshBuckets.ContainsKey(hash))
            _lshBuckets[hash] = new List<(string, string, float[])>();
        _lshBuckets[hash].Add((autoId, docId, embedding));

        Console.WriteLine($"Added vector with ID: {autoId}, Doc ID: {docId}, size: {vectorSize} bytes to collection '{Name}', total used: {_totalBytes} bytes.");
        
        try
        {
            await SaveAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving after adding vector to collection '{Name}': {ex.Message}");
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
            Console.WriteLine($"Removed {itemsToRemove.Count} vector(s) with Doc ID: '{docId}' from collection '{Name}', freed: {totalFreedBytes} bytes, total used: {_totalBytes} bytes.");
            
            try
            {
                await SaveAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving after removing vectors from collection '{Name}': {ex.Message}");
            }
            
            return true;
        }
        Console.WriteLine($"No vectors found with Doc ID '{docId}' in collection '{Name}'.");
        return false;
    }

    public List<(string Id, string DocId, float Score)> AnnSearch(float[] queryVector, int topK = 5)
    {
        string hash = ComputeHash(queryVector);
        if (!_lshBuckets.TryGetValue(hash, out var candidates))
        {
            Console.WriteLine($"No LSH bucket for query hash.");
            return new();
        }

        return candidates
            .Select(v => (v.Id, v.DocId, Score: CosineSimilarity(queryVector, v.Embedding)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
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

public class VectorDb
{
    private readonly Dictionary<string, Collection> _collections = new();
    private readonly string _baseDir;
    private const int MaxCollections = 5;

    public VectorDb(string baseDir = "collections")
    {
        _baseDir = baseDir;
        Directory.CreateDirectory(baseDir);
        LoadExistingCollections();
    }

    private void LoadExistingCollections()
    {
        foreach (var file in Directory.GetFiles(_baseDir, "*.json"))
        {
            var collectionName = Path.GetFileNameWithoutExtension(file);
            _collections[collectionName] = new Collection(collectionName, _baseDir);
        }
    }

    public Collection GetOrCreateCollection(string name)
    {
        if (!_collections.TryGetValue(name, out var collection))
        {
            if (_collections.Count >= MaxCollections)
            {
                throw new InvalidOperationException($"Cannot create collection '{name}': Maximum number of collections ({MaxCollections}) reached.");
            }
            collection = new Collection(name, _baseDir);
            _collections[name] = collection;
        }
        return collection;
    }

    public bool DeleteCollection(string name)
    {
        if (_collections.TryGetValue(name, out var collection))
        {
            _collections.Remove(name);
            var filePath = Path.Combine(_baseDir, $"{name}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Console.WriteLine($"Collection '{name}' deleted successfully.");
                return true;
            }
        }
        Console.WriteLine($"Collection '{name}' not found.");
        return false;
    }

    public IEnumerable<string> ListCollections()
    {
        return _collections.Keys;
    }

    public async Task<string> AddAsync(string collectionName, string docId, float[] embedding)
    {
        var collection = GetOrCreateCollection(collectionName);
        return await collection.AddAsync(docId, embedding);
    }

    public async Task<bool> RemoveAsync(string collectionName, string docId)
    {
        if (_collections.TryGetValue(collectionName, out var collection))
        {
            return await collection.RemoveAsync(docId);
        }
        Console.WriteLine($"Collection '{collectionName}' not found.");
        return false;
    }

    public List<(string Id, string DocId, float Score)> Search(string collectionName, float[] queryVector, int topK = 5)
    {
        if (_collections.TryGetValue(collectionName, out var collection))
        {
            return collection.Search(queryVector, topK);
        }
        Console.WriteLine($"Collection '{collectionName}' not found.");
        return new();
    }
    public List<(string Id, string DocId, float Score)> AnnSearch(string collectionName, float[] queryVector, int topK = 5)
    {
        if (_collections.TryGetValue(collectionName, out var collection))
            return collection.AnnSearch(queryVector, topK);
        return new();
    }

}

class CollectionData
{
    public string Name { get; set; }
    public List<VectorData> Vectors { get; set; }
}

class VectorData
{
    public string Id { get; set; }
    public string DocId { get; set; }
    public float[] Embedding { get; set; }
}