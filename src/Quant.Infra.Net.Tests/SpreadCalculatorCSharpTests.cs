using Quant.Infra.Net.Analysis;
using Quant.Infra.Net.Shared.Extension;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class SpreadCalculatorCSharpTests
    {
        
        [TestMethod]
        public void SpreadCalculator_Init_Should_Work()
        {
            // Arrange
            var symbol1 = "ALGOUSDT";
            var symbol2 = "DASHUSDT";
            var sourceFullPathFilename1 = AppDomain.CurrentDomain.BaseDirectory + $"data\\{symbol1}.csv";
            var sourceFullPathFilename2 = AppDomain.CurrentDomain.BaseDirectory + $"data\\{symbol2}.csv";
            var df1 = UtilityService.LoadCsvToDataFrame(sourceFullPathFilename1);
            var df2 = UtilityService.LoadCsvToDataFrame(sourceFullPathFilename2);

            // Action
            var calculator = new SpreadCalculatorPerpetualContract(symbol1, symbol2, df1, df2, resolutionLevel: ResolutionLevel.Hourly);

            // Assety
            // 如果无报错，说明工作正常
        }


        [TestMethod]
        public void UpsertRow_Should_Work()
        {
            // todo
            // Arrange
            var symbol1 = "ALGOUSDT";
            var symbol2 = "DASHUSDT";
            var sourceFullPathFilename1 = AppDomain.CurrentDomain.BaseDirectory + $"data\\{symbol1}.csv";
            var sourceFullPathFilename2 = AppDomain.CurrentDomain.BaseDirectory + $"data\\{symbol2}.csv";
            var df1 = UtilityService.LoadCsvToDataFrame(sourceFullPathFilename1);
            var df2 = UtilityService.LoadCsvToDataFrame(sourceFullPathFilename2);

            // Action
            var calculator = new SpreadCalculatorPerpetualContract(symbol1, symbol2, df1, df2, resolutionLevel: ResolutionLevel.Hourly);

            calculator.UpsertSpreadAndEquation();
            var spreads = calculator.GetSpreadsFromColumn();

            var dt = new DateTime(2024, 8, 1);
            var algoPrice = 0.1377;
            var dashPrice = 25.77;
            calculator.UpsertRow(dt,algoPrice,dashPrice);

            var dateTimeColumn = calculator.DataFrame.Columns["DateTime"];
            var endDateTime = dateTimeColumn.Cast<DateTime>().Max();

            // Fetch the row index for the given endDateTime
            int rowIndex = calculator.DataFrame.GetRowIndex("DateTime", endDateTime);

            double spread = rowIndex != -1 ? (double)calculator.DataFrame["Spread"][rowIndex] : default(double);
            string equation = rowIndex != -1 ? (string)calculator.DataFrame["Equation"][rowIndex] : default(string);
            double halfLife = rowIndex != -1 ? (double)calculator.DataFrame["HalfLife"][rowIndex] : default(double);

            // Print the results
            Console.WriteLine($"End DateTime: {endDateTime}");
            Console.WriteLine($"Spread: {spread}");
            Console.WriteLine($"Equation: {equation}");
            Console.WriteLine($"Half Life: {Math.Round(halfLife,2)}");
        }


        [TestMethod]
        public void GetSpread_Should_Work()
        {
            // Arrange
            var symbol1 = "ALGOUSDT";
            var symbol2 = "DASHUSDT";
            var sourceFullPathFilename1 = AppDomain.CurrentDomain.BaseDirectory + $"data\\{symbol1}.csv";
            var sourceFullPathFilename2 = AppDomain.CurrentDomain.BaseDirectory + $"data\\{symbol2}.csv";
            var df1 = UtilityService.LoadCsvToDataFrame(sourceFullPathFilename1);
            var df2 = UtilityService.LoadCsvToDataFrame(sourceFullPathFilename2);

            // Action
            var calculator = new SpreadCalculatorPerpetualContract(symbol1, symbol2, df1, df2, resolutionLevel: ResolutionLevel.Hourly);
            calculator.UpsertSpreadAndEquation();
            var dateTimeColumn = calculator.DataFrame.Columns["DateTime"];
            var endDateTime = dateTimeColumn.Cast<DateTime>().Max();

            // Fetch the row index for the given endDateTime
            int rowIndex = calculator.DataFrame.GetRowIndex("DateTime", endDateTime);
            double spread = rowIndex != -1 ? (double)calculator.DataFrame["Spread"][rowIndex] : default(double);
            string equation = rowIndex != -1 ? (string)calculator.DataFrame["Equation"][rowIndex] : default(string);

            // Print the results
            Console.WriteLine($"End DateTime: {endDateTime}");
            Console.WriteLine($"Spread: {spread}");
            Console.WriteLine($"Equation: {equation}");

            // Optionally, assert values for validation
            Assert.IsNotNull(spread, "Spread value should not be null.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(equation), "Equation value should not be null or empty.");
        }

       
    }
}



        



    

