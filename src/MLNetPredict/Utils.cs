using System.Reflection;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;
using Tensorflow;

namespace MLNetPredict
{
    public static partial class Utils
    {
        public static string? GetDelimiterFromExtension(string inputPath)
        {
            var extension = Path.GetExtension(inputPath).ToLower();
            return extension switch
            {
                ".csv" => ",",
                ".tsv" => "\t",
                _ => null
            };
        }

        public static ConfigInfo GetConfigInfo(string configPath)
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

        public static string? GetClassName(string sourceCode)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetRoot() as CompilationUnitSyntax;
            var classDeclaration = root?.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            return classDeclaration?.Identifier.Text!;
        }

        public static Assembly CompileAssembly(string[] sourceCodes, string scenario)
        {
            var syntaxTrees = sourceCodes.Select(code => CSharpSyntaxTree.ParseText(code)).ToArray();
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

            string[] mlNetAssemblies;

            if (scenario == "ImageClassification")
            {
                mlNetAssemblies =
                [
                    typeof(Microsoft.ML.MLContext).Assembly.Location,
                    typeof(Microsoft.ML.IDataView).Assembly.Location,
                    typeof(Microsoft.ML.Vision.ImageClassificationTrainer).Assembly.Location,
                ];
            }
            else if (scenario == "Classification")
            {
                mlNetAssemblies =
                [
                    typeof(Microsoft.ML.MLContext).Assembly.Location,
                    typeof(Microsoft.ML.IDataView).Assembly.Location,
                    typeof(Microsoft.ML.Vision.ImageClassificationTrainer).Assembly.Location,
                ];
            }
            else if (scenario == "Forecasting")
            {
                mlNetAssemblies =
                [
                    typeof(Microsoft.ML.MLContext).Assembly.Location,
                    typeof(Microsoft.ML.IDataView).Assembly.Location,
                    typeof(Microsoft.ML.Transforms.TimeSeries.SsaForecastingTransformer).Assembly.Location,
                ];
            }
            else if (scenario == "Regression")
            {
                mlNetAssemblies =
                [
                    typeof(Microsoft.ML.MLContext).Assembly.Location,
                    typeof(Microsoft.ML.IDataView).Assembly.Location,
                    typeof(Microsoft.ML.Trainers.LightGbm.LightGbmBinaryTrainer).Assembly.Location,
                    typeof(Microsoft.ML.Trainers.FastTree.FastTreeBinaryTrainer).Assembly.Location,
                    typeof(Microsoft.ML.Trainers.LightGbm.LightGbmMulticlassTrainer).Assembly.Location,
                ];
            }
            else if (scenario == "Recommendation")
            {
                mlNetAssemblies =
                [
                    typeof(Microsoft.ML.MLContext).Assembly.Location,
                    typeof(Microsoft.ML.IDataView).Assembly.Location,
                    typeof(Microsoft.ML.Trainers.MatrixFactorizationTrainer).Assembly.Location,
                ];
            }
            else if (scenario == "TextClassification")
            {
                mlNetAssemblies =
                [
                    typeof(Microsoft.ML.MLContext).Assembly.Location,
                    typeof(Microsoft.ML.IDataView).Assembly.Location,
                    typeof(Microsoft.ML.TorchSharp.NasBert.TextClassificationTrainer).Assembly.Location,
                ];
            }
            else
                throw new NotSupportedException($"Scenario {scenario} is not supported.");

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


        public static string SanitizeHeader(string header)
        {
            return PropertyNameRegex().Replace(header, "_");
        }

        public static string FormatValue(object? value)
        {
            if (value == null) return string.Empty;
            return value is float floatValue ? floatValue.ToString("F6") : $"{value}";
        }

        public static object? GetDefaultValue(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }

        public static object ConvertValue(string value, Type targetType)
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

        [GeneratedRegex(@"[^a-zA-Z0-9_]")]
        private static partial Regex PropertyNameRegex();
    }
}