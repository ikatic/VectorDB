# VectorDb

A simple in-memory vector database implementation in C# that supports local embeddings using Ollama's nomic-embed-text model.

## Features

- Store and search vector embeddings
- Integration with Ollama's nomic-embed-text model
- Cosine similarity search
- Memory management with configurable size limits
- Local inference with Ollama (no API keys required)
- File-based persistence for long-term storage
- Remove vectors by document ID
- Dual ID system (auto-generated and document IDs)
- Automatic persistence after modifications
- Robust error handling and data validation

## Prerequisites

- .NET 9.0 SDK
- Ollama installed and running locally
- nomic-embed-text model installed in Ollama

## Setup

1. Clone the repository:
```bash
git clone https://github.com/ikatic/VectorDb.git
cd VectorDb
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Install and start Ollama:
```bash
# Install Ollama (if not already installed)
# Start Ollama service
ollama serve
```

4. Pull the required model:
```bash
ollama pull nomic-embed-text
```

## Usage

The project demonstrates how to:
- Generate embeddings from text using Ollama's nomic-embed-text model
- Store embeddings in the vector database with both auto-generated and document IDs
- Search for similar documents using cosine similarity
- Automatically persist changes to a file
- Remove vectors from the database using document IDs

Example code:
```csharp
// Initialize with persistence file
var db = new VectorDb("vectordb.json");

// Get embedding from text using Ollama
var embedding = await GetEmbeddingAsync("Your text here");

// Store the embedding with a document ID
// The method returns the auto-generated ID
string autoId = await db.AddAsync("document_id", embedding);
Console.WriteLine($"Auto-generated ID: {autoId}");

// Search for similar documents
var results = db.Search(embedding, 5);
foreach (var result in results)
{
    Console.WriteLine($"Auto ID: {result.Id}, Doc ID: {result.DocId}, Score: {result.Score}");
}

// Remove a vector by document ID
bool removed = await db.RemoveAsync("document_id");
if (removed)
{
    Console.WriteLine("Successfully removed vector");
}
```

## Similarity Score Interpretation

The search results include cosine similarity scores that indicate how semantically similar the documents are. Here's how to interpret these scores:

| Cosine Score | Interpretation |
|--------------|----------------|
| 0.95 - 1.00 | Extremely similar (nearly duplicate meaning) |
| 0.90 - 0.95 | Strong semantic similarity |
| 0.85 - 0.90 | Related in meaning |
| 0.75 - 0.85 | Weak to moderate similarity |
| 0.60 - 0.75 | Vaguely related or topically nearby |
| < 0.60 | Likely unrelated |

## Configuration

The vector database has a default memory limit of 4GB. This can be adjusted by modifying the `_maxBytes` field in the `VectorDb` class.

## Technical Details

- Embedding dimension: 768
- Uses Ollama's nomic-embed-text model for local inference
- Memory-efficient storage with configurable limits
- Cosine similarity for semantic search
- JSON-based file persistence
- Automatic loading of persisted data on startup
- Memory tracking for added and removed vectors
- Dual ID system:
  - Auto-generated sequential IDs for internal use
  - Document IDs for external reference
- Automatic persistence:
  - Saves after each Add operation
  - Saves after each Remove operation
  - Loads data on initialization if file exists
- Error handling:
  - Validates embedding dimensions
  - Handles file operation errors gracefully
  - Provides detailed error messages
  - Skips invalid vectors during loading
  - Verifies data integrity

## License

MIT License 