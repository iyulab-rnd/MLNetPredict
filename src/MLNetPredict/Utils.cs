using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.ML.Data;
using Microsoft.ML.TorchSharp.AutoFormerV2;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace MLNetPredict;

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

    private static readonly HashSet<string> installedPackages = [];

    private static void InstallNugetPackage(string packageId, string packageVersion)
    {
        try
        {
            string packageKey = $"{packageId}.{packageVersion}";
            if (installedPackages.Contains(packageKey))
            {
                ConsoleLogger.Current.LogInformation($"Package {packageId} version {packageVersion} is already installed.");
                return;
            }

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

                    // Mark this package as installed
                    installedPackages.Add(packageKey);

                    // Parse .nuspec file and install dependencies
                    var nuspecReader = new NuspecReader(packageReader.GetNuspec());
                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    foreach (var dependencyGroup in dependencyGroups)
                    {
                        foreach (var dependency in dependencyGroup.Packages)
                        {
                            InstallNugetPackage(dependency.Id, dependency.VersionRange.MinVersion!.ToString());
                        }
                    }

                    // Copy only the necessary files (lib) to the appropriate location
                    foreach (var file in files)
                    {
                        var filePath = Path.Combine(packagesPath, file);
                        var relativePath = file[packagesPath.Length..].TrimStart(Path.DirectorySeparatorChar);
                        var segments = relativePath.Split(Path.DirectorySeparatorChar);

                        if (segments.Length > 2
                            && (segments[1].Equals("lib", StringComparison.OrdinalIgnoreCase) || segments[1].Equals("runtimes", StringComparison.OrdinalIgnoreCase)))
                        {
                            try
                            {
                                // Remove the package id and version from the path
                                var targetRelativePath = string.Join(Path.DirectorySeparatorChar, segments[1..]);
                                var targetPath = Path.Combine(AppContext.BaseDirectory, targetRelativePath);

                                var dir = Path.GetDirectoryName(targetPath)!;
                                if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }

                                File.Copy(filePath, targetPath, true);
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                        }
                    }
                }

                ConsoleLogger.Current.LogInformation("Package installation completed.");
            }
            else
            {
                throw new Exception($"Failed to download {packageId} {packageVersion}.");
            }
        } 
        catch (Exception ex)
        {
            ConsoleLogger.Current.LogError(ex.Message);
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

        InstallNugetPackage(packageId, packageVersion);
    }

    public static void InstallTorchSharpCpu()
    {
        string packageId = "TorchSharp-cpu";
        string packageVersion = "0.101.5";

        var skipCheckFile = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "torch.dll");
        if (File.Exists(skipCheckFile))
        {
            return;
        }

        InstallNugetPackage(packageId, packageVersion);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
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

    internal static void InstallFromCsProj(string? projFile)
    {
        if (string.IsNullOrEmpty(projFile))
        {
            throw new ArgumentNullException(nameof(projFile), "Project file path cannot be null or empty.");
        }

        if (!File.Exists(projFile))
        {
            throw new FileNotFoundException($"Project file not found: {projFile}");
        }

        try
        {
            ConsoleLogger.Current.LogInformation($"Reading project file: {projFile}");
            var projContent = File.ReadAllText(projFile);

            // Load the XML document
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(projContent);

            // Create namespace manager for XPath
            var nsManager = new System.Xml.XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("msbuild", "http://schemas.microsoft.com/developer/msbuild/2003");

            // Find all PackageReference elements
            var packageRefs = doc.SelectNodes("//PackageReference") ??
                             doc.SelectNodes("//msbuild:PackageReference", nsManager);

            if (packageRefs == null || packageRefs.Count == 0)
            {
                ConsoleLogger.Current.LogWarning("No package references found in the project file.");
                return;
            }

            // Process each package reference
            foreach (System.Xml.XmlNode packageRef in packageRefs)
            {
                var includeAttr = packageRef.Attributes?["Include"];
                var versionAttr = packageRef.Attributes?["Version"];

                if (includeAttr != null && versionAttr != null)
                {
                    var packageId = includeAttr.Value;
                    var version = versionAttr.Value;

                    ConsoleLogger.Current.LogInformation($"Installing package: {packageId} version {version}");
                    try
                    {
                        InstallNugetPackage(packageId, version);
                    }
                    catch (Exception ex)
                    {
                        ConsoleLogger.Current.LogError($"Failed to install package {packageId} version {version}: {ex.Message}");
                    }
                }
                else
                {
                    ConsoleLogger.Current.LogWarning($"Skipping invalid package reference: {packageRef.OuterXml}");
                }
            }

            ConsoleLogger.Current.LogInformation("Finished processing project file.");
        }
        catch (System.Xml.XmlException ex)
        {
            throw new InvalidOperationException($"Failed to parse project file: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to process project file: {ex.Message}", ex);
        }
    }
}