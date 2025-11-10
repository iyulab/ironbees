using System.Text.Json;

namespace Ironbees.Core.Embeddings;

/// <summary>
/// BERT tokenizer for converting text to input IDs and attention masks.
/// Implements WordPiece tokenization with [CLS] and [SEP] special tokens.
/// </summary>
public class BertTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<int, string> _idToToken;
    private readonly int _clsTokenId;
    private readonly int _sepTokenId;
    private readonly int _padTokenId;
    private readonly int _maxLength;

    /// <summary>
    /// Creates a new BERT tokenizer from a tokenizer.json file.
    /// </summary>
    /// <param name="tokenizerJsonPath">Path to tokenizer.json file</param>
    /// <param name="maxLength">Maximum sequence length (default: 256)</param>
    public BertTokenizer(string tokenizerJsonPath, int maxLength = 256)
    {
        _maxLength = maxLength;

        // Load tokenizer.json
        var json = File.ReadAllText(tokenizerJsonPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Load vocabulary
        _vocab = new Dictionary<string, int>();
        _idToToken = new Dictionary<int, string>();

        if (root.TryGetProperty("model", out var model) &&
            model.TryGetProperty("vocab", out var vocab))
        {
            foreach (var entry in vocab.EnumerateObject())
            {
                var token = entry.Name;
                var id = entry.Value.GetInt32();
                _vocab[token] = id;
                _idToToken[id] = token;
            }
        }

        // Get special token IDs
        _clsTokenId = _vocab.GetValueOrDefault("[CLS]", 101);
        _sepTokenId = _vocab.GetValueOrDefault("[SEP]", 102);
        _padTokenId = _vocab.GetValueOrDefault("[PAD]", 0);
    }

    /// <summary>
    /// Encodes text into input IDs and attention mask.
    /// </summary>
    /// <param name="text">Input text to encode</param>
    /// <returns>Tuple of (inputIds, attentionMask)</returns>
    public (long[] InputIds, long[] AttentionMask) Encode(string text)
    {
        // Basic tokenization (split by whitespace and punctuation)
        var tokens = BasicTokenize(text);

        // WordPiece tokenization
        var wordPieceTokens = new List<string> { "[CLS]" };
        foreach (var token in tokens)
        {
            wordPieceTokens.AddRange(WordPieceTokenize(token));
        }
        wordPieceTokens.Add("[SEP]");

        // Truncate if necessary
        if (wordPieceTokens.Count > _maxLength)
        {
            wordPieceTokens = wordPieceTokens.Take(_maxLength - 1).Concat(new[] { "[SEP]" }).ToList();
        }

        // Convert tokens to IDs
        var inputIds = new long[_maxLength];
        var attentionMask = new long[_maxLength];

        for (int i = 0; i < wordPieceTokens.Count && i < _maxLength; i++)
        {
            inputIds[i] = _vocab.GetValueOrDefault(wordPieceTokens[i], _vocab["[UNK]"]);
            attentionMask[i] = 1;
        }

        // Pad the rest
        for (int i = wordPieceTokens.Count; i < _maxLength; i++)
        {
            inputIds[i] = _padTokenId;
            attentionMask[i] = 0;
        }

        return (inputIds, attentionMask);
    }

    /// <summary>
    /// Encodes multiple texts in batch.
    /// </summary>
    public (long[][] InputIds, long[][] AttentionMasks) EncodeBatch(IReadOnlyList<string> texts)
    {
        var inputIds = new long[texts.Count][];
        var attentionMasks = new long[texts.Count][];

        for (int i = 0; i < texts.Count; i++)
        {
            var (ids, mask) = Encode(texts[i]);
            inputIds[i] = ids;
            attentionMasks[i] = mask;
        }

        return (inputIds, attentionMasks);
    }

    private List<string> BasicTokenize(string text)
    {
        // Convert to lowercase and split by whitespace
        text = text.ToLowerInvariant();

        var tokens = new List<string>();
        var currentToken = new List<char>();

        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (currentToken.Count > 0)
                {
                    tokens.Add(new string(currentToken.ToArray()));
                    currentToken.Clear();
                }
            }
            else if (char.IsPunctuation(ch))
            {
                if (currentToken.Count > 0)
                {
                    tokens.Add(new string(currentToken.ToArray()));
                    currentToken.Clear();
                }
                tokens.Add(ch.ToString());
            }
            else
            {
                currentToken.Add(ch);
            }
        }

        if (currentToken.Count > 0)
        {
            tokens.Add(new string(currentToken.ToArray()));
        }

        return tokens;
    }

    private List<string> WordPieceTokenize(string word)
    {
        if (_vocab.ContainsKey(word))
        {
            return new List<string> { word };
        }

        var tokens = new List<string>();
        int start = 0;

        while (start < word.Length)
        {
            int end = word.Length;
            string? subToken = null;

            while (start < end)
            {
                var substr = word.Substring(start, end - start);
                if (start > 0)
                {
                    substr = "##" + substr;
                }

                if (_vocab.ContainsKey(substr))
                {
                    subToken = substr;
                    break;
                }

                end--;
            }

            if (subToken == null)
            {
                tokens.Add("[UNK]");
                break;
            }

            tokens.Add(subToken);
            start = end;
        }

        return tokens;
    }
}
