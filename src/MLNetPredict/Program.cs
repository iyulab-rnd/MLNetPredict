using CommandLine;

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
                var assembly = Utils.CompileAssembly(new[] { consumptionCode });

                var className = Utils.GetClassName(consumptionCode);
                if (className == null)
                {
                    Console.WriteLine("Class name could not be found in the provided code.");
                    return 1;
                }

                ModelHandler.SetModelPath(assembly, modelPath, className);

                var configInfo = Utils.GetConfigInfo(mbconfigPath);
                var hasHeader = opts.HasHeader ?? configInfo.HasHeader;
                var delimiter = opts.Separator ?? Utils.GetDelimiterFromExtension(inputPath) ?? configInfo.Delimiter;

                var inputFileName = Path.GetFileNameWithoutExtension(inputPath);
                var outputFile = Path.Combine(outputPath, $"{inputFileName}-predicted.csv");

                if (configInfo.Scenario == "Classification")
                {
                    var predictionResult = ClassificationHandler.Predict(assembly, inputPath, className, hasHeader, delimiter);
                    ClassificationHandler.SaveResultsForClassification(predictionResult, outputFile);
                }
                else if (configInfo.Scenario == "Forecasting")
                {
                    var predictionResult = ForecastingHandler.Predict(assembly, inputPath, className, hasHeader, delimiter);
                    ForecastingHandler.SaveResultsForForecasting(predictionResult, outputFile);
                }
                else if (configInfo.Scenario == "Regression")
                {
                    var predictionResult = RegressionHandler.Predict(assembly, inputPath, className, hasHeader, delimiter);
                    RegressionHandler.SaveResultsForRegression(predictionResult, outputFile);
                }
                else if (configInfo.Scenario == "Recommendation")
                {
                    var predictionResult = RecommendationHandler.Predict(assembly, inputPath, className, hasHeader, delimiter);
                    RecommendationHandler.SaveResultsForRecommendation(predictionResult, outputFile);
                }
                else if (configInfo.Scenario == "TextClassification")
                {
                    var predictionResult = TextClassificationHandler.Predict(assembly, inputPath, className, hasHeader, delimiter);
                    TextClassificationHandler.SaveResultsForClassification(predictionResult, outputFile);
                }
                else
                {
                    throw new NotSupportedException($"Scenario {configInfo.Scenario} is not supported.");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}