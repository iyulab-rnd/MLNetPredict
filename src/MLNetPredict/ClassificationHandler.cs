using System.Reflection;

namespace MLNetPredict
{
    public class ClassificationPredictionResult(string[] headers, string[] classes, (object input, IOrderedEnumerable<KeyValuePair<string, float>> predictions)[] items)
    {
        public string[] Headers { get; set; } = headers;
        public string[] Classes { get; set; } = classes;
        public (object Input, IOrderedEnumerable<KeyValuePair<string, float>> Predictions)[] Items { get; set; } = items;
    }
    public static class ClassificationHandler
    {
        public static ClassificationPredictionResult Predict(Assembly assembly, string inputPath, string className, bool hasHeader, string delimiter)
        {
            var targetType = assembly.GetTypes().FirstOrDefault(t => t.Name == className)
                ?? throw new InvalidOperationException($"{className} class not found.");

            var modelInputType = targetType.GetNestedType("ModelInput")
                ?? throw new InvalidOperationException($"{"ModelInput"} class not found.");

            var predictAllLabelsMethod = targetType.GetMethod("PredictAllLabels")
                ?? throw new InvalidOperationException($"{"PredictAllLabels"} method not found.");

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
                var result = (IOrderedEnumerable<KeyValuePair<string, float>>)predictAllLabelsMethod.Invoke(null, new object[] { input })!;
                return (input, result);
            }).ToArray();

            var classes = new List<string>();
            foreach (var (input, result) in items)
            {
                foreach (var prediction in result)
                {
                    if (!classes.Contains(prediction.Key))
                    {
                        classes.Add(prediction.Key);
                    }
                }
            }

            return new ClassificationPredictionResult(headers, classes.ToArray(), items);
        }

        public static void SaveResults(ClassificationPredictionResult result, string outputPath)
        {
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
}