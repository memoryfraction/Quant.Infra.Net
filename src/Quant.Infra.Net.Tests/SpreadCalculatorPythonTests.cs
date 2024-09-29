using CsvHelper.Configuration;
using CsvHelper;
using Python.Runtime;
using Quant.Infra.Net.Analysis;
using Quant.Infra.Net.Shared.Extension;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using System.Globalization;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class SpreadCalculatorPythonTests
    {

        /// <summary>
        /// 对于Python的Ols结果， $"spread = {symbolA} - ({slope} * {symbolB} + {intercept})";
        /// </summary>
        [TestMethod]
        public void Python_LinerRegression_Should_Work()
        {
            // 初始化变量
            var condaVenvHomePath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading";
            var pythonDllFileName = "python39.dll";
            var pythonFullPathFileName = Path.Combine(condaVenvHomePath, pythonDllFileName);
            PythonInfraModel infra = PythonNetInfra.GetPythonInfra(condaVenvHomePath, "python39.dll");
            if (Runtime.PythonDLL == null || Runtime.PythonDLL != pythonFullPathFileName)
            {
                Runtime.PythonDLL = infra.PythonDLL;
            }
            PythonEngine.PythonHome = infra.PythonHome;
            PythonEngine.PythonPath = infra.PythonPath;
            PythonEngine.Initialize(); // 初始化Python引擎

            // 使用Python GIL
            using (Py.GIL())
            {
                try
                {
                    // Import sys and append all directories including modelDirectory
                    var pythonDirectory = AppDomain.CurrentDomain.BaseDirectory + "Python";
                    PythonEngine.Exec($"import sys; sys.path.append(r'{pythonDirectory}');");
                    OLSRegressionData data = new OLSRegressionData();
                    var symbolA = "DASH";
                    var symbolB = "ALGO";
                    var symbol1FullPathFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", $"{symbolA}USDT.csv");
                    var symbol2FullPathFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", $"{symbolB}USDT.csv");
                    // 读取ALGOUSDT.csv， 和DASHUSDT.csv作为SeriesA，SeriesB
                    data.SeriesA = UtilityService.ReadCloseColFromCsv(symbol1FullPathFileName);
                    data.SeriesB = UtilityService.ReadCloseColFromCsv(symbol2FullPathFileName);
                    var pyObjectResponse = RunScript<OLSRegressionData>("MySamplePython", "ols_regression", data);
                    // 从pyObjectResponse中获取回归结果，假设返回对象有属性'a'和'constant'
                    var a = pyObjectResponse.GetItem("a").As<double>();
                    var constant = pyObjectResponse.GetItem("constant").As<double>();

                    string formula = $"spread = {symbolA} - ({a} * {symbolB} + {constant})";
                    Console.WriteLine($"{formula}");
                }
                catch (PythonException ex)
                {
                    Console.WriteLine($"Error importing sys or adding path: {ex.Message}");
                    throw;
                }
            }
        }

        PyObject RunScript<T>(string scriptFileNameWithoutExtension, string methodName, T obj) where T : class
        {
            var pythonScript = Py.Import(scriptFileNameWithoutExtension);
            var pythonObject = obj.ToPython();
            PyObject response = pythonScript.InvokeMethod(methodName, new PyObject[] { pythonObject });
            return response;
        }


        
    }
}



        



    

