using System.IO;

namespace Quant.Infra.Net.Shared.Model
{
    public class PythonInfraModel
    {
        public string PythonDLL { get; set; }
        public string PythonHome { get; set; }
        public string PythonPath { get; set; }
    }

    public class PythonNetInfra
    {
        /// <summary>
        /// 输入PythonPath，返回路径PythonDLL, pythonHome, pythonPath三种变量
        /// </summary>
        /// <param name="pythonPath"></param>
        public static PythonInfraModel GetPythonInfra(string pythonPath, string pythonFilename = "python39.dll")
        {
            var pythonInfra = new PythonInfraModel();
            pythonInfra.PythonHome = pythonPath;
            pythonInfra.PythonDLL = Path.Combine(pythonPath, pythonFilename);
            // 对应Python内的重要路径
            string[] py_paths = {"DLLs", "lib", "lib\\site-packages",   "lib\\site-packages\\win32"
                , "lib\\site-packages\\win32\\lib",   "lib\\site-packages\\Pythonwin" };
            string pySearchPath = $"{pythonInfra.PythonHome};";
            foreach (string p in py_paths)
            {
                var tmpPath = Path.Combine(pythonInfra.PythonHome, p);
                pySearchPath += $"{tmpPath};";
            }
            pythonInfra.PythonPath = pySearchPath;
            return pythonInfra;
        }
    }
}