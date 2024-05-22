namespace MLNetPredict
{
    public class ConfigInfo(bool hasHeader, string delimiter, List<string> columns, string labelColumnName, string scenario)
    {
        public bool HasHeader { get; set; } = hasHeader;
        public string Delimiter { get; set; } = delimiter;
        public List<string> Columns { get; set; } = columns;
        public string LabelColumnName { get; set; } = labelColumnName;
        public string Scenario { get; set; } = scenario;
    }
}
