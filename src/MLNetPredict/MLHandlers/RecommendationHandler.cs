using System.Reflection;

namespace MLNetPredict.MLHandlers;

/// <summary>
/// Handler for recommendation model predictions
/// </summary>
public class RecommendationHandler : BaseMLHandler<RecommendationPredictionResult>
{
    /// <summary>
    /// Perform recommendation predictions on input data using model
    /// </summary>
    public override RecommendationPredictionResult Predict(
        Assembly assembly,
        string inputPath,
        string className,
        bool hasHeader = false,
        string delimiter = ",")
    {
        // Get target class and method
        var (targetType, modelInputType, predictMethod) =
            GetModelComponents(assembly, className, "Predict");

        var propertyNames = modelInputType.GetProperties().Select(p => p.Name).ToArray();

        // Read input file
        var (headers, dataLines) = ReadInputFile(inputPath, hasHeader, delimiter, propertyNames);

        // Create model input objects
        var inputs = CreateModelInputs(modelInputType, headers, dataLines, delimiter);

        // Perform predictions
        var items = inputs.Select(input =>
        {
            var output = predictMethod.Invoke(null, [input])!;
            return (input, output);
        }).ToArray();

        return new RecommendationPredictionResult(items);
    }

    /// <summary>
    /// Save recommendation prediction results to file
    /// </summary>
    public override void SaveResults(RecommendationPredictionResult result, string outputPath)
    {
        EnsureOutputDirectory(outputPath);

        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("Score");
        Console.WriteLine("Score");

        foreach (var (_, output) in result.Items)
        {
            var score = output.GetType().GetProperty("Score")?.GetValue(output);
            var line = $"{Utils.FormatValue(score)}";
            writer.WriteLine(line);
            Console.WriteLine(line);
        }
    }
}