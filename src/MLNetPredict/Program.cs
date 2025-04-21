using CommandLine;
using Microsoft.ML;
using MLNetPredict.MLHandlers;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

namespace MLNetPredict;

public static partial class Program
{
    public class Options
    {
        [Value(0, MetaName = "model-path", HelpText = "Path to the directory containing the .mlnet model file.", Required = true)]
        public required string ModelPath { get; set; }

        [Value(1, MetaName = "input-path", HelpText = "Path to the input file.", Required = true)]
        public required string InputPath { get; set; }

        [Option('o', "output-path", HelpText = "Path to the output file or directory (optional).")]
        public string? OutputPath { get; set; }

        [Option("has-header", HelpText = "Specify [true|false] depending if dataset file(s) have header row. Use auto-detect if this flag is not set. (optional).")]
        public bool? HasHeader { get; set; }

        [Option("separator", HelpText = "Specify the separator character used in the dataset file(s). Use auto-detect if this flag is not set. (optional).")]
        public string? Separator { get; set; }

        [Option('v', "verbose", HelpText = "Enable verbose logging output.")]
        public bool Verbose { get; set; }
    }

    public static int Main(string[] args)
    {
        var mlContext = new MLContext();
        Console.WriteLine("[ML.NET Prediction Engine]");

#if DEBUG
        // For debugging
        args = [
            @"D:\data\research\ml-price\SampleRegression",
            @"D:\data\research\ml-price\input_data.csv",
        ];
#endif

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

            // Input path validation
            var isDirectory = Directory.Exists(inputPath);
            var isFile = File.Exists(inputPath);

            if (!isDirectory && !isFile)
            {
                Console.WriteLine($"Error: Input path '{inputPath}' does not exist or is not accessible.");
                return 1;
            }

            // Determine output file path
            string outputFile = DetermineOutputPath(opts.OutputPath, inputPath, isDirectory);

            Console.WriteLine($"Processing input file: {inputPath}");

            // Debug output model directory files
            if (opts.Verbose)
            {
                PrintModelDirectoryFiles(modelDir);
            }

            try
            {
                // Create model context
                var modelContext = MLModelContext.Create(
                    modelDir,
                    opts.HasHeader,
                    opts.Separator ?? Utils.GetDelimiterFromExtension(inputPath),
                    opts.Verbose);

                // Set model path
                modelContext.SetModelPath();

                Console.WriteLine($"Using model: {modelDir}");
                Console.WriteLine($"Using class name: {modelContext.ClassName}");
                Console.WriteLine($"Output will be saved to: {outputFile}");

                // Check if class exists in assembly
                if (!modelContext.HasClass(modelContext.ClassName))
                {
                    if (opts.Verbose)
                    {
                        Console.WriteLine($"[DEBUG] Class '{modelContext.ClassName}' not found in assembly. Using fallback mechanism.");
                    }

                    // Try multiple class names
                    ExecuteWithFallback(modelContext, inputPath, outputFile, opts.Verbose);
                }
                else
                {
                    if (opts.Verbose)
                    {
                        Console.WriteLine($"[DEBUG] Found class '{modelContext.ClassName}' in assembly.");
                    }

                    // Standard prediction execution
                    ExecutePrediction(modelContext, inputPath, outputFile);
                }

                Console.WriteLine("Prediction completed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                if (opts.Verbose)
                {
                    Console.WriteLine($"[DEBUG] Error during model initialization or prediction: {ex.Message}");
                    Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                }

                // Final fallback attempt
                return ExecuteFinalFallback(modelDir, inputPath, outputFile, opts);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"Inner error: {ex.InnerException.Message}");
                Console.Error.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
            }
            return 1;
        }
    }

    /// <summary>
    /// Execute standard prediction
    /// </summary>
    private static void ExecutePrediction(
        MLModelContext modelContext,
        string inputPath,
        string outputFile)
    {
        // Perform prediction through factory
        var result = MLHandlerFactory.ExecutePrediction(
            modelContext.Scenario,
            modelContext.Assembly,
            inputPath,
            modelContext.ClassName,
            modelContext.HasHeader,
            modelContext.Delimiter);

        // Save results
        MLHandlerFactory.SaveResults(result, outputFile);
    }

    /// <summary>
    /// Execute with fallback trying multiple class names
    /// </summary>
    private static void ExecuteWithFallback(
        MLModelContext modelContext,
        string inputPath,
        string outputFile,
        bool verbose)
    {
        var candidateClasses = modelContext.GetAlternativeClassNames();
        var errorMessages = new List<string>();
        var originalClassName = modelContext.Config.ClassName;

        if (verbose)
        {
            Console.WriteLine($"[DEBUG] Attempting model prediction... Scenario: {modelContext.Scenario}, Original class name: {originalClassName}");
        }

        foreach (var className in candidateClasses)
        {
            try
            {
                modelContext.Config.ClassName = className;
                if (verbose)
                {
                    Console.WriteLine($"[DEBUG] Trying class: {className}");
                }

                // Set model file path
                ModelHandler.SetModelPath(modelContext.Assembly, modelContext.ModelPath, className);

                // Select handler and execute prediction based on scenario
                var result = MLHandlerFactory.ExecutePrediction(
                    modelContext.Scenario,
                    modelContext.Assembly,
                    inputPath,
                    className,
                    modelContext.HasHeader,
                    modelContext.Delimiter);

                // Save results
                MLHandlerFactory.SaveResults(result, outputFile);

                if (verbose)
                {
                    Console.WriteLine($"[DEBUG] Prediction successful with class {className}");
                }
                return;
            }
            catch (Exception ex)
            {
                var errorMessage = $"[DEBUG] Prediction failed with class {className}: {ex.Message}";
                if (verbose)
                {
                    Console.WriteLine(errorMessage);
                }
                errorMessages.Add(errorMessage);
            }
        }

        // If all attempts fail, restore original class name and throw error
        modelContext.Config.ClassName = originalClassName;
        throw new InvalidOperationException(
            $"All class name prediction attempts failed.\nAttempted classes: {string.Join(", ", candidateClasses)}\nErrors:\n{string.Join("\n", errorMessages)}");
    }

    /// <summary>
    /// Execute final fallback attempt
    /// </summary>
    private static int ExecuteFinalFallback(
        string modelDir,
        string inputPath,
        string outputFile,
        Options opts)
    {
        try
        {
            if (opts.Verbose)
            {
                Console.WriteLine("[DEBUG] Final attempt: Trying all combinations...");
            }

            // Try to extract class name from consumption file name
            var consumptionFile = Directory.GetFiles(modelDir, "*.consumption.cs").FirstOrDefault();
            if (consumptionFile != null)
            {
                var className = Path.GetFileNameWithoutExtension(consumptionFile);
                if (className.EndsWith(".consumption"))
                {
                    className = className.Substring(0, className.Length - 12);
                }

                if (opts.Verbose)
                {
                    Console.WriteLine($"[DEBUG] Directly trying consumption file-based class name: {className}");
                }

                // Directly compile and execute source code
                var code = File.ReadAllText(consumptionFile);
                var assembly = CompileModelAssembly(code, modelDir);
                var config = new ConfigInfo(opts.HasHeader ?? false, opts.Separator ?? ",", [], "Label", "regression", opts.Verbose)
                {
                    ClassName = className
                };

                // Set model file
                var modelFile = Directory.GetFiles(modelDir, "*.mlnet").FirstOrDefault();
                if (modelFile != null)
                {
                    ModelHandler.SetModelPath(assembly, modelFile, className);
                }

                // Attempt prediction
                var context = new MLModelContext(
                    assembly,
                    config,
                    modelFile,
                    modelDir,
                    opts.HasHeader,
                    opts.Separator);

                ExecutePrediction(context, inputPath, outputFile);

                if (opts.Verbose)
                {
                    Console.WriteLine("[DEBUG] Final attempt successful!");
                }
                return 0;
            }
        }
        catch (Exception fallbackEx)
        {
            if (opts.Verbose)
            {
                Console.WriteLine($"[DEBUG] Final attempt also failed: {fallbackEx.Message}");
            }
        }

        return 1;
    }

    /// <summary>
    /// Determine output file path
    /// </summary>
    private static string DetermineOutputPath(string? outputPath, string inputPath, bool isDirectory)
    {
        if (outputPath != null)
        {
            if (Path.HasExtension(outputPath))
            {
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
                return outputPath;
            }
            else
            {
                Directory.CreateDirectory(outputPath);
                return Path.Combine(outputPath, $"{Path.GetFileNameWithoutExtension(inputPath)}-predicted.csv");
            }
        }
        else
        {
            if (isDirectory)
            {
                return Path.Combine(inputPath, "predicted.csv");
            }
            else
            {
                var outputDir = Directory.Exists(inputPath) ? inputPath : Path.GetDirectoryName(inputPath)!;
                return Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}-predicted.csv");
            }
        }
    }

    /// <summary>
    /// Print model directory files for debugging
    /// </summary>
    private static void PrintModelDirectoryFiles(string modelDir)
    {
        Console.WriteLine($"[DEBUG] Files in model directory '{modelDir}':");
        if (Directory.Exists(modelDir))
        {
            foreach (var file in Directory.GetFiles(modelDir))
            {
                Console.WriteLine($"[DEBUG]   - {Path.GetFileName(file)}");
            }
        }
        else
        {
            Console.WriteLine($"[DEBUG] Model directory not found: {modelDir}");
        }
    }

    /// <summary>
    /// Compile source code (for final fallback attempt)
    /// </summary>
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
                    Console.WriteLine($"Warning: Cannot load assembly {assemblyName}: {ex.Message}");
                }
            }
        }

        // 2. Load additional references from running assembly location
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
                Console.WriteLine($"Warning: Cannot load local dll {dll}: {ex.Message}");
            }
        }

        // 3. Set compilation options and execute
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
}