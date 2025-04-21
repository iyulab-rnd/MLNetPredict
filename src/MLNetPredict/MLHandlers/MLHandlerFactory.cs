using System.Reflection;

namespace MLNetPredict.MLHandlers;

/// <summary>
/// ML handler factory class
/// </summary>
public static class MLHandlerFactory
{
    private static readonly Dictionary<string, object> _handlers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Return ML handler for the given scenario
    /// </summary>
    /// <typeparam name="TResult">Prediction result type</typeparam>
    /// <param name="scenario">ML scenario (e.g., classification, regression, etc.)</param>
    /// <returns>ML handler for the scenario</returns>
    public static IMLHandler<TResult> GetHandler<TResult>(string scenario) where TResult : class
    {
        var normalizedScenario = NormalizeScenario(scenario);

        // Return cached handler if available
        if (_handlers.TryGetValue(normalizedScenario, out var cachedHandler) &&
            cachedHandler is IMLHandler<TResult> typedHandler)
        {
            return typedHandler;
        }

        // Create new handler
        IMLHandler<TResult> handler = normalizedScenario switch
        {
            "classification" => CreateHandler<ClassificationHandler, TResult>(),
            "regression" => CreateHandler<RegressionHandler, TResult>(),
            "forecasting" => CreateHandler<ForecastingHandler, TResult>(),
            "recommendation" => CreateHandler<RecommendationHandler, TResult>(),
            "text-classification" => CreateHandler<TextClassificationHandler, TResult>(),
            "image-classification" => CreateHandler<ImageClassificationHandler, TResult>(),
            "object-detection" => CreateHandler<ObjectDetectionHandler, TResult>(),
            _ => throw new ArgumentException($"Unsupported scenario: {scenario}", nameof(scenario))
        };

        // Store in cache
        _handlers[normalizedScenario] = handler;
        return handler;
    }

    /// <summary>
    /// Perform prediction using ML handler for the given scenario
    /// </summary>
    public static PredictionResult ExecutePrediction(
        string scenario,
        Assembly assembly,
        string inputPath,
        string className,
        bool hasHeader = false,
        string delimiter = ",")
    {
        var normalizedScenario = NormalizeScenario(scenario);

        return normalizedScenario switch
        {
            "classification" => GetHandler<ClassificationPredictionResult>(normalizedScenario)
                .Predict(assembly, inputPath, className, hasHeader, delimiter),

            "regression" => GetHandler<RegressionPredictionResult>(normalizedScenario)
                .Predict(assembly, inputPath, className, hasHeader, delimiter),

            "forecasting" => GetHandler<ForecastingPredictionResult>(normalizedScenario)
                .Predict(assembly, inputPath, className, hasHeader, delimiter),

            "recommendation" => GetHandler<RecommendationPredictionResult>(normalizedScenario)
                .Predict(assembly, inputPath, className, hasHeader, delimiter),

            "text-classification" => GetHandler<TextClassificationPredictionResult>(normalizedScenario)
                .Predict(assembly, inputPath, className, hasHeader, delimiter),

            "image-classification" => GetHandler<ImageClassificationPredictionResult>(normalizedScenario)
                .Predict(assembly, inputPath, className),

            "object-detection" => GetHandler<ObjectDetectionPredictionResult>(normalizedScenario)
                .Predict(assembly, inputPath, className),

            _ => throw new ArgumentException($"Unsupported scenario: {scenario}", nameof(scenario))
        };
    }

    /// <summary>
    /// Save prediction results to file
    /// </summary>
    public static void SaveResults(PredictionResult result, string outputPath)
    {
        switch (result)
        {
            case ClassificationPredictionResult classResult:
                GetHandler<ClassificationPredictionResult>("classification")
                    .SaveResults(classResult, outputPath);
                break;

            case RegressionPredictionResult regResult:
                GetHandler<RegressionPredictionResult>("regression")
                    .SaveResults(regResult, outputPath);
                break;

            case ForecastingPredictionResult forecastResult:
                GetHandler<ForecastingPredictionResult>("forecasting")
                    .SaveResults(forecastResult, outputPath);
                break;

            case RecommendationPredictionResult recResult:
                GetHandler<RecommendationPredictionResult>("recommendation")
                    .SaveResults(recResult, outputPath);
                break;

            case TextClassificationPredictionResult textResult:
                GetHandler<TextClassificationPredictionResult>("text-classification")
                    .SaveResults(textResult, outputPath);
                break;

            case ImageClassificationPredictionResult imageResult:
                GetHandler<ImageClassificationPredictionResult>("image-classification")
                    .SaveResults(imageResult, outputPath);
                break;

            case ObjectDetectionPredictionResult objResult:
                GetHandler<ObjectDetectionPredictionResult>("object-detection")
                    .SaveResults(objResult, outputPath);
                break;

            default:
                throw new ArgumentException($"Unsupported result type: {result.GetType().Name}", nameof(result));
        }
    }

    private static IMLHandler<TResult> CreateHandler<THandler, TResult>()
        where THandler : class, new()
        where TResult : class
    {
        var handler = new THandler();

        if (handler is IMLHandler<TResult> typedHandler)
            return typedHandler;

        throw new InvalidOperationException(
            $"{typeof(THandler).Name} does not implement IMLHandler<{typeof(TResult).Name}>.");
    }

    private static string NormalizeScenario(string scenario)
    {
        // Convert to lowercase and remove whitespace
        scenario = scenario.ToLowerInvariant().Trim();

        // Map to standard ML.NET CLI commands
        return scenario switch
        {
            "imageclassification" or "image_classification" => "image-classification",
            "textclassification" or "text_classification" => "text-classification",
            "objectdetection" or "object_detection" => "object-detection",
            _ => scenario
        };
    }
}