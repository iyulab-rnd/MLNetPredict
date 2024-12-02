using System.Reflection;

namespace MLNetPredict
{
    public class ImageClassificationPredictionResult
    {
        public (string ImagePath, string PredictedLabel)[] Items { get; set; }

        public ImageClassificationPredictionResult((string imagePath, string predictedLabel)[] items)
        {
            Items = items;
        }
    }

    public static class ImageClassificationHandler
    {
        private static readonly string[] SupportedImageFormats = ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif"];

        public static ImageClassificationPredictionResult Predict(Assembly assembly, string inputFolderPath, string className)
        {
            if (!Directory.Exists(inputFolderPath))
            {
                if (File.Exists(inputFolderPath))
                {
                    Console.WriteLine($"Error: '{inputFolderPath}' is a file. Please provide a folder path.");
                }
                else
                {
                    Console.WriteLine($"Error: Folder '{inputFolderPath}' does not exist.");
                }
                throw new InvalidOperationException($"Invalid input path: {inputFolderPath}");
            }

            var targetType = assembly.GetTypes().FirstOrDefault(t => t.Name == className)
                ?? throw new InvalidOperationException($"{className} class not found.");

            var modelInputType = targetType.GetNestedType("ModelInput")
                ?? throw new InvalidOperationException($"ModelInput class not found.");

            var predictMethod = targetType.GetMethod("Predict")
                ?? throw new InvalidOperationException($"Predict method not found.");

            var imageFiles = SupportedImageFormats.SelectMany(format => Directory.GetFiles(inputFolderPath, format)).ToArray();

            if (imageFiles.Length == 0)
            {
                Console.WriteLine($"Error: No supported image files found in the folder '{inputFolderPath}'.");
                throw new InvalidOperationException($"No supported image files in folder: {inputFolderPath}");
            }

            var items = new List<(string, string)>();

            foreach (var imagePath in imageFiles)
            {
                var image = File.ReadAllBytes(imagePath);

                var input = Activator.CreateInstance(modelInputType)!;
                var property = modelInputType.GetProperty("ImageSource")
                    ?? throw new InvalidOperationException("Property ImageSource not found.");
                property.SetValue(input, image);

                var result = (dynamic)predictMethod.Invoke(null, [input])!;
                items.Add((imagePath, result.PredictedLabel));
            }

            return new ImageClassificationPredictionResult([.. items]);
        }

        public static void SaveResults(ImageClassificationPredictionResult result, string outputPath)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (Directory.Exists(dir) != true) Directory.CreateDirectory(dir!);

            using var writer = new StreamWriter(outputPath);
            writer.WriteLine("ImagePath,PredictedLabel");
            Console.WriteLine("ImagePath,PredictedLabel");

            foreach (var (imagePath, predictedLabel) in result.Items)
            {   
                var line = $"{Path.GetFileName(imagePath)},{predictedLabel}";
                writer.WriteLine(line);
                Console.WriteLine(line);
            }
        }
    }
}
