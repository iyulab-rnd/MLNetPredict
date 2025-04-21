using System.Reflection;
using Microsoft.ML.Data;

namespace MLNetPredict.MLHandlers;

/// <summary>
/// Handler for object detection model predictions
/// </summary>
public class ObjectDetectionHandler : BaseMLHandler<ObjectDetectionPredictionResult>
{
    private static readonly string[] SupportedImageFormats = ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif"];

    /// <summary>
    /// Perform object detection predictions on input images using model
    /// </summary>
    public override ObjectDetectionPredictionResult Predict(
        Assembly assembly,
        string inputPath,
        string className,
        bool hasHeader = false,
        string delimiter = ",")
    {
        // Validate input path
        if (!Directory.Exists(inputPath))
        {
            if (File.Exists(inputPath))
            {
                Console.WriteLine($"Error: '{inputPath}' is a file. Please provide a folder path.");
            }
            else
            {
                Console.WriteLine($"Error: Folder '{inputPath}' does not exist.");
            }
            throw new InvalidOperationException($"Invalid input path: {inputPath}");
        }

        // Get target class and method
        var (targetType, modelInputType, predictMethod) =
            GetModelComponents(assembly, className, "Predict");

        // Get list of image files
        var imageFiles = SupportedImageFormats
            .SelectMany(format => Directory.GetFiles(inputPath, format))
            .ToArray();

        if (imageFiles.Length == 0)
        {
            Console.WriteLine($"Error: No supported image files found in folder '{inputPath}'.");
            throw new InvalidOperationException($"No supported image files in folder: {inputPath}");
        }

        var items = new List<(string, string[], float[], float[])>();

        // Perform prediction for each image
        foreach (var imagePath in imageFiles)
        {
            var image = MLImage.CreateFromFile(imagePath);

            var input = Activator.CreateInstance(modelInputType);
            var imageProperty = modelInputType.GetProperty("Image");
            imageProperty?.SetValue(input, image);

            var result = (dynamic)predictMethod.Invoke(null, [input]);
            items.Add((imagePath, result.PredictedLabel, result.PredictedBoundingBoxes, result.Score));
        }

        return new ObjectDetectionPredictionResult([.. items]);
    }

    /// <summary>
    /// Save object detection prediction results to file
    /// </summary>
    public override void SaveResults(ObjectDetectionPredictionResult result, string outputPath)
    {
        EnsureOutputDirectory(outputPath);

        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("ImagePath,PredictedLabels,BoundingBoxes,Scores");
        Console.WriteLine("ImagePath,PredictedLabels,BoundingBoxes,Scores");

        foreach (var (imagePath, predictedLabels, boundingBoxes, scores) in result.Items)
        {
            var predictedLabelsStr = predictedLabels == null ? string.Empty : string.Join(";", predictedLabels);
            var boundingBoxesStr = boundingBoxes == null ? string.Empty : string.Join(";", boundingBoxes.Select(b => b.ToString("F6")));
            var scoresStr = scores == null ? string.Empty : string.Join(";", scores.Select(s => s.ToString("F6")));

            var line = $"{Path.GetFileName(imagePath)},{predictedLabelsStr},{boundingBoxesStr},{scoresStr}";
            writer.WriteLine(line);
            Console.WriteLine(line);
        }
    }
}