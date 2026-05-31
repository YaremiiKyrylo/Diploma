using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using AIChatAssistant.Application.ServiceInterfaces;
using AIChatAssistant.Shared.Configuration;

namespace AIChatAssistant.Infrastructure.Services;
public class EmbeddingService : IEmbeddingService
{
    private readonly InferenceSession _session;
    private readonly ITokenizerService _tokenizer;

    // The E5 model requires prefixes!
    private const string QueryPrefix = "query: ";
    private const string PassagePrefix = "passage: ";

    public EmbeddingService(IOptions<AiModelSettings> modelPath, ITokenizerService tokenizer)
    {
        var path = modelPath.Value.EmbeddingModelPath;
        _tokenizer = tokenizer;

        // LOAD THE ONNX MODEL
        var options = new SessionOptions
        {
            // Enable all optimizations
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };

        _session = new InferenceSession(path, options);
    }

    public async Task<float[]> GetEmbeddingAsync(string text, bool isQuery = false)
    {

        return await Task.Run(() =>
        {
        // STEP 1: ADDING THE PREFIX
        // The E5 model is trained with prefixes
        string textWithPrefix = (isQuery ? QueryPrefix : PassagePrefix) + text;

        // STEP 2: TOKENIZATION
        var tokenIds = _tokenizer.Tokenize(textWithPrefix);

        // STEP 3: PREPARING THE INPUT DATA
        // ONNX requires 3 inputs:
        // - input_ids: the tokens themselves
        // - attention_mask: which tokens are real (not padding)
        // - token_type_ids: token type (for BERT, usually all 0)

        int seqLen = tokenIds.Length;
        var dimensions = new[] { 1, seqLen }; // [batch_size, sequence_length] 

        // Convert int[] → long[] (ONNX requires long) 
        var inputIds = tokenIds.Select(x => (long)x).ToArray();
        var attentionMask = Enumerable.Repeat(1L, seqLen).ToArray();
        var tokenTypeIds = new long[seqLen]; // All zeros 

        // Create tensors 
        var inputIdsTensor = new DenseTensor<long>(inputIds, dimensions);
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, dimensions);
        var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, dimensions);

        // STEP 4: RUN THE MODEL
        var inputs = new List<NamedOnnxValue>
{
          NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
          NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
          NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
};

        using var results = _session.Run(inputs);

        // STEP 5: GET THE OUTPUT
        // The model returns "last_hidden_state" of size [1, seq_len, 768]
        // These are vectors for EACH token
        var lastHiddenState = results.First().AsTensor<float>();

        // STEP 6: MEAN POOLING
        // Average the vectors of all tokens into a single vector
        var embedding = MeanPooling(lastHiddenState, attentionMask, seqLen);

        // STEP 7: L2 NORMALIZATION
        // Normalize the vector to unit length
        return Normalize(embedding);
        });
    }

    private float[] MeanPooling(Tensor<float> hiddenStates, long[] mask, int seqLen)
    {
        // hiddenStates: [1, seq_len, 384]
        var pooled = new float[384];
        int validTokens = 0;

        // Sum the vectors of all real tokens
        for (int i = 0; i < seqLen; i++)
        {
            if (mask[i] == 1)
            {
                validTokens++;
                for (int j = 0; j < 384; j++)
                {
                    pooled[j] += hiddenStates[0, i, j];
                }
            }
        }

        // Divide by the number of tokens (average)
        for (int j = 0; j < 384; j++)
        {
            pooled[j] /= validTokens;
        }

        return pooled;
    }

    private float[] Normalize(float[] vector)
    {
        // Calculate the vector length: √(x₁² + x₂² + ... + x₃₈₄²)
        float sumSquares = 0f;
        foreach (var val in vector)
            sumSquares += val * val;

        float norm = (float)Math.Sqrt(sumSquares);

        // Divide each element by its length
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }

        return vector;
    }
}