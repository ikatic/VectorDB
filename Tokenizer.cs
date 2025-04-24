namespace VectorDb;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class Tokenizer
{
    private const int MaxTokens = 8192;
    private const int ApproxCharsPerToken = 4;
    private const int MaxCharsPerChunk = MaxTokens * ApproxCharsPerToken;

    public List<string> ChunkText(string input)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(input))
            return chunks;

        var paragraphs = Regex.Split(input, @"(\r?\n){2,}"); // split by paragraph

        var currentChunk = "";
        foreach (var paragraph in paragraphs)
        {
            if (currentChunk.Length + paragraph.Length < MaxCharsPerChunk)
            {
                currentChunk += paragraph;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(currentChunk))
                    chunks.Add(currentChunk.Trim());

                if (paragraph.Length < MaxCharsPerChunk)
                {
                    currentChunk = paragraph;
                }
                else
                {
                    // paragraph is too big: split further
                    var splitSentences = Regex.Split(paragraph, @"(?<=[\.!\?])\s+");
                    foreach (var sentence in splitSentences)
                    {
                        if (currentChunk.Length + sentence.Length < MaxCharsPerChunk)
                        {
                            currentChunk += sentence + " ";
                        }
                        else
                        {
                            chunks.Add(currentChunk.Trim());
                            currentChunk = sentence + " ";
                        }
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(currentChunk))
            chunks.Add(currentChunk.Trim());

        return chunks;
    }
}