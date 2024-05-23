# MLNetPredict

MLNetPredict is a .NET tool for predicting various machine learning tasks using ML.NET. It supports multiple scenarios including classification, regression, forecasting, recommendation, and image/text classification.

## Purpose

The `mlnet-cli` or `mlnet model builder` tools are designed to output strongly-typed model files and project files, which require the project to be built in order to be used. The purpose of this library is to execute predictions based on ML.NET models and output files without needing to modify the project. It allows for predictions to be made directly from input files.

## Features

- **Classification**: Predict categories for given inputs.
- **Regression**: Predict continuous values.
- **Forecasting**: Predict future values based on historical data.
- **Recommendation**: Provide recommendations based on user/item interactions.
- **Image Classification**: Classify images into predefined categories.
- **Text Classification**: Classify text data into predefined categories.

## Installation

To install the MLNetPredict tool, use the following command:

```bash
dotnet tool install --global mlnet-predict
```

## Usage

### Basic Usage

```bash
mlnet-predict <model-path> <input-path> [options]
```

- `model-path`: Path to the directory containing the .mlnet model file.
- `input-path`: Path to the input file or directory.

### Options

- `-o`, `--output-path`: Path to the output directory (optional).
- `--has-header`: Specify [true|false] if the dataset file(s) have a header row.
- `--separator`: Specify the separator character used in the dataset file(s).

### Example Commands

#### Classification

```bash
mlnet-predict models/GithubIssues files/github-issue/input.csv --has-header true
```

#### Regression

```bash
mlnet-predict models/TaxiFarePrediction files/taxi-fare/input.csv
```

#### Forecasting

```bash
mlnet-predict models/HourlyEnergyConsumption files/hourly_energy_consumption/input.json
```

#### Recommendation

```bash
mlnet-predict models/MovieRecommendation files/movie-recommendation/input.csv
```

#### Image Classification

```bash
mlnet-predict models/ImageClassification files/images/
```

#### Text Classification

```bash
mlnet-predict models/TextClassification files/texts/input.csv --has-header true
```

## Prerequisites

Before running predictions, ensure that your ML.NET model and consumption code are properly generated. Use the following commands to create your models:

### Example for Classification

```bash
mlnet classification --dataset "files/github-issue/issues.tsv" --label-col "Area" --train-time 120 --output "models" --name "GithubIssues" --log-file-path "./models/GithubIssues/logs.txt"
```

### Example for Regression

```bash
mlnet regression --dataset "files/taxi-fare/taxi-fare-train.csv" --label-col "fare_amount" --validation-dataset "files/taxi-fare/taxi-fare-test.csv" --has-header true --name "TaxiFarePrediction" --train-time 120 --output "models" --log-file-path "./models/Sales/logs.txt"
```

## Testing

Unit tests for the tool are available in the `MLNetPredict.Tests` namespace. Use the following command to run the tests:

```bash
dotnet test
```