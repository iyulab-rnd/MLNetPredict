//namespace MLNetPredict.Tests
//{
//    public class HeartDiseaseDatasetTest
//    {
//        private readonly string modelPath;
//        private readonly string inputPath;

//        public HeartDiseaseDatasetTest()
//        {
//            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
//            var projPath = currentPath.Substring(0, currentPath.IndexOf("bin"));
//            modelPath = Path.Combine(projPath, "models/HeartDiseaseDataset");
//            inputPath = Path.Combine(projPath, "files/HeartDiseaseDataset/input.csv");

//            // # pre requsites
//            // mlnet classification --dataset ".\files\HeartDiseaseDataset\heart_statlog_cleveland_hungary_final.csv" --label-col target --train-time 20 --output "./models" --name "HeartDiseaseDataset" --log-file-path "./models/HeartDiseaseDataset/logs.txt"
//        }

//        [Fact]
//        public void TestPredictionToFileOutput()
//        {
//            // Arrange
//            var args = new[] { modelPath, inputPath };

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