using System.Reflection;

namespace MLNetPredict.MLHandlers;

/// <summary>
/// 회귀 모델 예측을 처리하는 핸들러
/// </summary>
public class RegressionHandler : BaseMLHandler<RegressionPredictionResult>
{
    /// <summary>
    /// 모델을 사용하여 입력 데이터에 대한 회귀 예측 수행
    /// </summary>
    public override RegressionPredictionResult Predict(
        Assembly assembly,
        string inputPath,
        string className,
        bool hasHeader = false,
        string delimiter = ",")
    {
        // 대상 클래스 및 메서드 가져오기
        var (targetType, modelInputType, predictMethod) =
            GetModelComponents(assembly, className, "Predict");

        var propertyNames = modelInputType.GetProperties().Select(p => p.Name).ToArray();

        // 입력 파일 읽기
        var (headers, dataLines) = ReadInputFile(inputPath, hasHeader, delimiter, propertyNames);

        // 모델 입력 객체 생성
        var inputs = CreateModelInputs(modelInputType, headers, dataLines, delimiter);

        // 예측 수행
        var items = inputs.Select(input =>
        {
            var output = predictMethod.Invoke(null, [input])!;
            return (input, output);
        }).ToArray();

        return new RegressionPredictionResult(items);
    }

    /// <summary>
    /// 회귀 예측 결과를 파일에 저장
    /// </summary>
    public override void SaveResults(RegressionPredictionResult result, string outputPath)
    {
        EnsureOutputDirectory(outputPath);

        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("Score");
        Console.WriteLine("Score");

        foreach (var (_, output) in result.Items)
        {
            var value = output.GetType().GetProperty("Score")?.GetValue(output);
            var line = $"{Utils.FormatValue(value)}";
            writer.WriteLine(line);
            Console.WriteLine(line);
        }
    }
}