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
    public string ClassName { get; set; } = "Model";

    public ConfigInfo(bool hasHeader, string delimiter, List<string> columns, string labelColumnName, string scenario)
    {
        HasHeader = hasHeader;
        Delimiter = delimiter;
        Columns = columns;
        LabelColumnName = labelColumnName;
        Scenario = scenario;
    }
}
