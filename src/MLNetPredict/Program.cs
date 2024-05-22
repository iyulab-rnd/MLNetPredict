using System.Collections.Generic;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.ML.Data;
using Newtonsoft.Json.Linq;
using static System.Net.Mime.MediaTypeNames;
using static MLNetPredict.Program;

namespace MLNetPredict
{
    public static partial class Program
    {
        public class Options
        {
            [Value(0, MetaName = "model-path", HelpText = "Path to the directory containing the .mlnet model file.", Required = true)]
            public required string ModelPath { get; set; }

            [Value(1, MetaName = "input-path", HelpText = "Path to the input file.", Required = true)]
            public required string InputPath { get; set; }

            [Option('o', "output-path", HelpText = "Path to the output directory (optional).")]
            public string? OutputPath { get; set; }

            [Option("has-header", HelpText = "Specify [true|false] depending if dataset file(s) have header row. Use auto-detect if this flag is not set. (optional).")]
            public bool? HasHeader { get; set; }

            [Option("separator", HelpText = "Specify the separator character used in the dataset file(s). Use auto-detect if this flag is not set. (optional).")]
            public string? Separator { get; set; }
        }

        public class ConfigInfo(bool hasHeader, string delimiter, List<string> columns, string labelColumnName, string scenario)
        {
            public bool HasHeader { get; set; } = hasHeader;
            public string Delimiter { get; set; } = delimiter;
            public List<string> Columns { get; set; } = columns;
            public string LabelColumnName { get; set; } = labelColumnName;
            public string Scenario { get; set; } = scenario;
        }

        public class ClassificationPredictionResult(string[] headers, string[] classes, (object input, IOrderedEnumerable<KeyValuePair<string, float>> predictions)[] items)
        {
            public string[] Headers { get; set; } = headers;
            public string[] Classes { get; set; } = classes;
            public (object Input, IOrderedEnumerable<KeyValuePair<string, float>> Predictions)[] Items { get; set; } = items;
        }

        public class ForecastingPredictionResult(object output)
        {
            public object Output { get; set; } = output;
        }

        /// <summary>
        /// mlnet 으로 학습된 모델을 사용하여 예측을 수행합니다.
        /// </summary>
        public static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<Options>(args)
                .MapResult(
                    (Options opts) => RunPrediction(opts),
                    errs => 1);
        }

