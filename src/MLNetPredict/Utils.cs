using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.ML.Data;
using Microsoft.ML.TorchSharp.AutoFormerV2;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
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

        public static Assembly CompileAssembly(string code, string scenario)
        {
            var syntaxTrees = new SyntaxTree[] { CSharpSyntaxTree.ParseText(code) };
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

            string[] mlNetAssemblies;

            if (scenario == "Classification")
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
            else if (scenario == "ImageClassification")
            {
                mlNetAssemblies =
                [
                    typeof(Microsoft.ML.MLContext).Assembly.Location,
                    typeof(Microsoft.ML.IDataView).Assembly.Location,
                    typeof(Microsoft.ML.Vision.ImageClassificationTrainer).Assembly.Location,
                ];
            }
            else if (scenario == "ObjectDetection")
            {
                mlNetAssemblies =
                [
                    typeof(Microsoft.ML.MLContext).Assembly.Location,
                    typeof(Microsoft.ML.IDataView).Assembly.Location,
                    typeof(ObjectDetectionTrainer).Assembly.Location,
                    typeof(MLImage).Assembly.Location,
                    typeof(JsonNode).Assembly.Location,
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


        private static void InstallNugetPackage(string packageId, string packageVersion, string libFolder)
        {
            string packagesPath = Path.Combine(Path.GetTempPath(), "nuget-packages");

            var cache = new SourceCacheContext();
            var settings = Settings.LoadDefaultSettings(root: null); // Load default settings
            var repositories = new List<SourceRepository>
            {
                Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json")
            };

            var repository = repositories.First();
            ConsoleLogger.Current.LogInformation("Retrieving package resource...");
            var resource = repository.GetResourceAsync<FindPackageByIdResource>().Result;

            var packageIdentity = new PackageIdentity(packageId, NuGetVersion.Parse(packageVersion));
            ConsoleLogger.Current.LogInformation($"Downloading package {packageId} version {packageVersion}...");
            var packageDownloader = resource.GetPackageDownloaderAsync(packageIdentity, cache, ConsoleLogger.Current, CancellationToken.None).Result;

            if (packageDownloader != null)
            {
                var packagePath = Path.Combine(packagesPath, $"{packageId}.{packageVersion}.nupkg");
                Directory.CreateDirectory(packagesPath); // Ensure the directory exists
                packageDownloader.CopyNupkgFileToAsync(packagePath, CancellationToken.None).Wait();

                if (!File.Exists(packagePath))
                {
                    throw new Exception($"Failed to download {packageId} {packageVersion}.");
                }

                ConsoleLogger.Current.LogInformation($"Extracting package {packageId} version {packageVersion}...");
                // Extract the package
                using (var packageStream = File.OpenRead(packagePath))
                {
                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Defaultv3,
                        XmlDocFileSaveMode.Skip,
                        ClientPolicyContext.GetClientPolicy(settings, ConsoleLogger.Current), // Pass the loaded settings here
                        ConsoleLogger.Current
                    );

                    var packagePathResolver = new PackagePathResolver(packagesPath);
                    var packageReader = new PackageArchiveReader(packageStream);
                    var files = PackageExtractor.ExtractPackageAsync(
                        packagePath,
                        packageReader,
                        packagePathResolver,
                        packageExtractionContext,
                        CancellationToken.None
                    ).Result;
                }

                ConsoleLogger.Current.LogInformation($"Copying {packageId} native library to output directory...");
                // Copy the TensorFlow native library to the output directory
                var nativeLibFolder = Path.Combine(packagesPath, $"{packageId}.{packageVersion}", libFolder);
                var outputLibFolder = Path.Combine(AppContext.BaseDirectory);
                CopyDirectory(nativeLibFolder, outputLibFolder);

                ConsoleLogger.Current.LogInformation("native library installation completed.");
            }
            else
            {
                throw new Exception($"Failed to download {packageId} {packageVersion}.");
            }
        }

        public static void InstallTensorFlowRedist()
        {
            string packageId = "SciSharp.TensorFlow.Redist";
            string packageVersion = "2.3.1";
            
            var skipCheckFile = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "tensorflow.dll");
            if (File.Exists(skipCheckFile))
            {
                return;
            }

            InstallNugetPackage(packageId, packageVersion, "runtimes");
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                var destDir = Path.Combine(destinationDir, Path.GetFileName(directory));
                CopyDirectory(directory, destDir);
            }
        }

        internal static string CombinePartialClassCodes(params string[] codes)
        {
            var trees = codes.Select(code => CSharpSyntaxTree.ParseText(code)).ToList();
            var rootNodes = trees.Select(tree => tree.GetCompilationUnitRoot()).ToList();

            // Collect and merge using directives
            var usings = new SortedSet<string>();
            foreach (var root in rootNodes)
            {
                foreach (var usingDirective in root.Usings)
                {
                    usings.Add(usingDirective.ToFullString());
                }
            }

            // Collect class declarations
            var classMembers = new List<MemberDeclarationSyntax>();
            foreach (var root in rootNodes)
            {
                foreach (var member in root.Members)
                {
                    if (member is NamespaceDeclarationSyntax namespaceDeclaration)
                    {
                        classMembers.AddRange(namespaceDeclaration.Members);
                    }
                    else
                    {
                        classMembers.Add(member);
                    }
                }
            }

            // Create new compilation unit
            var newRoot = SyntaxFactory.CompilationUnit()
                .WithUsings(SyntaxFactory.List(usings.Select(u => SyntaxFactory.ParseCompilationUnit(u).Usings[0])))
                .WithMembers(SyntaxFactory.List(classMembers))
                .NormalizeWhitespace();

            return newRoot.ToFullString();
        }
    }
}