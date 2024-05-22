namespace MLNetPredict.Tests
{
    public class SalesTest
    {
        private readonly string projPath;
        private readonly string modelPath;

        public SalesTest()
        {
            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
            this.projPath = currentPath.Substring(0, currentPath.IndexOf("bin"));
            modelPath = Path.Combine(projPath, "models/Sales");

            // # pre requsites
            // mlnet forecasting --dataset "files/sales/sales.csv" --train-time 120 --horizon 7 --label-col "sales" --time-col "date" --has-header true --output "models" --name "Sales" --log-file-path "./models/Sales/logs.txt"
        }

        [Fact]
        public void TestPredictionToFileOutput()
        {
            var inputPath = Path.Combine(projPath, "files/sales/input.json");
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