using System.Reflection;
using Newtonsoft.Json.Linq;

namespace MLNetPredict.MLHandlers;

/// <summary>
/// Handler for forecasting model predictions
/// </summary>
public class ForecastingHandler : BaseMLHandler<ForecastingPredictionResult>
{
    /// <summary>
    /// Perform prediction on input data using model
    /// </summary>
    public override ForecastingPredictionResult Predict(
        Assembly assembly,
        string inputPath,
        string className,
        bool hasHeader = false,
        string delimiter = ",")
    {
        // Get target class and method
        var (targetType, modelInputType, predictMethod) =
            GetModelComponents(assembly, className, "Predict");

        object? input = null;
        object? horizon = null;

        if (File.Exists(inputPath))
        {
            var content = File.ReadAllText(inputPath);
            var ext = Path.GetExtension(inputPath).ToLowerInvariant();

            if (ext == ".json")
            {
                var json = JObject.Parse(content);

                if (json.ContainsKey("horizon"))
                {
                    if (json["horizon"]!.Type == JTokenType.Integer)
                    {
                        horizon = json["horizon"]!.Value<int>();
                    }
                    else
                    {
                        horizon = int.Parse(json["horizon"]!.Value<string>()!);
                    }
                }

                if (json.ContainsKey("input"))
                {
                    if (json["input"]!.Type == JTokenType.Object)
                    {
                        input = json["input"]!.ToObject(modelInputType);
                    }
                }
            }
            else
            {
                // input .csv or .tsv or .txt file
                // TODO: Implement processing for CSV, TSV, etc.
            }
        }

        var output = predictMethod.Invoke(null, [input, horizon]);
        return new ForecastingPredictionResult(output!);
    }

    /// <summary>
    /// Save prediction results to file
    /// </summary>
    public override void SaveResults(ForecastingPredictionResult result, string outputPath)
    {
        EnsureOutputDirectory(outputPath);

        var outputType = result.Output.GetType();
        var propertyNames = outputType.GetProperties().Select(p => p.Name).ToArray();

        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("PredictedValue,LowerBound,UpperBound");
        Console.WriteLine("PredictedValue,LowerBound,UpperBound");

        float[]? values = null;
        float[]? lowerBounds = null;
        float[]? upperBounds = null;

        foreach (var property in propertyNames)
        {
            var value = outputType.GetProperty(property)?.GetValue(result.Output)!;
            if (property.EndsWith("_LB"))
            {
                lowerBounds = (float[])value;
            }
            else if (property.EndsWith("_UB"))
            {
                upperBounds = (float[])value;
            }
            else
            {
                values = (float[])value;
            }
        }

        if (values == null || lowerBounds == null || upperBounds == null)
            throw new InvalidOperationException("Output values not found.");

        for (int i = 0; i < values.Length; i++)
        {
            var line = $"{Utils.FormatValue(values[i])},{Utils.FormatValue(lowerBounds[i])},{Utils.FormatValue(upperBounds[i])}";
            writer.WriteLine(line);
            Console.WriteLine(line);
        }
    }
}