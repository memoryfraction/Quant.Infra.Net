using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.SourceData.Service;


namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class PythonNetTests
    {
        private readonly ServiceProvider _serviceProvider;
        public PythonNetTests()
        {
            ServiceCollection _serviceCollection = new ServiceCollection();
            // Register the Automapper to container
            _serviceCollection.AddSingleton<IMapper>(sp =>
            {
                var autoMapperConfiguration = new MapperConfiguration(cfg =>
                {
                    cfg.AddProfile<MappingProfile>();
                });
                return new Mapper(autoMapperConfiguration);
            });
            _serviceCollection.AddScoped<ITraditionalFinanceSourceDataService, TraditionalFinanceSourceDataService>();
            _serviceProvider = _serviceCollection.BuildServiceProvider();
        }


        ///// <summary>
        ///// 获取单个数据
        ///// </summary>
        //[TestMethod]
        //public void DownloadSymbolData_Should_Work()
        //{
        //    var targetPath = AppDomain.CurrentDomain.BaseDirectory + "data\\result";
        //    var symbol = "AAPL";
        //    var targetFileName = $"{symbol}.csv";
        //    var targetFullPathFileName = Path.Combine(targetPath, targetFileName);
        //    var pythonFileName = "Functions";
        //    var pythonFunctionName = "fetch_and_save_financial_data";
        //    var parameterObjs = new List<object>()
        //    {
        //        targetPath,
        //        targetFileName,
        //        "2023-1-1",
        //        "2024-8-1",
        //        "1h",
        //        symbol
        //    }; // 小时级数据最多730天
        //    var pythonDirectories = new List<string>();
        //    pythonDirectories.Add(AppDomain.CurrentDomain.BaseDirectory + "Python");
        //    var venvPath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading";
        //    var pythonDll = "python39.dll";
        //    var pyObjectResponse = UtilityService.ExecutePython(pythonFileName, pythonFunctionName, parameterObjs, pythonDirectories, venvPath, pythonDll);

        //    var targetFileExist = File.Exists(targetFullPathFileName);
        //    Assert.IsTrue(targetFileExist);
        //}


        ///// <summary>
        ///// 批量获取数据
        ///// </summary>
        //[TestMethod]
        //public async Task DownloadSymbolsData_Should_Work()
        //{
        //    var targetPath = AppDomain.CurrentDomain.BaseDirectory + "data\\result";
        //    var sourceDataService = _serviceProvider.GetRequiredService<ITraditionalFinanceSourceDataService>();
        //    //var symbols = await sourceDataService.GetSp500SymbolsAsync();
        //    var symbols = new List<string>() { "AAPL", "NVDA"};
        //    var pythonFileName = "Functions";
        //    var pythonFunctionName = "fetch_and_save_financial_data_symbols";
        //    var parameterObjs = new List<object>()
        //    {
        //        symbols.Select(x => x.ToPython()).ToArray(),
        //        "2023-1-1",
        //        "2024-8-1",
        //        targetPath,
        //        "1h"
        //    }; // 小时级数据最多730天
        //    var pythonDirectories = new List<string>();
        //    pythonDirectories.Add(AppDomain.CurrentDomain.BaseDirectory + "Python");
        //    var venvPath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading";
        //    var pythonDll = "python39.dll";
        //    UtilityService.ExecutePython(pythonFileName, pythonFunctionName, parameterObjs, pythonDirectories, venvPath, pythonDll);
        //}


    }
}
