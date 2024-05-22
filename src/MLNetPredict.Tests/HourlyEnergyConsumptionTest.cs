namespace MLNetPredict.Tests
{
    public class HourlyEnergyConsumptionTest
    {
        private readonly string projPath;
        private readonly string modelPath;

        public HourlyEnergyConsumptionTest()
        {
            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
            this.projPath = currentPath.Substring(0, currentPath.IndexOf("bin"));
            modelPath = Path.Combine(projPath, "models/HourlyEnergyConsumption");

            // # pre requsites
            // mlnet forecasting --dataset "files/hourly_energy_consumption/AEP_hourly.csv" --train-time 120 --horizon 7 --label-col "AEP_MW" --time-col "Datetime" --has-header true --output "models" --name "HourlyEnergyConsumption" --log-file-path "./models/HourlyEnergyConsumption/logs.txt"
        }

        [Fact]
        public void TestPredictionToFileOutput()
        {
            var inputPath = Path.Combine(projPath, "files/hourly_energy_consumption/input.json");
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