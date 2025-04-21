using System.Reflection;

namespace MLNetPredict.MLHandlers;

/// <summary>
/// Common interface for ML model prediction handlers
/// </summary>
/// <typeparam name="TResult">Prediction result type</typeparam>
public interface IMLHandler<TResult> where TResult : class
{
    /// <summary>
    /// Perform prediction on input data using model
    /// </summary>
    /// <param name="assembly">Assembly containing the model</param>
    /// <param name="inputPath">Input data file path</param>
    /// <param name="className">Model class name to use</param>
    /// <param name="hasHeader">Whether input file has header</param>
    /// <param name="delimiter">Input file delimiter</param>
    /// <returns>Prediction result</returns>
    TResult Predict(Assembly assembly, string inputPath, string className, bool hasHeader = false, string delimiter = ",");

    /// <summary>
    /// Save prediction results to file
    /// </summary>
    /// <param name="result">Prediction result to save</param>
    /// <param name="outputPath">Output file path</param>
    void SaveResults(TResult result, string outputPath);
}