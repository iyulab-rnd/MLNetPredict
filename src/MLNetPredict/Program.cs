using CommandLine;
using Microsoft.ML;
using MLNetPredict.MLHandlers;
using System.Reflection;

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
    }

    public static int Main(string[] args)
    {
        var mlContext = new MLContext();
        Console.WriteLine("[ML.NET Prediction Engine]");

#if DEBUG
        // mlnet-predict "" ""
        args = [
            @"D:/mloop-folder/mloop/scenarios/acc7f3460a414d1c9dccbee17573155b/models/m20250107023322/Model",
            @"D:/mloop-folder/mloop/scenarios/acc7f3460a414d1c9dccbee17573155b/predictions/15e9c6ffc1c54d8d8c9ec657f46deac8/00140_2.jpg"

            //@"D:\data\MLoop\storage\scenarios\76de3a15eb78480ba0cfe4288079fae0\models\m20241219073828\Model", // model-path
            //@"D:\data\MLoop\storage\scenarios\76de3a15eb78480ba0cfe4288079fae0\predictions\2224a7f30551496fb9f125d0b3090760\input.tsv", // input-path
            //"--has-header", "true"
            //"--output-path", @"D:\data\ML-Research\OCR_01\test_output"
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

            var isDirectory = Directory.Exists(inputPath);
            var isFile = File.Exists(inputPath);

            if (!isDirectory && !isFile)
            {
                Console.WriteLine($"Error: Input path '{inputPath}' does not exist or is not accessible.");
                return 1;
            }

            string outputFile;

            // Determine output file path
            if (opts.OutputPath != null)
            {
                if (Path.HasExtension(opts.OutputPath))
                {
                    outputFile = opts.OutputPath;
                    var outputDir = Path.GetDirectoryName(outputFile);
                    if (!string.IsNullOrEmpty(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }
                }
                else
                {
                    Directory.CreateDirectory(opts.OutputPath);
                    outputFile = Path.Combine(opts.OutputPath, $"{Path.GetFileNameWithoutExtension(inputPath)}-predicted.csv");
                }
            }
            else
            {
                if (isDirectory)
                {
                    outputFile = Path.Combine(inputPath, "predicted.csv");
                }
                else
                {
                    var outputDir = Directory.Exists(inputPath) ? inputPath : Path.GetDirectoryName(inputPath)!;
                    outputFile = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}-predicted.csv");
                }
            }

            Console.WriteLine($"Processing input file: {inputPath}");

            // Initialize model and get config
            (Assembly assembly, ConfigInfo configInfo) = ModelInitializer.Initialize(modelDir);

            // Apply user overrides for config if specified
            var hasHeader = opts.HasHeader ?? configInfo.HasHeader;
            var delimiter = opts.Separator ?? Utils.GetDelimiterFromExtension(inputPath) ?? configInfo.Delimiter;

            Console.WriteLine($"Using model: {modelDir}");
            Console.WriteLine($"Output will be saved to: {outputFile}");

            // Execute prediction based on scenario
            switch (configInfo.Scenario)
            {
                case "classification":
                    var classResult = ClassificationHandler.Predict(assembly, inputPath, className: configInfo.ClassName, hasHeader, delimiter);
                    ClassificationHandler.SaveResults(classResult, outputFile);
                    break;

                case "forecasting":
                    var forecastResult = ForecastingHandler.Predict(assembly, inputPath, className: configInfo.ClassName, hasHeader, delimiter);
                    ForecastingHandler.SaveResults(forecastResult, outputFile);
                    break;

                case "regression":
                    var regResult = RegressionHandler.Predict(assembly, inputPath, className: configInfo.ClassName, hasHeader, delimiter);
                    RegressionHandler.SaveResults(regResult, outputFile);
                    break;

                case "recommendation":
                    var recResult = RecommendationHandler.Predict(assembly, inputPath, className: configInfo.ClassName, hasHeader, delimiter);
                    RecommendationHandler.SaveResults(recResult, outputFile);
                    break;

                case "text-classification":
                    var textResult = TextClassificationHandler.Predict(assembly, inputPath, className: configInfo.ClassName, hasHeader, delimiter);
                    TextClassificationHandler.SaveResults(textResult, outputFile);
                    break;

                case "image-classification":
                    var imageResult = ImageClassificationHandler.Predict(assembly, inputPath, className: configInfo.ClassName);
                    ImageClassificationHandler.SaveResults(imageResult, outputFile);
                    break;

                case "object-detection":
                    var objResult = ObjectDetectionHandler.Predict(assembly, inputPath, className: configInfo.ClassName);
                    ObjectDetectionHandler.SaveResults(objResult, outputFile);
                    break;

                default:
                    Console.WriteLine($"Scenario {configInfo.Scenario} is not supported.");
                    return 1;
            }

            Console.WriteLine("Prediction completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"Inner Error: {ex.InnerException.Message}");
            }
            return 1;
        }
    }
}