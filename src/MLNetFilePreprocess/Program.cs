using CommandLine;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

public static partial class Program
{
    public class Options
    {
        [Value(0, MetaName = "input", Required = true, HelpText = "Input file path.")]
        public required string InputPath { get; set; }

        [Value(1, MetaName = "output", Required = true, HelpText = "Output file path.")]
        public required string OutputPath { get; set; }

        [Option('c', "cols", HelpText = "Columns to include and reorder (comma-separated). Can be column names or indexes.")]
        public string? Columns { get; set; }
    }

    public static int Main(string[] args)
    {
#if DEBUG
        args = new[] { @"D:\data\MLNetPredict\src\MLNetPredict.Tests\files\movie-recommendation\recommendation-ratings-test.csv", "output.csv", "-c", "userId,movieId,rating" };
#endif
        return CommandLine.Parser.Default.ParseArguments<Options>(args)
            .MapResult(
                (Options opts) => Run(opts),
                errs => 1);
    }

    private static int Run(Options opts)
    {
        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = Path.GetExtension(opts.InputPath).Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? "\t" : ",",
                HasHeaderRecord = true,
            };

            // 상대경로인 경우 input 파일의 경로를 기준으로 output 파일의 경로를 설정
            if (!Path.IsPathRooted(opts.OutputPath))
            {
                opts.OutputPath = Path.Combine(Path.GetDirectoryName(opts.InputPath)!, opts.OutputPath);
            }

            using var reader = new StreamReader(opts.InputPath);
            using var csv = new CsvReader(reader, config);
            csv.Read();
            csv.ReadHeader();
            var header = csv.HeaderRecord;

            List<int> columnIndices = GetColumnIndices(header, opts.Columns);

            var selectedHeaders = columnIndices.Select(index => header[index]).ToArray();

            using var writer = new StreamWriter(opts.OutputPath);
            using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Write the selected headers
            foreach (var headerItem in selectedHeaders)
            {
                csvWriter.WriteField(headerItem);
            }
            csvWriter.NextRecord();

            while (csv.Read())
            {
                foreach (var index in columnIndices)
                {
                    csvWriter.WriteField(csv.GetField(index));
                }
                csvWriter.NextRecord();
            }

            Console.WriteLine("File processed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static List<int> GetColumnIndices(string[] headers, string? columnsOption)
    {
        if (string.IsNullOrEmpty(columnsOption))
        {
            return Enumerable.Range(0, headers.Length).ToList();
        }

        var columns = columnsOption.Split(',');
        var indices = new List<int>();

        foreach (var column in columns)
        {
            if (int.TryParse(column, out int index))
            {
                indices.Add(index - 1);
            }
            else
            {
                var headerIndex = Array.IndexOf(headers, column);
                if (headerIndex != -1)
                {
                    indices.Add(headerIndex);
                }
                else
                {
                    throw new ArgumentException($"Column '{column}' not found in headers.");
                }
            }
        }

        return indices;
    }
}
