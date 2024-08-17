using Quant.Infra.Net.Shared.Service;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class PythonNetTests
    {
        [TestMethod]
        public void DownloadBTCData_Should_Work()
        {
            var targetPath = AppDomain.CurrentDomain.BaseDirectory + "data\\result";
            var symbol = "AAPL";
            var targetFileName = $"{symbol}.csv";
            var targetFullPathFileName = Path.Combine(targetPath, targetFileName);
            var pythonFileName = "Functions";
            var pythonFunctionName = "fetch_and_save_financial_data";
            var parameterObjs = new List<object>()
            {
                targetPath,
                targetFileName,
                "2023-1-1",
                "2024-8-1",
                "1h",
                symbol
            }; //小时级数据最多730天
            var pythonDirectories = new List<string>();
            pythonDirectories.Add(AppDomain.CurrentDomain.BaseDirectory + "Python");
            var venvPath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading";
            var pythonDll = "python39.dll";
            var pyObjectResponse = UtilityService.ExecutePython(pythonFileName, pythonFunctionName, parameterObjs, pythonDirectories, venvPath, pythonDll);

            var targetFileExist = File.Exists(targetFullPathFileName);
            Assert.IsTrue(targetFileExist);
        }
    }
}
