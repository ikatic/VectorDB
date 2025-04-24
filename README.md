# VectorDb

A simple vector database implementation in C# that uses Ollama's nomic-embed-text model for embeddings. By default, the database supports up to 5 collections, allowing you to organize and manage vectors in separate groups, each with its own persistence file and 1GB memory limit. Both the number of collections and collection size are configurable in the code.

## Features

- Uses Ollama's nomic-embed-text model for generating embeddings
- Supports up to 5 collections for organizing vectors - configurable in the code.
- Each collection has its own persistence file and 1GB memory limit - configurable in the code.
- Cosine similarity for vector search
- Fast approximate top-k search using cosine-based LSH (Locality Sensitive Hashing)
  - Suitable for datasets up to ~1M vectors
  - Can be swapped out with a more advanced algorithm like HNSW later
- Add vectors with auto-generated or custom IDs
- Remove vectors by document ID
- Search for similar vectors within a collection
- Durable persistence for each collection

## Prerequisites

- .NET 6.0 or later
- Ollama running locally with nomic-embed-text model installed

## Getting Started

1. Make sure you have Ollama running locally with the nomic-embed-text model:
```bash
ollama run nomic-embed-text
```

2. Clone this repository and build the project:
```bash
dotnet build
```

3. Run the example program:
```bash
dotnet run
```

## Usage

### Initialize the Vector Database

```csharp
// Initialize with a base directory for collections
var db = new VectorDb("collections");
```

### Working with Collections

```csharp
// Add vectors to a collection
string id = await db.AddAsync("my_collection", "document text", embedding);

// Search within a collection
var results = db.AnnSearch("my_collection", queryEmbedding, topK: 5);

// Remove a document from a collection
await db.RemoveAsync("my_collection", "document text");

// List all collections
var collections = db.ListCollections();

// Delete a collection
db.DeleteCollection("my_collection");
```

### Getting Embeddings

```csharp
// Get embeddings using Ollama's nomic-embed-text model
float[] embedding = await GetEmbeddingAsync("Your text here");
```

### Persistence

Each collection is automatically persisted to a separate JSON file in the specified collections directory. The files are named after the collections (e.g., "my_collection.json"). The database automatically loads existing collections on startup and saves changes after each operation.

## Example Program

The included example program demonstrates:
- Creating and managing multiple collections
- Adding vectors to different collections
- Searching within specific collections
- Removing documents from collections
- Deleting collections
- Listing available collections

## Notes

- Each collection has a memory limit of 1GB to prevent excessive memory usage
- The database supports a maximum of 5 collections
- Collections are stored in separate files for better organization and management
- The database uses cosine similarity for vector search operations
- Fast approximate top-k search using cosine-based LSH (Locality Sensitive Hashing)
  - Suitable for datasets up to ~1M vectors
  - Can be swapped out with a more advanced algorithm like HNSW later
- Error handling is implemented for file operations and API calls

## Configuration

The vector database has the following built-in limits:
- Maximum of 5 collections
- 1GB memory limit per collection

These limits cannot be adjusted as they are designed to ensure optimal performance and resource usage.

## Technical Details

- Embedding dimension: 768
- Uses nomic-embed-text model and ollama for local inference
- Memory-efficient storage with configurable limits
- Cosine similarity for semantic search
- Fast approximate top-k search using cosine-based LSH (Locality Sensitive Hashing)
  - Suitable for datasets up to ~1M vectors
  - Can be swapped out with a more advanced algorithm like HNSW later
- JSON-based file persistence
- Automatic loading of persisted data on startup
- Memory tracking for added and removed vectors
- Dual ID system:
  - Auto-generated sequential IDs for internal use (unique)
  - Document IDs for external reference (can be repeated)
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
