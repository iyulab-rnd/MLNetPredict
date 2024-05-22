namespace MLNetPredict.Tests
{
    public class MovieRecommendationTest
    {
        private readonly string projPath;
        private readonly string modelPath;

        public MovieRecommendationTest()
        {
            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
            this.projPath = currentPath.Substring(0, currentPath.IndexOf("bin"));
            modelPath = Path.Combine(projPath, "models/MovieRecommendation");

            // # pre requsites
            // mlnet recommendation --dataset "files/movie-recommendation/data.csv" --user-col userId --item-col movieId --rating-col rating --has-header true --name MovieRecommendation --train-time 120 --output "models" --log-file-path "./models/MovieRecommendation/logs.txt"
        }

        [Fact]
        public void TestPredictionToFileOutput()
        {
            var inputPath = Path.Combine(projPath, "files/movie-recommendation/input.csv");
            // Arrange
            var args = new[]
            {
                modelPath,
                inputPath,
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