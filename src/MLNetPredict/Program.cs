using CommandLine;
using Microsoft.ML;

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

        public static int Main(string[] args)
        {
            var mlContext = new MLContext();
            Console.WriteLine("[ML.NET Prediction Engine]");

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
                var outputPath = opts.OutputPath ?? (Directory.Exists(inputPath) ? inputPath : Path.GetDirectoryName(inputPath)!);

                if (!Directory.Exists(modelDir))
                {
                    Console.WriteLine($"Model directory '{modelDir}' does not exist.");
                    return 1;
                }

                var modelFiles = Directory.GetFiles(modelDir, "*.mlnet");
                if (modelFiles.Length == 0)
                {
                    Console.WriteLine("No .mlnet model file found. Ensure you have created a model using the latest version of mlnet-cli.");
                    return 1;
                }

                var modelPath = modelFiles.First();

                var consumptionFiles = Directory.GetFiles(modelDir, "*.consumption.cs");
                if (consumptionFiles.Length == 0)
                {
                    Console.WriteLine("No .consumption.cs file found. Ensure you have created a model using the latest version of mlnet-cli.");
                    return 1;
                }

                var consumptionPath = consumptionFiles.First();

                var mbconfigFiles = Directory.GetFiles(modelDir, "*.mbconfig");
                if (mbconfigFiles.Length == 0)
                {
                    Console.WriteLine("No .mbconfig file found. Ensure you have created a model using the latest version of mlnet-cli.");
                    return 1;
                }

                var mbconfigPath = mbconfigFiles.First();
                var configInfo = Utils.GetConfigInfo(mbconfigPath);
                var hasHeader = opts.HasHeader ?? configInfo.HasHeader;
                var delimiter = opts.Separator ?? Utils.GetDelimiterFromExtension(inputPath) ?? configInfo.Delimiter;

                var inputFileName = Path.GetFileNameWithoutExtension(inputPath);
                var outputFile = Path.Combine(outputPath, $"{inputFileName}-predicted.csv");

                Console.WriteLine($"Processing input file: {inputPath}");
                Console.WriteLine($"Using model: {modelPath}");
                Console.WriteLine($"Output will be saved to: {outputFile}");

                if (configInfo.Scenario == "ImageClassification")
                {
                    Utils.InstallTensorFlowRedist();
                }

                var consumptionCode = File.ReadAllText(consumptionPath);
                var assembly = Utils.CompileAssembly([consumptionCode], configInfo.Scenario);

                var className = Utils.GetClassName(consumptionCode);
                if (className == null)
                {
                    Console.WriteLine("Class name could not be found in the provided code.");
                    return 1;
                }

                ModelHandler.SetModelPath(assembly, modelPath, className);

                if (configInfo.Scenario == "ImageClassification")
                {
                    var predictionResult = ImageClassificationHandler.Predict(assembly, inputPath, className);
                    ImageClassificationHandler.SaveResults(predictionResult, outputFile);
                }
                else if (configInfo.Scenario == "Classification")
                {
                    var predictionResult = ClassificationHandler.Predict(assembly, inputPath, className, hasHeader, delimiter);
                    ClassificationHandler.SaveResults(predictionResult, outputFile);
                }
                else if (configInfo.Scenario == "Forecasting")
                {
                    var predictionResult = ForecastingHandler.Predict(assembly, inputPath, className, hasHeader, delimiter);
                    ForecastingHandler.SaveResults(predictionResult, outputFile);
                }
                else if (configInfo.Scenario == "Regression")
                {
                    var predictionResult = RegressionHandler.Predict(assembly, inputPath, className, hasHeader, delimiter);
                    RegressionHandler.SaveResults(predictionResult, outputFile);
                }
                else if (configInfo.Scenario == "Recommendation")
                {
                    var predictionResult = RecommendationHandler.Predict(assembly, inputPath, className, hasHeader, delimiter);
                    RecommendationHandler.SaveResults(predictionResult, outputFile);
                }
                else if (configInfo.Scenario == "TextClassification")
                {
                    var predictionResult = TextClassificationHandler.Predict(assembly, inputPath, className, hasHeader, delimiter);
                    TextClassificationHandler.SaveResults(predictionResult, outputFile);
                }
                else
                {
                    Console.WriteLine($"Scenario {configInfo.Scenario} is not supported.");
                    return 1;
                }

                Console.WriteLine("Prediction completed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }
    }
}