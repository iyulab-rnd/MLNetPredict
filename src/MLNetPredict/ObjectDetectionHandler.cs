using System.Reflection;
using Microsoft.ML;
using Microsoft.ML.Transforms.Image;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.ML.Data;

namespace MLNetPredict
{
    public class ObjectDetectionPredictionResult
    {
        public (string ImagePath, string[] PredictedLabels, float[] PredictedBoundingBoxes, float[] Scores)[] Items { get; set; }

        public ObjectDetectionPredictionResult((string imagePath, string[] predictedLabels, float[] predictedBoundingBoxes, float[] scores)[] items)
        {
            Items = items;
        }
    }

    public static class ObjectDetectionHandler
    {
        private static readonly string[] SupportedImageFormats = { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif" };

        public static ObjectDetectionPredictionResult Predict(Assembly assembly, string inputFolderPath, string className)
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

            var items = new List<(string, string[], float[], float[])>();

            foreach (var imagePath in imageFiles)
            {
                var image = MLImage.CreateFromFile(imagePath);

                var input = Activator.CreateInstance(modelInputType);
                var imageProperty = modelInputType.GetProperty("Image");
                imageProperty?.SetValue(input, image);

                var result = (dynamic)predictMethod.Invoke(null, new object[] { input });
                items.Add((imagePath, result.PredictedLabel, result.PredictedBoundingBoxes, result.Score));
            }

            return new ObjectDetectionPredictionResult(items.ToArray());
        }

        public static void SaveResults(ObjectDetectionPredictionResult result, string outputPath)
        {
            using var writer = new StreamWriter(outputPath);
            writer.WriteLine("ImagePath,PredictedLabels,BoundingBoxes,Scores");

            foreach (var (imagePath, predictedLabels, boundingBoxes, scores) in result.Items)
            {
                var predictedLabelsStr = predictedLabels == null ? string.Empty : string.Join(";", predictedLabels);
                var boundingBoxesStr = boundingBoxes == null ? string.Empty : string.Join(";", boundingBoxes.Select(b => b.ToString("F6")));
                var scoresStr = scores == null ? string.Empty : string.Join(";", scores.Select(s => s.ToString("F6")));
                writer.WriteLine($"{Path.GetFileName(imagePath)},{predictedLabelsStr},{boundingBoxesStr},{scoresStr}");
            }
        }
    }
}