using System.Reflection;
using Tensorflow;

namespace MLNetPredict.MLHandlers;

public class ImageClassificationPredictionResult
{
    public (string ImagePath, string PredictedLabel, float Score)[] Items { get; set; }

    public ImageClassificationPredictionResult((string imagePath, string predictedLabel, float score)[] items)
    {
        Items = items;
    }
}


public static class ImageClassificationHandler
{
    public static ImageClassificationPredictionResult Predict(Assembly assembly, string inputPath, string className)
    {
        var targetType = assembly.GetTypes().FirstOrDefault(t => t.Name == className)
            ?? throw new InvalidOperationException($"{className} class not found.");

        var modelInputType = targetType.GetNestedType("ModelInput")
            ?? throw new InvalidOperationException($"ModelInput class not found.");

        var predictAllLabelsMethod = targetType.GetMethod("PredictAllLabels")
            ?? throw new InvalidOperationException($"PredictAllLabels method not found.");

        // 'CreatePredictEngine' 메서드를 통해 PredictionEngine 인스턴스를 매번 새로 생성
        var modelType = assembly.GetType("Model.ConsoleApp.Model")
            ?? throw new InvalidOperationException("Model type not found.");

        var createPredictEngineMethod = modelType.GetMethod("CreatePredictEngine", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreatePredictEngine method not found.");

        // 모델 경로 검증
        var modelPathField = modelType.GetField("MLNetModelPath", BindingFlags.Static | BindingFlags.NonPublic);
        var modelPath = modelPathField?.GetValue(null) as string;
        Console.WriteLine($"[DEBUG] Model path: {modelPath}");
        if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Model file not found at {modelPath}");
        }

        var supportedFormats = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        var imageFiles = new List<string>();
        if (File.Exists(inputPath))
        {
            var ext = Path.GetExtension(inputPath).ToLower();
            if (supportedFormats.Contains(ext))
                imageFiles.Add(inputPath);
        }
        else if (Directory.Exists(inputPath))
        {
            imageFiles.AddRange(supportedFormats.SelectMany(fmt => Directory.GetFiles(inputPath, "*" + fmt, SearchOption.AllDirectories)));
        }
        else
        {
            throw new InvalidOperationException($"Input path not found: {inputPath}");
        }

        if (imageFiles.Count == 0)
            throw new InvalidOperationException("No supported image files found.");

        var items = new List<(string, string, float)>();

        foreach (var imagePath in imageFiles)
        {
            try
            {
                Console.WriteLine($"Processing image: {imagePath}");
                var imageBytes = File.ReadAllBytes(imagePath);
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    Console.WriteLine($"Warning: Image data is null or empty for {imagePath}");
                    continue;
                }

                var inputInstance = Activator.CreateInstance(modelInputType)!;
                var imageProp = modelInputType.GetProperty("ImageSource")
                    ?? throw new InvalidOperationException("Property ImageSource not found.");
                imageProp.SetValue(inputInstance, imageBytes);

                Console.WriteLine($"Invoking PredictAllLabels for image: {imagePath}");
                var result = predictAllLabelsMethod.Invoke(null, new[] { inputInstance });
                if (result is IOrderedEnumerable<KeyValuePair<string, float>> predictions)
                {
                    var topPrediction = predictions.FirstOrDefault();
                    items.Add((imagePath, topPrediction.Key, topPrediction.Value));
                }
                else
                {
                    Console.WriteLine($"Warning: Unexpected prediction result type for {imagePath}");
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
            throw new InvalidOperationException("No valid predictions were made");

        return new ImageClassificationPredictionResult([.. items]);
    }

    public static void SaveResults(ImageClassificationPredictionResult result, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (Directory.Exists(dir) != true) Directory.CreateDirectory(dir!);

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
}