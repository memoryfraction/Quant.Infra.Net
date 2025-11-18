using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quant.Infra.Net.Shared
{
    /// <summary>
    /// Responsible for thread-safe initialization and cleanup of the Python.NET engine and GIL.
    /// Integrated the path construction logic (previously in PythonNetInfra) for self-contained configuration.
    /// </summary>
    public static class PythonInitializer
    {
        // Lock object to ensure initialization happens only once
        private static readonly object InitializationLock = new object();
        private static volatile bool IsInitialized = false;
        private static IntPtr? InitialGilStateHandle = null;

        // --- START OF CONFIGURABLE PROPERTIES (解决 todo) ---

        /// <summary>
        /// 获取或设置 Conda/虚拟环境的根路径。默认值已根据用户环境设置。
        /// </summary>
        public static string CondaVenvHomePath { get; set; } = @"D:\ProgramData\PythonVirtualEnvs\pair_trading";

        /// <summary>
        /// 获取或设置 Python DLL 的文件名（例如: python39.dll）。
        /// </summary>
        public static string PythonDllFileName { get; set; } = "python39.dll";

        // --- END OF CONFIGURABLE PROPERTIES ---


        // 关键的 Python 搜索子路径（保持不变）
        private static readonly string[] PySearchSubPaths = {
            "DLLs",
            "lib",
            "lib\\site-packages",
            "lib\\site-packages\\win32",
            "lib\\site-packages\\win32\\lib",
            "lib\\site-packages\\Pythonwin"
        };


        /// <summary>
        /// Ensures the Python engine is initialized only once and releases the GIL immediately.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (IsInitialized)
            {
                return;
            }

            lock (InitializationLock)
            {
                if (IsInitialized)
                {
                    return;
                }

                Console.WriteLine("--- PythonInitializer: Starting Initialization ---");

                // --- 0. 警告抑制：清除 PYTHONTZPATH 环境变量 ---
                // 解决 InvalidTZPathWarning 警告。
                Console.WriteLine("[INFO] Clearing PYTHONTZPATH to suppress InvalidTZPathWarning.");
                Environment.SetEnvironmentVariable("PYTHONTZPATH", null);


                // --- 1. 内部构建 Python 路径配置 ---

                // 1.1 构建 PythonDLL 的绝对路径
                // 使用属性值
                var pythonDllFullPath = Path.Combine(CondaVenvHomePath, PythonDllFileName);

                // 1.2 构造 PythonPath
                // 使用属性值
                var pySearchPathList = new List<string> { CondaVenvHomePath };
                pySearchPathList.AddRange(
                    PySearchSubPaths.Select(p => Path.Combine(CondaVenvHomePath, p))
                );
                var pythonPathConfig = string.Join(";", pySearchPathList);

                // 1.3 PythonHome 
                var pythonHomeConfig = CondaVenvHomePath;


                // --- 2. 设置 DLL path and Python environment ---
                try
                {
                    Runtime.PythonDLL = pythonDllFullPath;
                    PythonEngine.PythonHome = pythonHomeConfig;
                    PythonEngine.PythonPath = pythonPathConfig;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to set Python paths: {ex.Message}");
                    throw;
                }

                // --- 3. Initialize the engine ---
                try
                {
                    PythonEngine.Initialize();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] PythonEngine.Initialize() failed: {ex.Message}");
                    throw;
                }

                // --- 4. Crucial: Release the GIL ---
                InitialGilStateHandle = PythonEngine.BeginAllowThreads();

                IsInitialized = true;
                Console.WriteLine("Python engine initialized successfully, GIL released.");
            }
        }

        /// <summary>
        /// Shuts down the Python engine and cleans up resources.
        /// </summary>
        public static void Shutdown()
        {
            lock (InitializationLock)
            {
                if (!IsInitialized)
                {
                    return;
                }
                Console.WriteLine("--- PythonInitializer: Shutting Down Engine ---");

                // 1. Clean up GIL handle
                if (InitialGilStateHandle.HasValue)
                {
                    PythonEngine.EndAllowThreads(InitialGilStateHandle.Value);
                    InitialGilStateHandle = null;
                }

                // 2. Shut down the engine
                if (PythonEngine.IsInitialized)
                {
                    try
                    {
                        PythonEngine.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        // --- 关键修改：静默忽略 BinaryFormatter 异常 (解决关闭警告) ---
                        if (ex.Message.Contains("BinaryFormatter serialization and deserialization have been removed"))
                        {
                            // 忽略此异常，不打印输出，以清理控制台。
                            return;
                        }

                        // 打印其他所有异常
                        Console.WriteLine($"[WARNING] Exception occurred during PythonEngine.Shutdown(): {ex.Message}");
                    }
                }
                IsInitialized = false;
            }
        }
    }
}
