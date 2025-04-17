# VectorDb

A simple in-memory vector database implementation in C# that supports OpenAI embeddings.

## Features

- Store and search vector embeddings
- Integration with OpenAI's text-embedding-3-large model
- Cosine similarity search
- Memory management with configurable size limits
- Secure API key management using .NET user secrets

## Prerequisites

- .NET 9.0 SDK
- OpenAI API key

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

3. Set up your OpenAI API key:
```bash
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key"
```

## Usage

The project demonstrates how to:
- Generate embeddings from text using OpenAI
- Store embeddings in the vector database
- Search for similar documents using cosine similarity

Example code:
```csharp
var db = new VectorDb();
var openAi = new OpenAIAPI(apiKey);

// Get embedding from text
var embedding = await db.GetEmbeddingAsync(openAi, "Your text here");

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

## Security

API keys are stored securely using .NET user secrets and are never committed to the repository.

## License

MIT License 