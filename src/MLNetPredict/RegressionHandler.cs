using System.Reflection;

namespace MLNetPredict
{
    public class RegressionPredictionResult((object input, object output)[] items)
    {
        public (object Input, object output)[] Items { get; set; } = items;
    }

    public static class RegressionHandler
    {
        public static RegressionPredictionResult Predict(Assembly assembly, string inputPath, string className, bool hasHeader, string delimiter)
        {
            var targetType = assembly.GetTypes().FirstOrDefault(t => t.Name == className)
                ?? throw new InvalidOperationException($"{className} class not found.");

            var modelInputType = targetType.GetNestedType("ModelInput")
                ?? throw new InvalidOperationException($"{"ModelInput"} class not found.");

            var predictMethod = targetType.GetMethod("Predict")
                ?? throw new InvalidOperationException($"{"Predict"} method not found.");

            var propertyNames = modelInputType.GetProperties().Select(p => p.Name).ToArray();

            // Read input file
            var lines = File.ReadAllLines(inputPath);
            string[] headers;
            IEnumerable<string> dataLines;
            if (hasHeader)
            {
                headers = lines.First().Split(delimiter);
                dataLines = lines.Skip(1);
            }
            else
            {
                headers = propertyNames;
                dataLines = lines;
            }

            var inputs = new List<object>();

            var lowerHeaders = headers.Select(h => Utils.SanitizeHeader(h.ToLower())).ToArray();
            foreach (var line in dataLines)
            {
                var input = Activator.CreateInstance(modelInputType)!;
                var values = line.Split(delimiter);
                for (int i = 0; i < propertyNames.Length; i++)
                {
                    var property = modelInputType.GetProperty(propertyNames[i])
                        ?? throw new InvalidOperationException($"Property {propertyNames[i]} not found.");

                    var valueIndex = Array.IndexOf(lowerHeaders, propertyNames[i].ToLower());
                    var value = valueIndex >= 0 && valueIndex < values.Length ? values[valueIndex]
                        : Utils.GetDefaultValue(property.PropertyType);
                    property.SetValue(input, Utils.ConvertValue($"{value}", property.PropertyType));
                }
                inputs.Add(input);
            }

            var items = inputs.Select(input =>
            {
                var output = (object)predictMethod.Invoke(null, new object[] { input })!;
                return (input, output);
            }).ToArray();

            return new RegressionPredictionResult(items);
        }

        public static void SaveResults(RegressionPredictionResult result, string outputPath)
        {
            using var writer = new StreamWriter(outputPath);
            writer.WriteLine("Score");

            foreach (var (_, output) in result.Items)
            {
                var value = output.GetType().GetProperty("Score")?.GetValue(output);
                var line = $"{Utils.FormatValue(value)}";
                writer.WriteLine(line);
            }
        }
    }
}
