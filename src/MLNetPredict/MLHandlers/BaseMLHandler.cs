using System.Reflection;

namespace MLNetPredict.MLHandlers;

/// <summary>
/// Base implementation for ML handler classes
/// </summary>
/// <typeparam name="TResult">Prediction result type</typeparam>
public abstract class BaseMLHandler<TResult> : IMLHandler<TResult> where TResult : class
{
    /// <summary>
    /// Perform prediction on input data using model
    /// </summary>
    public abstract TResult Predict(Assembly assembly, string inputPath, string className, bool hasHeader = false, string delimiter = ",");

    /// <summary>
    /// Save prediction results to file
    /// </summary>
    public abstract void SaveResults(TResult result, string outputPath);

    /// <summary>
    /// Get target model class and methods
    /// </summary>
    protected (Type TargetType, Type ModelInputType, MethodInfo PredictMethod) GetModelComponents(
        Assembly assembly,
        string className,
        string methodName)
    {
        var targetType = assembly.GetTypes().FirstOrDefault(t => t.Name == className)
            ?? throw new InvalidOperationException($"Class {className} not found.");

        var modelInputType = targetType.GetNestedType("ModelInput")
            ?? throw new InvalidOperationException($"Class {"ModelInput"} not found.");

        var predictMethod = targetType.GetMethod(methodName)
            ?? throw new InvalidOperationException($"Method {methodName} not found.");

        return (targetType, modelInputType, predictMethod);
    }

    /// <summary>
    /// Read data from input file
    /// </summary>
    protected (string[] Headers, IEnumerable<string> DataLines) ReadInputFile(
        string inputPath,
        bool hasHeader,
        string delimiter,
        string[] propertyNames)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Input file not found: {inputPath}");

        var lines = File.ReadAllLines(inputPath);
        string[] headers;
        IEnumerable<string> dataLines;

        if (hasHeader)
        {
            headers = lines.First().Split(delimiter);
            dataLines = lines.Skip(1);
        }
        else
        {
            headers = propertyNames;
            dataLines = lines;
        }

        return (headers, dataLines);
    }

    /// <summary>
    /// Create model input objects from input data
    /// </summary>
    protected List<object> CreateModelInputs(
        Type modelInputType,
        string[] headers,
        IEnumerable<string> dataLines,
        string delimiter)
    {
        var propertyNames = modelInputType.GetProperties().Select(p => p.Name).ToArray();
        var inputs = new List<object>();

        var lowerHeaders = headers.Select(h => Utils.SanitizeHeader(h.ToLower())).ToArray();

        foreach (var line in dataLines)
        {
            var input = Activator.CreateInstance(modelInputType)!;
            var values = line.Split(delimiter);

            for (int i = 0; i < propertyNames.Length; i++)
            {
                var property = modelInputType.GetProperty(propertyNames[i])
                    ?? throw new InvalidOperationException($"Property {propertyNames[i]} not found.");

                var valueIndex = Array.IndexOf(lowerHeaders, propertyNames[i].ToLower());
                var value = valueIndex >= 0 && valueIndex < values.Length
                    ? values[valueIndex]
                    : Utils.GetDefaultValue(property.PropertyType);

                property.SetValue(input, Utils.ConvertValue($"{value}", property.PropertyType));
            }

            inputs.Add(input);
        }

        return inputs;
    }

    /// <summary>
    /// Create output file directory
    /// </summary>
    protected void EnsureOutputDirectory(string outputPath)
    {
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
    }
}