//namespace MLNetPredict.Tests
//{
//    public class SentimentTextClassificationTest
//    {
//        private readonly string projPath;
//        private readonly string modelPath;

//        public SentimentTextClassificationTest()
//        {
//            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
//            this.projPath = currentPath.Substring(0, currentPath.IndexOf("bin"));
//            modelPath = Path.Combine(projPath, "models/sentiment");

//            // # pre requsites
//            // mlnet text-classification --dataset ".\files\sentiment\yelp_labelled.txt" --has-header false --label-col 1 --text-col 0 --output "./models" --name "Sentiment_Text" --log-file-path "./models/Sentiment_Text/logs.txt"
//        }

//        [Fact]
//        public void TestPredictionToFileOutput()
//        {
//            var inputPath = Path.Combine(projPath, "files/sentiment/input.csv");
//            // Arrange
//            var args = new[]
//            {
//                modelPath,
//                inputPath,
//                "--has-header", "true",
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