        private static int RunPrediction(Options opts)
        {
            try
            {
                var modelDir = opts.ModelPath;
                var inputPath = opts.InputPath;
                var outputPath = opts.OutputPath ?? Path.GetDirectoryName(inputPath)!;

                var modelPath = Directory.GetFiles(modelDir, "*.mlnet").First();
                var consumptionPath = Directory.GetFiles(modelDir, "*.consumption.cs").First();
                var mbconfigPath = Directory.GetFiles(modelDir, "*.mbconfig").First();

                var consumptionCode = File.ReadAllText(consumptionPath);
                var assembly = CompileAssembly(new[] { consumptionCode });

                var className = GetClassName(consumptionCode);
                if (className == null)
                {
                    Console.WriteLine("Class name could not be found in the provided code.");
                    return 1;
                }

                SetModelPath(assembly, modelPath, className);

                var configInfo = GetConfigInfo(mbconfigPath);
                var hasHeader = opts.HasHeader ?? configInfo.HasHeader;
                var delimiter = opts.Separator ?? GetDelimiterFromExtension(inputPath) ?? configInfo.Delimiter;

                var inputFileName = Path.GetFileNameWithoutExtension(inputPath);
                var outputFile = Path.Combine(outputPath, $"{inputFileName}-predicted.csv");

                if (configInfo.Scenario == "Classification")
                {
                    var predictionResult = PredictionClassification(assembly, inputPath, className, hasHeader, delimiter);
                    SaveResultsForClassification(predictionResult, outputFile);
                }
                else if (configInfo.Scenario == "Forecasting")
                {
                    var predictionResult = PredictionForecasting(assembly, inputPath, className, hasHeader, delimiter);
                    SaveResultsForForecasting(predictionResult, outputFile);
                }
                else
                    throw new NotSupportedException($"Scenario {configInfo.Scenario} is not supported.");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static string? GetDelimiterFromExtension(string inputPath)
        {
            var extension = Path.GetExtension(inputPath).ToLower();
            return extension switch
            {
                ".csv" => ",",
                ".tsv" => "\t",
                _ => null
            };
        }

        private static ConfigInfo GetConfigInfo(string configPath)
        {
            var configContent = File.ReadAllText(configPath);
            var jsonConfig = JObject.Parse(configContent);

            var hasHeader = jsonConfig["DataSource"]?["HasHeader"]?.Value<bool>() ?? false;
            var delimiter = jsonConfig["DataSource"]?["Delimiter"]?.Value<string>() ?? ",";
            var columns = jsonConfig["DataSource"]?["ColumnProperties"]
                ?.Select(col => col["ColumnName"]?.Value<string>() ?? "")
                .ToList() ?? [];

            var labelColumnName = jsonConfig["TrainingOption"]?["LabelColumn"]?.Value<string>() ?? "Label";
            var scenario = jsonConfig["Scenario"]?.Value<string>() ?? "Unknown";

            return new ConfigInfo(hasHeader, delimiter, columns, labelColumnName, scenario);
        }

        private static string GetClassName(string sourceCode)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetRoot() as CompilationUnitSyntax;
            var classDeclaration = root?.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            return classDeclaration?.Identifier.Text!;
        }

        private static Assembly CompileAssembly(string[] sourceCodes)
        {
            var syntaxTrees = sourceCodes.Select(code => CSharpSyntaxTree.ParseText(code)).ToArray();
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

            var mlNetAssemblies = new[]
            {
                typeof(Microsoft.ML.MLContext).Assembly.Location,
                typeof(Microsoft.ML.IDataView).Assembly.Location,
                typeof(Microsoft.ML.Transforms.Text.TextFeaturizingEstimator).Assembly.Location,

                // Binary Classification
                typeof(Microsoft.ML.Trainers.LightGbm.LightGbmBinaryTrainer).Assembly.Location,
                typeof(Microsoft.ML.Trainers.FastTree.FastTreeBinaryTrainer).Assembly.Location,

                // Regression
                typeof(Microsoft.ML.Trainers.LightGbm.LightGbmRegressionTrainer).Assembly.Location,
                typeof(Microsoft.ML.Trainers.FastTree.FastTreeRegressionTrainer).Assembly.Location,

                // Multiclass Classification
                typeof(Microsoft.ML.Trainers.LightGbm.LightGbmMulticlassTrainer).Assembly.Location,

                // Clustering
                typeof(Microsoft.ML.Trainers.KMeansTrainer).Assembly.Location,

                // Recommendation
                typeof(Microsoft.ML.Trainers.MatrixFactorizationTrainer).Assembly.Location,

                // Anomaly Detection
                typeof(Microsoft.ML.Transforms.TimeSeries.IidSpikeDetector).Assembly.Location,

                // Time Series
                typeof(Microsoft.ML.Transforms.TimeSeries.SsaForecastingTransformer).Assembly.Location,
            };

            foreach (var assemblyPath in mlNetAssemblies)
            {
                if (string.IsNullOrEmpty(assemblyPath))
                {
                    throw new ArgumentException($"The value cannot be an empty string. (Parameter 'path')");
                }

                references.Add(MetadataReference.CreateFromFile(assemblyPath));
            }

            var compilation = CSharpCompilation.Create(
                "DynamicAssembly",
                syntaxTrees,
                references.Distinct(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
                foreach (var diagnostic in failures)
                {
                    Console.Error.WriteLine(diagnostic.ToString());
                }
                throw new InvalidOperationException("Compilation failed.");
            }

            ms.Seek(0, SeekOrigin.Begin);
            return Assembly.Load(ms.ToArray());
        }

        private static void SetModelPath(Assembly assembly, string modelPath, string className)
        {
            var targetType = assembly.GetTypes().FirstOrDefault(t => t.Name == className)
                ?? throw new InvalidOperationException($"{className} class not found.");

            var modelPathField = targetType.GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .FirstOrDefault(f => f.Name == "MLNetModelPath")
                ?? throw new InvalidOperationException($"{"MLNetModelPath"} field not found.");

            modelPathField.SetValue(null, modelPath);
        }

        private static ClassificationPredictionResult PredictionClassification(Assembly assembly, string inputPath, string className, bool hasHeader, string delimiter)
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

            var lowerHeaders = headers.Select(h => SanitizeHeader(h.ToLower())).ToArray();
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
                        : GetDefaultValue(property.PropertyType);
                    property.SetValue(input, ConvertValue($"{value}", property.PropertyType));
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

        private static ForecastingPredictionResult PredictionForecasting(Assembly assembly, string inputPath, string className, bool hasHeader, string delimiter)
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

            object? input = null;
            object? horizon = null;

            var output = predictMethod.Invoke(null, [input, horizon]);

            return new ForecastingPredictionResult(output);
        }

        private static void SaveResultsForClassification(ClassificationPredictionResult result, string outputPath)
        {
            using var writer = new StreamWriter(outputPath);

            if (result.Classes.Length == 2)
            {
                var headers = new string[] { "PredictedLabel", "Score" };
                writer.WriteLine(string.Join(",", headers));

                foreach (var (_, Predictions) in result.Items)
                {
                    foreach (var prediction in Predictions)
                    {
                        var formattedValue = FormatValue(prediction.Value);
                        var line = $"{prediction.Key},{formattedValue}";
                        writer.WriteLine(line);
                    }
                }
            }
            else
            {
                var headers = new string[] { "Top1", "Top1Score", "Top2", "Top2Score", "Top3", "Top3Score" };
                writer.WriteLine(string.Join(",", headers));

                foreach (var (_, Predictions) in result.Items)
                {
                    var values = new List<string>();
                    for (int i = 0; i < 3; i++)
                    {
                        var prediction = Predictions.ElementAtOrDefault(i);
                        if (prediction.Key != null)
                        {
                            values.Add(prediction.Key);
                            values.Add(FormatValue(prediction.Value));
                        }
                        else
                        {
                            values.Add("");
                            values.Add("");
                        }
                    }

                    var line = string.Join(",", values);
                    writer.WriteLine(line);
                }
            }
        }

        private static void SaveResultsForForecasting(ForecastingPredictionResult result, string outputPath)
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
                var line = $"{FormatValue(values[i])},{FormatValue(lowerBounds[i])},{FormatValue(upperBounds[i])}";
                writer.WriteLine(line);
            }
        }

        private static string FormatValue(object? value)
        {
            if (value == null) return string.Empty;
            return value is float floatValue ? floatValue.ToString("F6") : $"{value}";
        }

        private static object? GetDefaultValue(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }

        private static object ConvertValue(string value, Type targetType)
        {
            if (targetType == typeof(string))
            {
                return value;
            }
            else if (targetType == typeof(float))
            {
                if (float.TryParse(value, out var result))
                {
                    return result;
                }
                return 0f; // Default value for float
            }
            else if (targetType == typeof(int))
            {
                if (int.TryParse(value, out var result))
                {
                    return result;
                }
                return 0; // Default value for int
            }
            throw new NotSupportedException($"Type {targetType.Name} is not supported.");
        }

        public static string SanitizeHeader(string header)
        {
            return PropertyNameRegex().Replace(header, "_");
        }

        [GeneratedRegex(@"[^a-zA-Z0-9_]")]
        private static partial Regex PropertyNameRegex();
    }
}
