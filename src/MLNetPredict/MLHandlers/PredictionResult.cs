namespace MLNetPredict.MLHandlers;

/// <summary>
/// Base prediction result class
/// </summary>
public abstract class PredictionResult
{
    /// <summary>
    /// Result type (scenario)
    /// </summary>
    public string ResultType { get; private set; }

    /// <summary>
    /// Number of predicted items
    /// </summary>
    public int ItemCount { get; protected set; }

    protected PredictionResult(string resultType)
    {
        ResultType = resultType;
    }
}

/// <summary>
/// Classification prediction result
/// </summary>
public class ClassificationPredictionResult : PredictionResult
{
    public string[] Headers { get; set; }
    public string[] Classes { get; set; }
    public (object Input, IOrderedEnumerable<KeyValuePair<string, float>> Predictions)[] Items { get; set; }

    public ClassificationPredictionResult(string[] headers, string[] classes, (object input, IOrderedEnumerable<KeyValuePair<string, float>> predictions)[] items)
        : base("classification")
    {
        Headers = headers;
        Classes = classes;
        Items = items;
        ItemCount = items.Length;
    }
}

/// <summary>
/// Regression prediction result
/// </summary>
public class RegressionPredictionResult : PredictionResult
{
    public (object Input, object Output)[] Items { get; set; }

    public RegressionPredictionResult((object input, object output)[] items)
        : base("regression")
    {
        Items = items;
        ItemCount = items.Length;
    }
}

/// <summary>
/// Forecasting prediction result
/// </summary>
public class ForecastingPredictionResult : PredictionResult
{
    public object Output { get; set; }

    public ForecastingPredictionResult(object output)
        : base("forecasting")
    {
        Output = output;
        ItemCount = 1;
    }
}

/// <summary>
/// Text classification prediction result
/// </summary>
public class TextClassificationPredictionResult : PredictionResult
{
    public string[] Headers { get; set; }
    public string[] Classes { get; set; }
    public (object Input, IOrderedEnumerable<KeyValuePair<string, float>> Predictions)[] Items { get; set; }

    public TextClassificationPredictionResult(string[] headers, string[] classes, (object input, IOrderedEnumerable<KeyValuePair<string, float>> predictions)[] items)
        : base("text-classification")
    {
        Headers = headers;
        Classes = classes;
        Items = items;
        ItemCount = items.Length;
    }
}

/// <summary>
/// Recommendation system prediction result
/// </summary>
public class RecommendationPredictionResult : PredictionResult
{
    public (object Input, object Output)[] Items { get; set; }

    public RecommendationPredictionResult((object input, object output)[] items)
        : base("recommendation")
    {
        Items = items;
        ItemCount = items.Length;
    }
}

/// <summary>
/// Image classification prediction result
/// </summary>
public class ImageClassificationPredictionResult : PredictionResult
{
    public (string ImagePath, string PredictedLabel, float Score)[] Items { get; set; }

    public ImageClassificationPredictionResult((string imagePath, string predictedLabel, float score)[] items)
        : base("image-classification")
    {
        Items = items;
        ItemCount = items.Length;
    }
}

/// <summary>
/// Object detection prediction result
/// </summary>
public class ObjectDetectionPredictionResult : PredictionResult
{
    public (string ImagePath, string[] PredictedLabels, float[] PredictedBoundingBoxes, float[] Scores)[] Items { get; set; }

    public ObjectDetectionPredictionResult((string imagePath, string[] predictedLabels, float[] predictedBoundingBoxes, float[] scores)[] items)
        : base("object-detection")
    {
        Items = items;
        ItemCount = items.Length;
    }
}