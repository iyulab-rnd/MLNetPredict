using System.Reflection;

namespace MLNetPredict.MLHandlers;

/// <summary>
/// Handler for image classification model predictions
/// </summary>
public class ImageClassificationHandler : BaseMLHandler<ImageClassificationPredictionResult>
{
    private static readonly string[] SupportedFormats = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];

    /// <summary>
    /// Perform classification predictions on input images using model
    /// </summary>
    public override ImageClassificationPredictionResult Predict(
        Assembly assembly,
        string inputPath,
        string className,
        bool hasHeader = false,
        string delimiter = ",")
    {
        // Get target class and method
        var (targetType, modelInputType, predictMethod) =
            GetModelComponents(assembly, className, "PredictAllLabels");

        // Create PredictionEngine instance using 'CreatePredictEngine' method
        var modelType = assembly.GetType("Model.ConsoleApp.Model")
            ?? throw new InvalidOperationException("Model type not found.");

        var createPredictEngineMethod = modelType.GetMethod("CreatePredictEngine",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreatePredictEngine method not found.");

        // Validate model path
        var modelPathField = modelType.GetField("MLNetModelPath", BindingFlags.Static | BindingFlags.NonPublic);
        var modelPath = modelPathField?.GetValue(null) as string;
        Console.WriteLine($"[DEBUG] Model path: {modelPath}");

        if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Model file not found at path: {modelPath}");
        }

        // Get list of input image files
        var imageFiles = GetImageFiles(inputPath);

        if (imageFiles.Count == 0)
            throw new InvalidOperationException("No supported image files found.");

        var items = new List<(string, string, float)>();

        // Perform prediction for each image
        foreach (var imagePath in imageFiles)
        {
            try
            {
                Console.WriteLine($"Processing image: {imagePath}");
                var imageBytes = File.ReadAllBytes(imagePath);

                if (imageBytes == null || imageBytes.Length == 0)
                {
                    Console.WriteLine($"Warning: No or empty image data for {imagePath}.");
                    continue;
                }

                var inputInstance = Activator.CreateInstance(modelInputType)!;
                var imageProp = modelInputType.GetProperty("ImageSource")
                    ?? throw new InvalidOperationException("ImageSource property not found.");

                imageProp.SetValue(inputInstance, imageBytes);

                Console.WriteLine($"Calling PredictAllLabels for image {imagePath}");
                var result = predictMethod.Invoke(null, new[] { inputInstance });

                if (result is IOrderedEnumerable<KeyValuePair<string, float>> predictions)
                {
                    var topPrediction = predictions.FirstOrDefault();
                    items.Add((imagePath, topPrediction.Key, topPrediction.Value));
                }
                else
                {
                    Console.WriteLine($"Warning: Prediction result type for {imagePath} is not as expected.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing image {imagePath}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner exception stack trace: {ex.InnerException.StackTrace}");
                }
                continue;
            }
        }

        if (items.Count == 0)
            throw new InvalidOperationException("No valid predictions performed.");

        return new ImageClassificationPredictionResult([.. items]);
    }

    /// <summary>
    /// Save image classification prediction results to file
    /// </summary>
    public override void SaveResults(ImageClassificationPredictionResult result, string outputPath)
    {
        EnsureOutputDirectory(outputPath);

        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("ImagePath,PredictedLabel,Score");
        Console.WriteLine("ImagePath,PredictedLabel,Score");

        foreach (var (imagePath, predictedLabel, score) in result.Items)
        {
            var line = $"{Path.GetFileName(imagePath)},{predictedLabel},{Utils.FormatValue(score)}";
            writer.WriteLine(line);
            Console.WriteLine(line);
        }
    }

    /// <summary>
    /// Get list of image files from input path
    /// </summary>
    private List<string> GetImageFiles(string inputPath)
    {
        var imageFiles = new List<string>();

        if (File.Exists(inputPath))
        {
            var ext = Path.GetExtension(inputPath).ToLower();
            if (SupportedFormats.Contains(ext))
                imageFiles.Add(inputPath);
        }
        else if (Directory.Exists(inputPath))
        {
            imageFiles.AddRange(SupportedFormats.SelectMany(fmt =>
                Directory.GetFiles(inputPath, "*" + fmt, SearchOption.AllDirectories)));
        }
        else
        {
            throw new InvalidOperationException($"Input path not found: {inputPath}");
        }

        return imageFiles;
    }
}