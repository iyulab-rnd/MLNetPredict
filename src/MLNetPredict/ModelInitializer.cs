using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuGet.Configuration;
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

    public static (Assembly Assembly, ConfigInfo Config) Initialize(string modelDir)
    {
        // 1. Quick validation
        if (!Directory.Exists(modelDir))
            throw new DirectoryNotFoundException($"Model directory '{modelDir}' does not exist.");

        // 2. Check cache
        var modelKey = Path.GetFullPath(modelDir);
        if (ModelCache.TryGetValue(modelKey, out var cached))
        {
            return cached;
        }

        // 3. Load essential files
        var files = new Dictionary<string, string>
        {
            ["model"] = Directory.GetFiles(modelDir, "*.mlnet").FirstOrDefault() ??
                throw new FileNotFoundException("No .mlnet model file found."),
            ["consumption"] = Directory.GetFiles(modelDir, "*.consumption.cs").FirstOrDefault() ??
                throw new FileNotFoundException("No .consumption.cs file found."),
            ["config"] = Directory.GetFiles(modelDir, "*.mbconfig").FirstOrDefault() ??
                throw new FileNotFoundException("No .mbconfig file found.")
        };

        // 4. Load config
        var config = LoadMinimalConfig(files["config"]);

        // 5. Install packages if needed
        var csprojFile = Directory.GetFiles(modelDir, "*.csproj").FirstOrDefault();
        if (csprojFile != null)
        {
            InstallRequiredPackages(csprojFile);
        }

        // 6. Compile
        var code = File.ReadAllText(files["consumption"]);
        var assembly = CompileModelAssembly(code, modelDir);

        // 7. Set model path
        var className = GetClassName(code) ?? throw new InvalidOperationException("Could not determine model class name");
        ModelHandler.SetModelPath(assembly, files["model"], className);

        // 8. Cache and return
        var result = (assembly, config);
        ModelCache[modelKey] = result;
        return result;
    }

    private static ConfigInfo LoadMinimalConfig(string configPath)
    {
        var configContent = File.ReadAllText(configPath);
        var jsonConfig = System.Text.Json.JsonDocument.Parse(configContent);
        var root = jsonConfig.RootElement;

        var hasHeader = root.GetProperty("DataSource").GetProperty("HasHeader").GetBoolean();
        var delimiter = root.GetProperty("DataSource").GetProperty("Delimiter").GetString() ?? ",";
        var scenario = root.GetProperty("Scenario").GetString() ?? "Unknown";
        var labelColumn = root.GetProperty("TrainingOption").GetProperty("LabelColumn").GetString() ?? "Label";

        var columns = new List<string>();
        if (root.GetProperty("DataSource").TryGetProperty("ColumnProperties", out var colProps))
        {
            foreach (var col in colProps.EnumerateArray())
            {
                if (col.TryGetProperty("ColumnName", out var colName))
                {
                    columns.Add(colName.GetString() ?? string.Empty);
                }
            }
        }

        return new ConfigInfo(hasHeader, delimiter, columns, labelColumn, scenario);
    }

    private static string? GetClassName(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var classDeclaration = root
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        return classDeclaration?.Identifier.Text;
    }

    private static readonly HashSet<string> InstalledPackages = [];
    private static readonly string PackagesPath = Path.Combine(Path.GetTempPath(), "nuget-packages");

    private static void InstallRequiredPackages(string projFile)
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

        // 1. 기본 프레임워크 참조 추가
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

        // 2. 모델 디렉토리의 csproj 파일에서 패키지 참조 추가
        var csprojFile = Directory.GetFiles(modelDir, "*.csproj").FirstOrDefault();
        if (csprojFile != null)
        {
            var packageRefs = GetPackageReferences(csprojFile);
            foreach (var (id, version) in packageRefs)
            {
                // 패키지별 어셈블리 찾기
                var packageDir = Path.Combine(PackagesPath, $"{id}.{version}");
                if (Directory.Exists(packageDir))
                {
                    // lib 폴더에서 적절한 프레임워크 버전의 dll 찾기
                    var dllFiles = Directory.GetFiles(packageDir, "*.dll", SearchOption.AllDirectories);
                    foreach (var dll in dllFiles)
                    {
                        // Native dll 제외
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

        // 3. 실행 중인 어셈블리의 위치에서 추가 참조 로드
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

        // 4. 컴파일 옵션 설정 및 실행
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