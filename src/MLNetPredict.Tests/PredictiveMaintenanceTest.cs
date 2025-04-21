//namespace MLNetPredict.Tests
//{
//    public class PredictiveMaintenanceTest
//    {
//        private readonly string projPath;
//        private readonly string modelPath;

//        public PredictiveMaintenanceTest()
//        {
//            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
//            this.projPath = currentPath.Substring(0, currentPath.IndexOf("bin"));
//            modelPath = Path.Combine(projPath, "models/PredictiveMaintenance");

//            // # pre requsites
//            // mlnet classification --dataset "files/predictive_maintenance/predictive_maintenance.csv" --label-col "Failure Type" --train-time 120 --output "models" --name "PredictiveMaintenance" --log-file-path "./models/PredictiveMaintenance/logs.txt"
//        }

//        [Fact]
//        public void TestPredictionToFileOutput()
//        {
//            var inputPath = Path.Combine(projPath, "files/predictive_maintenance/input.csv");
//            // Arrange
//            var args = new[]
//            {
//                modelPath,
//                inputPath
//            };

//            var outputPath = Path.GetDirectoryName(inputPath)!;
//            var actualOutputPath = Path.Combine(outputPath, $"{Path.GetFileNameWithoutExtension(inputPath)}-predicted.csv");

//            if (File.Exists(actualOutputPath)) File.Delete(actualOutputPath);

//            // Act
//            var r = Program.Main(args);

//            // Assert
//            Assert.Equal(0, r);
//            Assert.True(File.Exists(actualOutputPath), "Output file was not created.");
//        }
//    }
//}