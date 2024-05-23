using Newtonsoft.Json.Linq;
using System.Reflection;

namespace MLNetPredict
{
    public class ForecastingPredictionResult(object output)
    {
        public object Output { get; set; } = output;
    }

    public static class ForecastingHandler
    {
        public static ForecastingPredictionResult Predict(Assembly assembly, string inputPath, string className, bool hasHeader, string delimiter)
        {
            var targetType = assembly.GetTypes().FirstOrDefault(t => t.Name == className)
                ?? throw new InvalidOperationException($"{className} class not found.");

            var predictMethod = targetType.GetMethod("Predict")
                ?? throw new InvalidOperationException($"{"Predict"} method not found.");

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
                            var modelInputType = targetType.GetNestedType("ModelInput")
                                ?? throw new InvalidOperationException($"{"ModelInput"} class not found.");

                            input = json["input"]!.ToObject(modelInputType);
                        }
                    }
                }
                else
                {
                    // input logic here...
                }
            }

            var output = predictMethod.Invoke(null, [input, horizon]);
            return new ForecastingPredictionResult(output);
        }

        public static void SaveResults(ForecastingPredictionResult result, string outputPath)
        {
            var outputType = result.Output.GetType();
            var propertyNames = outputType.GetProperties().Select(p => p.Name).ToArray();

            using var writer = new StreamWriter(outputPath);
            writer.WriteLine("PredictedValue,LowerBound,UpperBound");

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
            }
        }
    }
}