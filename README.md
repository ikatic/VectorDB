# VectorDb

A simple in-memory vector database implementation in C# that supports local embeddings using Ollama.

## Features

- Store and search vector embeddings
- Integration with Ollama's nomic-embed-text model
- Cosine similarity search
- Memory management with configurable size limits
- Local inference with Ollama (no API keys required)

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
- Store embeddings in the vector database
- Search for similar documents using cosine similarity

Example code:
```csharp
var db = new VectorDb();

// Get embedding from text using Ollama
var embedding = await GetEmbeddingAsync("Your text here");

// Store the embedding
db.Add("doc1", embedding);

// Search for similar documents
var results = db.Search(embedding, 5);
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

## License

MIT License 