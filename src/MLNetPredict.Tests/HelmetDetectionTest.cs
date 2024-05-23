using Microsoft.ML;

namespace MLNetPredict.Tests
{
    public class HelmetDetectionTest
    {
        private readonly string projPath;
        private readonly string modelPath;

        public HelmetDetectionTest()
        {
            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
            this.projPath = currentPath.Substring(0, currentPath.IndexOf("bin"));
            modelPath = Path.Combine(projPath, "models/HelmetDetection");

            // # pre requsites
            // mlnet object-detection --dataset "files/helmet_detection/train/_annotations.coco.json" --validation-dataset "files/helmet_detection/valid/_annotations.coco.json" --name "HelmetDetection" --output "models" --log-file-path "./models/HelmetDetection/logs.txt"
        }

        [Fact]
        public void TestPredictionToFileOutput()
        {
            var inputPath = Path.Combine(projPath, "files/helmet_detection/input");
            // Arrange
            var args = new[]
            {
                modelPath,
                inputPath
            };

            var actualOutputPath = Path.Combine(inputPath, $"{Path.GetFileNameWithoutExtension(inputPath)}-predicted.csv");

            if (File.Exists(actualOutputPath)) File.Delete(actualOutputPath);

            // Act
            var r = Program.Main(args);

            // Assert
            Assert.Equal(0, r);
            Assert.True(File.Exists(actualOutputPath), "Output file was not created.");
        }
    }
}