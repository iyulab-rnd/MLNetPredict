using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.ML.Data;
using Microsoft.ML.TorchSharp.AutoFormerV2;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace MLNetPredict;

public static partial class Utils
{
    public static string? GetDelimiterFromExtension(string inputPath)
    {
        return Path.GetExtension(inputPath).ToLower() switch
        {
            ".csv" => ",",
            ".tsv" => "\t",
            _ => null
        };
    }

    public static string FormatValue(object? value)
    {
        if (value == null) return string.Empty;
        return value is float floatValue ? floatValue.ToString("F6") : $"{value}";
    }

    public static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    public static object ConvertValue(string value, Type targetType)
    {
        if (targetType == typeof(string))
            return value;

        if (targetType == typeof(float) && float.TryParse(value, out var floatResult))
            return floatResult;

        if (targetType == typeof(int) && int.TryParse(value, out var intResult))
            return intResult;

        throw new NotSupportedException($"Type {targetType.Name} is not supported.");
    }

    public static string SanitizeHeader(string header)
    {
        return CharNumberRegex().Replace(header, "_");
    }

    [GeneratedRegex(@"[^a-zA-Z0-9_]")]
    public static partial Regex CharNumberRegex();
}
