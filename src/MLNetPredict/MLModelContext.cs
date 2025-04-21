using System.Reflection;

namespace MLNetPredict;

/// <summary>
/// Context class for ML model prediction
/// </summary>
public class MLModelContext
{
    /// <summary>
    /// Assembly containing the model
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// Model configuration information
    /// </summary>
    public ConfigInfo Config { get; }

    /// <summary>
    /// Model file path
    /// </summary>
    public string ModelPath { get; }

    /// <summary>
    /// Model class name
    /// </summary>
    public string ClassName => Config.ClassName;

    /// <summary>
    /// Scenario type
    /// </summary>
    public string Scenario => Config.Scenario;

    /// <summary>
    /// Whether input file has header
    /// </summary>
    public bool HasHeader { get; }

    /// <summary>
    /// Input file delimiter
    /// </summary>
    public string Delimiter { get; }

    /// <summary>
    /// Model directory path
    /// </summary>
    public string ModelDirectoryPath { get; }

    /// <summary>
    /// Verbose logging flag
    /// </summary>
    public bool Verbose => Config.Verbose;

    /// <summary>
    /// Constructor
    /// </summary>
    public MLModelContext(
        Assembly assembly,
        ConfigInfo config,
        string modelPath,
        string modelDirectoryPath,
        bool? hasHeader = null,
        string? delimiter = null)
    {
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        Config = config ?? throw new ArgumentNullException(nameof(config));
        ModelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
        ModelDirectoryPath = modelDirectoryPath ?? throw new ArgumentNullException(nameof(modelDirectoryPath));
        HasHeader = hasHeader ?? config.HasHeader;
        Delimiter = delimiter ?? config.Delimiter;
    }

    /// <summary>
    /// Set model path
    /// </summary>
    public void SetModelPath()
    {
        ModelHandler.SetModelPath(Assembly, ModelPath, ClassName);
    }

    /// <summary>
    /// Create model context
    /// </summary>
    public static MLModelContext Create(
        string modelDirectoryPath,
        bool? hasHeader = null,
        string? delimiter = null,
        bool verbose = false)
    {
        // Set the verbose flag for ModelInitializer
        // Initialize model - Note: We need to update ModelInitializer to accept verbose parameter
        var (assembly, config) = ModelInitializer.Initialize(modelDirectoryPath);

        // Set verbose flag in config
        config.Verbose = verbose;

        // Find model file
        var modelPath = Directory.GetFiles(modelDirectoryPath, "*.mlnet").FirstOrDefault()
            ?? throw new FileNotFoundException("Model file (.mlnet) not found.", modelDirectoryPath);

        return new MLModelContext(assembly, config, modelPath, modelDirectoryPath, hasHeader, delimiter);
    }

    /// <summary>
    /// Check if class exists in assembly
    /// </summary>
    public bool HasClass(string className)
    {
        return Assembly.GetTypes().Any(t => t.Name == className);
    }

    /// <summary>
    /// Get list of alternative class names
    /// </summary>
    public List<string> GetAlternativeClassNames()
    {
        var candidates = new List<string> { Config.ClassName };

        // Add folder name-based class name
        var folderName = Path.GetFileName(Path.GetFullPath(ModelDirectoryPath));
        if (!candidates.Contains(folderName))
        {
            candidates.Add(folderName);
        }

        // Add all classes found in assembly (excluding ModelInput and ModelOutput)
        var assemblyTypes = Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsNested && t.Name != "ModelInput" && t.Name != "ModelOutput")
            .Select(t => t.Name)
            .Where(name => !candidates.Contains(name))
            .ToList();

        candidates.AddRange(assemblyTypes);

        // Finally add "Model" name (default)
        if (!candidates.Contains("Model"))
        {
            candidates.Add("Model");
        }

        return candidates;
    }
}