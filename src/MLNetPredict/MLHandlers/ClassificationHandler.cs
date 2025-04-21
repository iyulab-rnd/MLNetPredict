using System.Reflection;

namespace MLNetPredict.MLHandlers;

/// <summary>
/// Handler for classification model predictions
/// </summary>
public class ClassificationHandler : BaseMLHandler<ClassificationPredictionResult>
{
    /// <summary>
    /// Perform classification predictions on input data using model
    /// </summary>
    public override ClassificationPredictionResult Predict(
        Assembly assembly,
        string inputPath,
        string className,
        bool hasHeader = false,
        string delimiter = ",")
    {
        // Get target class and method
        var (targetType, modelInputType, predictMethod) =
            GetModelComponents(assembly, className, "PredictAllLabels");

        var propertyNames = modelInputType.GetProperties().Select(p => p.Name).ToArray();

        // Read input file
        var (headers, dataLines) = ReadInputFile(inputPath, hasHeader, delimiter, propertyNames);

        // Create model input objects
        var inputs = CreateModelInputs(modelInputType, headers, dataLines, delimiter);

        // Perform predictions
        var items = inputs.Select(input =>
        {
            var result = (IOrderedEnumerable<KeyValuePair<string, float>>)predictMethod.Invoke(null, [input])!;
            return (input, result);
        }).ToArray();

        // Extract class list
        var classes = new List<string>();
        foreach (var (_, result) in items)
        {
            foreach (var prediction in result)
            {
                if (!classes.Contains(prediction.Key))
                {
                    classes.Add(prediction.Key);
                }
            }
        }

        return new ClassificationPredictionResult(headers, [.. classes], items);
    }

    /// <summary>
    /// Save classification prediction results to file
    /// </summary>
    public override void SaveResults(ClassificationPredictionResult result, string outputPath)
    {
        EnsureOutputDirectory(outputPath);

        using var writer = new StreamWriter(outputPath);

        if (result.Classes.Length == 2)
        {
            writer.WriteLine("PredictedLabel,Score");
            Console.WriteLine("PredictedLabel,Score");

            foreach (var (_, Predictions) in result.Items)
            {
                var prediction = Predictions.FirstOrDefault();
                var line = $"{prediction.Key},{Utils.FormatValue(prediction.Value)}";
                writer.WriteLine(line);
                Console.WriteLine(line);
            }
        }
        else
        {
            writer.WriteLine("Top1,Top1Score,Top2,Top2Score,Top3,Top3Score");
            Console.WriteLine("Top1,Top1Score,Top2,Top2Score,Top3,Top3Score");

            foreach (var (_, Predictions) in result.Items)
            {
                var values = new List<string>();
                for (int i = 0; i < 3; i++)
                {
                    var prediction = Predictions.ElementAtOrDefault(i);
                    if (prediction.Key != null)
                    {
                        values.Add(prediction.Key);
                        values.Add(Utils.FormatValue(prediction.Value));
                    }
                    else
                    {
                        values.Add("");
                        values.Add("");
                    }
                }

                var line = string.Join(",", values);
                writer.WriteLine(line);
                Console.WriteLine(line);
            }
        }
    }
}