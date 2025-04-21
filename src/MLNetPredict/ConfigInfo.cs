namespace MLNetPredict;

/// <summary>
/// Model configuration information
/// </summary>
public class ConfigInfo
{
    public bool HasHeader { get; set; }
    public string Delimiter { get; set; }
    public List<string> Columns { get; set; }
    public string LabelColumnName { get; set; }
    public string Scenario { get; set; }
    public string ClassName { get; set; } = "Model";  // Default to "Model"
    public bool Verbose { get; set; }

    public ConfigInfo(bool hasHeader, string delimiter, List<string> columns, string labelColumnName, string scenario, bool verbose = false)
    {
        HasHeader = hasHeader;
        Delimiter = delimiter;
        Columns = columns;
        LabelColumnName = labelColumnName;
        Scenario = scenario;
        Verbose = verbose;

        // Try to derive class name from model file name if available
        string derivedClassName = null;
        var modelDir = AppDomain.CurrentDomain.BaseDirectory;
        var modelFiles = Directory.GetFiles(modelDir, "*.consumption.cs");

        if (modelFiles.Length > 0)
        {
            derivedClassName = Path.GetFileNameWithoutExtension(modelFiles[0]);
            if (derivedClassName.EndsWith(".consumption"))
            {
                derivedClassName = derivedClassName.Substring(0, derivedClassName.Length - 12);
            }
            if (Verbose)
            {
                Console.WriteLine($"[DEBUG] Derived class name from file: {derivedClassName}");
            }
        }

        if (!string.IsNullOrEmpty(derivedClassName))
        {
            ClassName = derivedClassName;
        }
    }
}