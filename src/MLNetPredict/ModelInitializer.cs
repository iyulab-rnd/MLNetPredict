using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MLNetPredict;

public partial class ModelInitializer
{
    private static readonly Dictionary<string, (Assembly Assembly, ConfigInfo Config)> ModelCache = [];

    // Overload for backward compatibility
    public static (Assembly Assembly, ConfigInfo Config) Initialize(string modelDir)
    {
        return Initialize(modelDir, false);
    }

    public static (Assembly Assembly, ConfigInfo Config) Initialize(string modelDir, bool verbose)
    {
        // 1. Quick validation
        if (!Directory.Exists(modelDir))
            throw new DirectoryNotFoundException($"Model directory '{modelDir}' does not exist.");

        if (verbose)
        {
            Console.WriteLine($"[DEBUG] Model directory found: {modelDir}");
        }

        // 2. Check cache
        var modelKey = Path.GetFullPath(modelDir);
        if (ModelCache.TryGetValue(modelKey, out var cached))
        {
            if (verbose)
            {
                Console.WriteLine("[DEBUG] Returning model from cache");
            }
            return cached;
        }

        // 3. Load essential files
        var files = new Dictionary<string, string>();

        if (verbose)
        {
            var modelFiles = Directory.GetFiles(modelDir, "*.mlnet");
            Console.WriteLine($"[DEBUG] Found {modelFiles.Length} .mlnet files:");
            foreach (var file in modelFiles)
            {
                Console.WriteLine($"[DEBUG]   - {Path.GetFileName(file)}");
            }

            var consumptionFiles = Directory.GetFiles(modelDir, "*.consumption.cs");
            Console.WriteLine($"[DEBUG] Found {consumptionFiles.Length} .consumption.cs files:");
            foreach (var file in consumptionFiles)
            {
                Console.WriteLine($"[DEBUG]   - {Path.GetFileName(file)}");
            }

            var configFiles = Directory.GetFiles(modelDir, "*.mbconfig");
            Console.WriteLine($"[DEBUG] Found {configFiles.Length} .mbconfig files:");
            foreach (var file in configFiles)
            {
                Console.WriteLine($"[DEBUG]   - {Path.GetFileName(file)}");
            }
        }

        files["model"] = Directory.GetFiles(modelDir, "*.mlnet").FirstOrDefault() ??
            throw new FileNotFoundException("No .mlnet model file found.");
        files["consumption"] = Directory.GetFiles(modelDir, "*.consumption.cs").FirstOrDefault() ??
            throw new FileNotFoundException("No .consumption.cs file found.");
        files["config"] = Directory.GetFiles(modelDir, "*.mbconfig").FirstOrDefault() ??
            throw new FileNotFoundException("No .mbconfig file found.");

        if (verbose)
        {
            Console.WriteLine($"[DEBUG] Selected model file: {Path.GetFileName(files["model"])}");
            Console.WriteLine($"[DEBUG] Selected consumption file: {Path.GetFileName(files["consumption"])}");
            Console.WriteLine($"[DEBUG] Selected config file: {Path.GetFileName(files["config"])}");
        }

        // 4. Load config
        var config = LoadMinimalConfig(files["config"], verbose);

        // Try to infer class name (based on file name)
        var consumptionFileName = Path.GetFileNameWithoutExtension(files["consumption"]);
        if (consumptionFileName.EndsWith(".consumption"))
        {
            var derivedClassName = consumptionFileName.Substring(0, consumptionFileName.Length - 12);
            if (verbose)
            {
                Console.WriteLine($"[DEBUG] Derived class name from consumption file: {derivedClassName}");
            }
            config.ClassName = derivedClassName;
        }

        if (verbose)
        {
            Console.WriteLine($"[DEBUG] Config loaded. Scenario: {config.Scenario}, Class name: {config.ClassName}");
        }

        // 5. Install packages if needed
        var csprojFile = Directory.GetFiles(modelDir, "*.csproj").FirstOrDefault();
        if (csprojFile != null)
        {
            if (verbose)
            {
                Console.WriteLine($"[DEBUG] Found project file: {Path.GetFileName(csprojFile)}");
            }
            InstallRequiredPackages(csprojFile, verbose);
        }

        // 6. Compile
        var code = File.ReadAllText(files["consumption"]);

        // Add debug output for class discovery
        var detectedClassName = GetClassName(code, verbose);
        if (verbose)
        {
            Console.WriteLine($"[DEBUG] Detected class name from consumption code: {detectedClassName ?? "NULL"}");
        }

        // Check for available classes
        if (verbose)
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var allClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            Console.WriteLine($"[DEBUG] Found {allClasses.Count} classes in the consumption file:");
            foreach (var cls in allClasses)
            {
                Console.WriteLine($"[DEBUG]   - Class: {cls.Identifier.Text}");

                // List methods in each class
                var methods = cls.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
                Console.WriteLine($"[DEBUG]     Methods ({methods.Count}):");
                foreach (var method in methods)
                {
                    Console.WriteLine($"[DEBUG]       - {method.Identifier.Text}");
                }
            }
        }

