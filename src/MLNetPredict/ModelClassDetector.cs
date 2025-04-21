namespace MLNetPredict;

public static class ModelClassDetector
{
    public static string DetectClassName(string modelDir, bool verbose = false)
    {
        // Method 1: Check .consumption.cs files
        var consumptionFiles = Directory.GetFiles(modelDir, "*.consumption.cs");
        if (verbose)
        {
            Console.WriteLine($"[DEBUG] Found {consumptionFiles.Length} consumption files");
        }

        if (consumptionFiles.Length > 0)
        {
            // First, try to derive name from filename
            var fileName = Path.GetFileNameWithoutExtension(consumptionFiles[0]);
            string code;
            if (fileName.EndsWith(".consumption"))
            {
                fileName = fileName.Substring(0, fileName.Length - 12);
                if (verbose)
                {
                    Console.WriteLine($"[DEBUG] Potential class name from file: {fileName}");
                }

                // Verify this class exists in the file
                code = File.ReadAllText(consumptionFiles[0]);
                if (code.Contains($"class {fileName}") || code.Contains($"class {fileName} "))
                {
                    if (verbose)
                    {
                        Console.WriteLine($"[DEBUG] Confirmed class {fileName} exists in the file");
                    }
                    return fileName;
                }
            }

            // If filename-based approach fails, scan file content for class definition
            code = File.ReadAllText(consumptionFiles[0]);

            // Look for "class X" where X is not "ModelInput" or "ModelOutput"
            var classMatches = System.Text.RegularExpressions.Regex.Matches(code, @"class\s+([A-Za-z0-9_]+)");
            foreach (System.Text.RegularExpressions.Match match in classMatches)
            {
                var className = match.Groups[1].Value;
                if (className != "ModelInput" && className != "ModelOutput")
                {
                    if (verbose)
                    {
                        Console.WriteLine($"[DEBUG] Found primary class: {className}");
                    }
                    return className;
                }
            }
        }

        // Method 2: Check .csproj file name
        var csprojFiles = Directory.GetFiles(modelDir, "*.csproj");
        if (csprojFiles.Length > 0)
        {
            var projectName = Path.GetFileNameWithoutExtension(csprojFiles[0]);
            if (verbose)
            {
                Console.WriteLine($"[DEBUG] Using project name as class name: {projectName}");
            }
            return projectName;
        }

        // Method 3: Check .mlnet file name
        var mlnetFiles = Directory.GetFiles(modelDir, "*.mlnet");
        if (mlnetFiles.Length > 0)
        {
            var modelName = Path.GetFileNameWithoutExtension(mlnetFiles[0]);
            if (verbose)
            {
                Console.WriteLine($"[DEBUG] Using model name as class name: {modelName}");
            }
            return modelName;
        }

        // If all else fails, return default
        if (verbose)
        {
            Console.WriteLine("[DEBUG] Using default class name: Model");
        }
        return "Model";
    }
}