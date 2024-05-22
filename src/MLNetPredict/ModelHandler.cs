using System.Reflection;

namespace MLNetPredict
{
    public static class ModelHandler
    {
        public static void SetModelPath(Assembly assembly, string modelPath, string className)
        {
            var targetType = assembly.GetTypes().FirstOrDefault(t => t.Name == className)
                ?? throw new InvalidOperationException($"{className} class not found.");

            var modelPathField = targetType.GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .FirstOrDefault(f => f.Name == "MLNetModelPath")
                ?? throw new InvalidOperationException($"{"MLNetModelPath"} field not found.");

            modelPathField.SetValue(null, modelPath);
        }
    }
}
