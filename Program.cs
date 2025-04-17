namespace VectorDb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using OpenAI_API;
using OpenAI_API.Embedding;
using Microsoft.Extensions.Configuration;

class Program
{
    static async Task Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        // Get API key from secrets
        var apiKey = configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: OpenAI API key not found in user secrets.");
            Console.WriteLine("Please set your API key using: dotnet user-secrets set \"OpenAI:ApiKey\" \"your-api-key\"");
            return;
        }

        Console.WriteLine("Hello, World!");
        var db = new VectorDb();
        
        // Initialize OpenAI API with the secure API key
        var openAi = new OpenAIAPI(apiKey);

        try
        {
            // Example text to embed
            string text = "This is an example text that we want to embed and store in the vector database.";
            
            // Get embedding from OpenAI
            var embedding = await db.GetEmbeddingAsync(openAi, text);
            
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
}

class VectorDb
{
    private readonly List<(string Id, float[] Embedding)> _vectors = new();
    private long _totalBytes = 0;
    private readonly long _maxBytes = 4L * 1024 * 1024 * 1024; // 4 GB

    public async Task<float[]> GetEmbeddingAsync(OpenAIAPI openAi, string text)
    {
        var embeddingRequest = new EmbeddingRequest
        {
            Input = text,
            Model = "text-embedding-3-large"
        };

        var result = await openAi.Embeddings.CreateEmbeddingAsync(embeddingRequest);
        return result.Data[0].Embedding;
    }

    public void Add(string id, float[] embedding)
    {
        if (embedding.Length != 3072)
        {
            Console.WriteLine($"Embedding length must be 3072. Got: {embedding.Length}");
            return;
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
        if (queryVector.Length != 3072)
        {
            Console.WriteLine($"Query vector must be 3072-dim. Got: {queryVector.Length}");
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