        var assembly = CompileModelAssembly(code, modelDir);
        if (verbose)
        {
            Console.WriteLine("[DEBUG] Assembly compiled successfully");

            // Debug output for types in the assembly
            var types = assembly.GetTypes();
            Console.WriteLine($"[DEBUG] Found {types.Length} types in compiled assembly:");
            foreach (var type in types)
            {
                Console.WriteLine($"[DEBUG]   - {type.FullName}");
            }
        }

        // 7. Set model path using either detected class name or config class name
        var className = detectedClassName ?? config.ClassName ?? throw new InvalidOperationException("Could not determine model class name");
        if (verbose)
        {
            Console.WriteLine($"[DEBUG] Using class name: {className}");
        }

        try
        {
            ModelHandler.SetModelPath(assembly, files["model"], className);
            if (verbose)
            {
                Console.WriteLine($"[DEBUG] Successfully set model path for class {className}");
            }
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.WriteLine($"[DEBUG] Error setting model path: {ex.Message}");
                // Try to find alternative class in assembly if class not found
                Console.WriteLine("[DEBUG] Trying to find alternative class in assembly...");
            }

            var alternativeClass = FindAlternativeModelClass(assembly, Path.GetFileName(modelDir));
            if (alternativeClass != null)
            {
                if (verbose)
                {
                    Console.WriteLine($"[DEBUG] Found alternative class: {alternativeClass}");
                }
                className = alternativeClass;
                config.ClassName = alternativeClass;

                // Set model path again with new class
                ModelHandler.SetModelPath(assembly, files["model"], className);
                if (verbose)
                {
                    Console.WriteLine($"[DEBUG] Successfully set model path for alternative class {className}");
                }
            }
            else
            {
                throw; // Rethrow original exception if no alternative found
            }
        }

        // 8. Cache and return
        var result = (assembly, config);
        ModelCache[modelKey] = result;
        return result;
    }

    // Find appropriate alternative model class in assembly
    private static string? FindAlternativeModelClass(Assembly assembly, string folderName)
    {
        var types = assembly.GetTypes().Where(t => t.IsClass && !t.IsNested).ToArray();

        // 1. Find class matching folder name
        var folderMatch = types.FirstOrDefault(t => t.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));
        if (folderMatch != null)
        {
            return folderMatch.Name;
        }

        // 2. Find class with Predict or PredictAllLabels method
        var predictionClass = types.FirstOrDefault(t =>
            t.GetMethods().Any(m => m.Name == "Predict" || m.Name == "PredictAllLabels"));

        if (predictionClass != null)
        {
            return predictionClass.Name;
        }

        // 3. Find class that's not ModelInput or ModelOutput
        var nonModelIOClass = types.FirstOrDefault(t =>
            t.Name != "ModelInput" && t.Name != "ModelOutput");

        if (nonModelIOClass != null)
        {
            return nonModelIOClass.Name;
        }

        // All attempts failed
        return null;
    }

    private static ConfigInfo LoadMinimalConfig(string configPath, bool verbose)
    {
        var configContent = File.ReadAllText(configPath);
        var jsonConfig = System.Text.Json.JsonDocument.Parse(configContent);
        var root = jsonConfig.RootElement;

        // Get scenario and normalize it to match ML.NET CLI standard commands
        var rawScenario = root.GetProperty("Scenario").GetString() ?? "Unknown";
        var scenario = NormalizeScenario(rawScenario);

        // Initialize with default values
        var hasHeader = false;
        var delimiter = ",";
        var labelColumn = "Label";
        var columns = new List<string>();

        // Handle different scenarios
        switch (scenario)
        {
            case "image-classification":
            case "object-detection":
                // Image-based scenarios typically don't have these properties
                hasHeader = false;
                delimiter = ",";
                labelColumn = "Label";

                // Try to get any column information if available
                if (root.TryGetProperty("DataSource", out var dataSource))
                {
                    if (dataSource.TryGetProperty("ColumnProperties", out var colProps))
                    {
                        foreach (var col in colProps.EnumerateArray())
                        {
                            if (col.TryGetProperty("ColumnName", out var colName))
                            {
                                columns.Add(colName.GetString() ?? string.Empty);
                            }
                        }
                    }
                }
                break;

            default:
                // For other scenarios, try to get standard properties
                if (root.TryGetProperty("DataSource", out var ds))
                {
                    if (ds.TryGetProperty("HasHeader", out var header))
                    {
                        hasHeader = header.GetBoolean();
                    }

                    if (ds.TryGetProperty("Delimiter", out var delim))
                    {
                        delimiter = delim.GetString() ?? ",";
                    }

                    if (ds.TryGetProperty("ColumnProperties", out var colProps))
                    {
                        foreach (var col in colProps.EnumerateArray())
                        {
                            if (col.TryGetProperty("ColumnName", out var colName))
                            {
                                columns.Add(colName.GetString() ?? string.Empty);
                            }
                        }
                    }
                }

                // Try to get label column from training options
                if (root.TryGetProperty("TrainingOption", out var trainOpt))
                {
                    if (trainOpt.TryGetProperty("LabelColumn", out var label))
                    {
                        labelColumn = label.GetString() ?? "Label";
                    }
                }
                break;
        }

        return new ConfigInfo(hasHeader, delimiter, columns, labelColumn, scenario, verbose);
    }

    private static string NormalizeScenario(string scenario)
    {
        // Convert to lowercase and trim
        scenario = scenario.ToLowerInvariant().Trim();

        // Map to standard ML.NET CLI commands
        return scenario switch
        {
            "imageclassification" or "image_classification" => "image-classification",
            "textclassification" or "text_classification" => "text-classification",
            "objectdetection" or "object_detection" => "object-detection",
            _ => scenario
        };
    }

    private static string? GetClassName(string code, bool verbose)
    {
        try
        {
            if (string.IsNullOrEmpty(code))
            {
                if (verbose) Console.WriteLine("[DEBUG] GetClassName: Input code is empty.");
                return null;
            }

            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            // 1. Find class with Predict or PredictAllLabels method
            if (verbose) Console.WriteLine("[DEBUG] GetClassName: Searching for class with Predict or PredictAllLabels method");
            var classWithPredictMethod = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Any(m => m.Identifier.Text == "Predict" || m.Identifier.Text == "PredictAllLabels"));

            if (classWithPredictMethod != null)
            {
                if (verbose) Console.WriteLine($"[DEBUG] GetClassName: Found class with Predict method: {classWithPredictMethod.Identifier.Text}");
                return classWithPredictMethod.Identifier.Text;
            }

            // 2. Try to extract file name pattern
            var fileNamePattern = @"//\s+([A-Za-z0-9_]+)\.consumption\.cs";
            var fileNameMatch = System.Text.RegularExpressions.Regex.Match(code, fileNamePattern);
            if (fileNameMatch.Success)
            {
                var fileBasedClassName = fileNameMatch.Groups[1].Value;
                if (verbose) Console.WriteLine($"[DEBUG] GetClassName: Extracted class name from file comment: {fileBasedClassName}");

                // Check if a class with this name actually exists
                var matchingClass = root
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text.Equals(fileBasedClassName, StringComparison.OrdinalIgnoreCase));

                if (matchingClass != null)
                {
                    if (verbose) Console.WriteLine($"[DEBUG] GetClassName: Confirmed file-based class: {matchingClass.Identifier.Text}");
                    return matchingClass.Identifier.Text;
                }
            }

            // 3. Try to infer class name from namespace
            var namespacePattern = @"namespace\s+([A-Za-z0-9_.]+)";
            var namespaceMatch = System.Text.RegularExpressions.Regex.Match(code, namespacePattern);
            if (namespaceMatch.Success)
            {
                var namespaceParts = namespaceMatch.Groups[1].Value.Split('.');
                var lastPart = namespaceParts.Last();
                if (verbose) Console.WriteLine($"[DEBUG] GetClassName: Last part of namespace: {lastPart}");

                // Find class matching the last part of namespace
                var namespaceBasedClass = root
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text.Equals(lastPart, StringComparison.OrdinalIgnoreCase));

                if (namespaceBasedClass != null)
                {
                    if (verbose) Console.WriteLine($"[DEBUG] GetClassName: Found namespace-based class: {namespaceBasedClass.Identifier.Text}");
                    return namespaceBasedClass.Identifier.Text;
                }
            }

            // 4. Find top-level class that's not ModelInput or ModelOutput
            var mainClass = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text != "ModelInput" && c.Identifier.Text != "ModelOutput");

            if (mainClass != null)
            {
                if (verbose) Console.WriteLine($"[DEBUG] GetClassName: Found main class: {mainClass.Identifier.Text}");
                return mainClass.Identifier.Text;
            }

            // 5. As a last resort, use first class
            var firstClass = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();

            if (firstClass != null)
            {
                if (verbose) Console.WriteLine($"[DEBUG] GetClassName: Using first class as fallback: {firstClass.Identifier.Text}");
                return firstClass.Identifier.Text;
            }

            if (verbose) Console.WriteLine("[DEBUG] GetClassName: Could not find any class in code");
            return null;
        }
        catch (Exception ex)
        {
            if (verbose) Console.WriteLine($"[DEBUG] GetClassName: Error parsing code: {ex.Message}");
            return null;
        }
    }

    private static readonly HashSet<string> InstalledPackages = [];
    private static readonly string PackagesPath = Path.Combine(Path.GetTempPath(), "nuget-packages");

    private static void InstallRequiredPackages(string projFile, bool verbose)
    {
        if (!File.Exists(projFile))
            return;

        // Parse project file for package references
        var packagesToInstall = GetPackageReferences(projFile);
        if (packagesToInstall.Count == 0)
            return;

        // Setup NuGet
        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var packageResource = repository.GetResourceAsync<FindPackageByIdResource>().Result;
        var cache = new SourceCacheContext();

        foreach (var (packageId, version) in packagesToInstall)
        {
            var packageKey = $"{packageId}.{version}";
            if (InstalledPackages.Contains(packageKey))
                continue;

            try
            {
                var packageIdentity = new PackageIdentity(packageId, NuGetVersion.Parse(version));
                var packagePath = Path.Combine(PackagesPath, $"{packageId}.{version}");

                // Skip if already downloaded
                if (Directory.Exists(packagePath) && Directory.GetFiles(packagePath, "*.dll", SearchOption.AllDirectories).Length > 0)
                {
                    InstalledPackages.Add(packageKey);
                    continue;
                }

                // Download and extract package
                var downloader = packageResource.GetPackageDownloaderAsync(packageIdentity, cache, ConsoleLogger.Current, CancellationToken.None).Result;
                if (downloader != null)
                {
                    Directory.CreateDirectory(packagePath);
                    var nupkgPath = Path.Combine(packagePath, $"{packageId}.{version}.nupkg");

                    downloader.CopyNupkgFileToAsync(nupkgPath, CancellationToken.None).Wait();

                    using var packageReader = new PackageArchiveReader(nupkgPath);
                    var libItems = packageReader.GetLibItems().ToList();

                    // Find best framework match
                    var bestFramework = GetBestFrameworkMatch(libItems);

                    if (bestFramework != null)
                    {
                        foreach (var file in packageReader.GetFiles())
                        {
                            if (file.StartsWith($"lib/{bestFramework}/", StringComparison.OrdinalIgnoreCase) ||
                                file.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase))
                            {
                                var targetPath = Path.Combine(AppContext.BaseDirectory, file);
                                var targetDir = Path.GetDirectoryName(targetPath);

                                if (!Directory.Exists(targetDir))
                                    Directory.CreateDirectory(targetDir!);

                                using var stream = packageReader.GetStream(file);
                                using var targetStream = File.Create(targetPath);
                                stream.CopyTo(targetStream);
                            }
                        }
                    }

                    // Special handling: SciSharp.TensorFlow.Redist
                    if (packageId.Equals("SciSharp.TensorFlow.Redist", StringComparison.OrdinalIgnoreCase))
                    {
                        string nativeOutputDir = Path.Combine(AppContext.BaseDirectory, "tensorflow_native");
                        Directory.CreateDirectory(nativeOutputDir);
                        foreach (var file in packageReader.GetFiles())
                        {
                            if (file.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase) &&
                                (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                                 file.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
                                 file.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase)))
                            {
                                var targetPath = Path.Combine(nativeOutputDir, Path.GetFileName(file));
                                using var stream = packageReader.GetStream(file);
                                using var targetStream = File.Create(targetPath);
                                stream.CopyTo(targetStream);
                            }
                        }
                        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                        Environment.SetEnvironmentVariable("PATH", nativeOutputDir + ";" + currentPath);
                        ConsoleLogger.Current.LogInformation($"Added TensorFlow native directory to PATH: {nativeOutputDir}");
                    }

                    InstalledPackages.Add(packageKey);
                }
            }
            catch (Exception ex)
            {
                ConsoleLogger.Current.LogWarning($"Failed to install package {packageId}: {ex.Message}");
            }
        }
    }

    private static string? GetBestFrameworkMatch(List<FrameworkSpecificGroup> libItems)
    {
        var preferredFrameworks = new[]
        {
            "net9.0",
            "net8.0",
            "net7.0",
            "net6.0",
            "netcoreapp3.1",
            "netstandard2.1",
            "netstandard2.0"
        };

        foreach (var framework in preferredFrameworks)
        {
            var match = libItems
                .FirstOrDefault(x => x.TargetFramework.GetShortFolderName()
                    .Equals(framework, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return framework;
            }
        }

        return libItems
            .Select(x => x.TargetFramework.GetShortFolderName())
            .FirstOrDefault();
    }

    private static Assembly CompileModelAssembly(string code, string modelDir)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var references = new HashSet<MetadataReference>();

        // 1. Add basic framework references
        var trustedAssembliesPaths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
        var neededAssemblies = new[]
        {
            "netstandard",
            "System.Runtime",
            "System.Collections",
            "System.Linq",
            "System.Console",
            "System.Text.RegularExpressions",
            "System.ComponentModel.Primitives",
            "System.Private.CoreLib",
            "System.ObjectModel",
            "System.Text.Json",
            "Microsoft.ML.Core",
            "Microsoft.ML.Data",
            "Microsoft.ML.DataView"
        };

        foreach (var assemblyPath in trustedAssembliesPaths)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (neededAssemblies.Contains(assemblyName))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assemblyPath));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not load assembly {assemblyName}: {ex.Message}");
                }
            }
        }

        // 2. Add package references from csproj file in model directory
        var csprojFile = Directory.GetFiles(modelDir, "*.csproj").FirstOrDefault();
        if (csprojFile != null)
        {
            var packageRefs = GetPackageReferences(csprojFile);
            foreach (var (id, version) in packageRefs)
            {
                // Find assemblies for each package
                var packageDir = Path.Combine(PackagesPath, $"{id}.{version}");
                if (Directory.Exists(packageDir))
                {
                    // Find dll in lib folder for appropriate framework version
                    var dllFiles = Directory.GetFiles(packageDir, "*.dll", SearchOption.AllDirectories);
                    foreach (var dll in dllFiles)
                    {
                        // Exclude native dlls
                        if (!dll.Contains("native", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(dll).EndsWith("Native.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                references.Add(MetadataReference.CreateFromFile(dll));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Warning: Could not load dll {dll}: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        // 3. Load additional references from running assembly location
        var baseDir = AppContext.BaseDirectory;
        var localDlls = Directory.GetFiles(baseDir, "*.dll");
        foreach (var dll in localDlls)
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(dll));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load local dll {dll}: {ex.Message}");
            }
        }

        // 4. Set compilation options and execute
        var compilation = CSharpCompilation.Create(
            "ModelAssembly_" + Guid.NewGuid().ToString("N"),
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithPlatform(Platform.AnyCpu));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"{d.Id}: {d.GetMessage()} at line {d.Location.GetLineSpan().StartLinePosition.Line}");

            throw new InvalidOperationException(
                $"Compilation failed:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }

    private static List<(string Id, string Version)> GetPackageReferences(string projFile)
    {
        var packages = new List<(string Id, string Version)>();
        var doc = new System.Xml.XmlDocument();
        doc.Load(projFile);

        var packageRefs = doc.SelectNodes("//PackageReference");
        if (packageRefs != null)
        {
            foreach (System.Xml.XmlNode package in packageRefs)
            {
                var id = package.Attributes?["Include"]?.Value;
                var version = package.Attributes?["Version"]?.Value;

                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(version))
                {
                    packages.Add((id, version));
                }
            }
        }

        return packages;
    }
}