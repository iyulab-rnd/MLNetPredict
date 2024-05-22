namespace MLNetPredict.Tests
{
    public class SentimentClassificationTest
    {
        private readonly string projPath;
        private readonly string modelPath;

        public SentimentClassificationTest()
        {
            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
            this.projPath = currentPath.Substring(0, currentPath.IndexOf("bin"));
            modelPath = Path.Combine(projPath, "models/sentiment");
            
            // # pre requsites
            // mlnet classification --dataset ".\files\sentiment\yelp_labelled.txt" --has-header false --label-col 1 --train-time 10 --output "./models" --name "Sentiment" --log-file-path "./models/Sentiment/logs.txt"
        }

        [Fact]
        public void TestPredictionToFileOutput()
        {
            var inputPath = Path.Combine(projPath, "files/sentiment/input.csv");
            // Arrange
            var args = new[] 
            { 
                modelPath, 
                inputPath,
                "--has-header", "true",
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

        [Fact]
        public void TestPredictionToFileHeadlessOutput()
        {
            var inputPath = Path.Combine(projPath, "files/sentiment/input.txt");
            // Arrange
            var args = new[]
            {
                modelPath,
                inputPath,
                "--has-header", "false",
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