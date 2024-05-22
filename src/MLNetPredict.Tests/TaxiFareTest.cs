namespace MLNetPredict.Tests
{
    public class TaxiFareTest
    {
        private readonly string projPath;
        private readonly string modelPath;

        public TaxiFareTest()
        {
            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
            this.projPath = currentPath.Substring(0, currentPath.IndexOf("bin"));
            modelPath = Path.Combine(projPath, "models/TaxiFarePrediction");

            // # pre requsites
            // mlnet regression --dataset "files/taxi-fare/taxi-fare-train.csv" --label-col "fare_amount" --validation-dataset "files/taxi-fare/taxi-fare-test.csv" --has-header true --name "TaxiFarePrediction" --train-time 120 --output "models" --log-file-path "./models/Sales/logs.txt"
        }

        [Fact]
        public void TestPredictionToFileOutput()
        {
            var inputPath = Path.Combine(projPath, "files/taxi-fare/input.csv");
            // Arrange
            var args = new[]
            {
                modelPath,
                inputPath
            };

            var outputPath = Path.GetDirectoryName(inputPath)!;
            var actualOutputPath = Path.Combine(outputPath, $"{Path.GetFileNameWithoutExtension(inputPath)}-predicted.csv");

            if (File.Exists(actualOutputPath)) File.Delete(actualOutputPath);

            // Act
            var r = Program.Main(args);

            // Assert
            Assert.Equal(0, r);
            Assert.True(File.Exists(actualOutputPath), "Output file was not created.");
        }
    }
}