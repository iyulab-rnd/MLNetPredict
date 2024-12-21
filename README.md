# MLNetPredict

MLNetPredict is a command-line tool for making predictions using trained ML.NET models. It supports various machine learning scenarios and provides a simple interface for batch predictions.

## Features

- Supports multiple ML.NET scenarios:
  - Classification (binary and multi-class)
  - Regression
  - Forecasting
  - Recommendation
  - Text Classification
  - Image Classification
  - Object Detection
- Automatic handling of model dependencies
- Flexible input/output options
- Support for different data formats (CSV, TSV)
- Header detection and delimiter customization

## Installation

To install MLNetPredict, you can use the .NET CLI:

```bash
dotnet tool install --global mlnet-predict
```

## Usage

Basic syntax:
```bash
mlnet-predict <model-path> <input-path> [options]
```

### Required Arguments

- `model-path`: Path to the directory containing the .mlnet model file
- `input-path`: Path to the input file or directory (for image-based tasks)

### Options

- `-o, --output-path`: Path to the output file or directory (optional)
- `--has-header`: Specify if dataset file(s) have header row [true|false]
- `--separator`: Specify the separator character used in the dataset file(s)

### Examples

1. Basic Classification:
```bash
mlnet-predict "models/sentiment" "data/input.csv" --has-header true
```

2. Image Classification with Custom Output:
```bash
mlnet-predict "models/image_classifier" "images/test" -o "results/predictions.csv"
```

3. Forecasting with TSV Input:
```bash
mlnet-predict "models/forecast" "data/timeseries.tsv" --separator "\t"
```

## Input Data Format

### Text-based Tasks (Classification, Regression, etc.)
- Supported formats: CSV, TSV
- Files should contain the required features as columns
- Headers can be included or excluded (use --has-header option)

### Image-based Tasks
- Supported formats: JPG, JPEG, PNG, BMP, GIF
- Input should be a directory containing image files
- Output will be a CSV file with predictions for each image

## Output Format

The output format varies depending on the machine learning task:

### Classification
```csv
PredictedLabel,Score
```
For multi-class classification:
```csv
Top1,Top1Score,Top2,Top2Score,Top3,Top3Score
```

### Regression/Recommendation
```csv
Score
```

### Forecasting
```csv
PredictedValue,LowerBound,UpperBound
```

### Image Classification
```csv
ImagePath,PredictedLabel
```

### Object Detection
```csv
ImagePath,PredictedLabels,BoundingBoxes,Scores
```

## Error Handling

The tool provides detailed error messages for common issues:
- Missing or invalid model files
- Unsupported input formats
- Invalid data format
- Missing required columns
- Model loading errors