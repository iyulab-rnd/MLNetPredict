using System.Reflection;

namespace MLNetPredict;

/// <summary>
/// Handles model path setting using reflection
/// </summary>
public static class ModelHandler
{
    public static void SetModelPath(Assembly assembly, string modelPath, string className)
    {
        if (string.IsNullOrEmpty(className))
            throw new ArgumentException("Class name cannot be null or empty", nameof(className));

        var targetType = assembly.GetTypes()
            .FirstOrDefault(t => t.Name == className)
            ?? throw new InvalidOperationException($"Class '{className}' not found in assembly.");

        var modelPathField = targetType.GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .FirstOrDefault(f => f.Name == "MLNetModelPath")
            ?? throw new InvalidOperationException($"Field 'MLNetModelPath' not found in class '{className}'.");

        modelPathField.SetValue(null, modelPath);
    }
}