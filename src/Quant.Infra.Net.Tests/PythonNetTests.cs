using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Tests
{
	[TestClass]
	public class PythonNetTests
	{
		private readonly ServiceProvider _serviceProvider;

		public PythonNetTests()
		{
			ServiceCollection _serviceCollection = new ServiceCollection();

			// 1. 修复 MapperConfiguration 报错
			// 标准写法是直接使用 AddAutoMapper 扩展方法，它能自动处理所有构造函数依赖
			_serviceCollection.AddAutoMapper(cfg =>
			{
				cfg.AddProfile<MappingProfile>();
			}, typeof(MappingProfile).Assembly);

			_serviceCollection.AddScoped<ITraditionalFinanceSourceDataService, TraditionalFinanceSourceDataService>();
			_serviceProvider = _serviceCollection.BuildServiceProvider();
		}

		///// <summary>
		///// 获取单个数据
		///// </summary>
		//[TestMethod]
		//public void DownloadSymbolData_Should_Work()
		//{
		//	var targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "result");
		//	if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);

		//	var symbol = "AAPL";
		//	var targetFileName = $"{symbol}.csv";
		//	var targetFullPathFileName = Path.Combine(targetPath, targetFileName);
		//	var pythonFileName = "Functions";
		//	var pythonFunctionName = "fetch_and_save_financial_data";

		//	var parameterObjs = new List<object>()
		//	{
		//		targetPath,
		//		targetFileName,
		//		"2023-1-1",
		//		"2024-8-1",
		//		"1h",
		//		symbol
		//	};

		//	var pythonDirectories = new List<string> { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Python") };
		//	var venvPath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading";
		//	var pythonDll = "python39.dll";

		//	// 注意：如果 UtilityService 报错，请确保该类定义了 static ExecutePython 方法
		//	var pyObjectResponse = UtilityService.ExecutePython(pythonFileName, pythonFunctionName, parameterObjs, pythonDirectories, venvPath, pythonDll);

		//	var targetFileExist = File.Exists(targetFullPathFileName);
		//	Assert.IsTrue(targetFileExist);
		//}

		///// <summary>
		///// 批量获取数据
		///// </summary>
		//[TestMethod]
		//public async Task DownloadSymbolsData_Should_Work()
		//{
		//	var targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "result");
		//	if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);

		//	var sourceDataService = _serviceProvider.GetRequiredService<ITraditionalFinanceSourceDataService>();
		//	var symbols = new List<string>() { "AAPL", "NVDA" };
		//	var pythonFileName = "Functions";
		//	var pythonFunctionName = "fetch_and_save_financial_data_symbols";

		//	// 假设 ToPython 是你的扩展方法，如果报错请确保引用了相关命名空间
		//	var parameterObjs = new List<object>()
		//	{
		//		symbols.ToArray(),
		//		"2023-1-1",
		//		"2024-8-1",
		//		targetPath,
		//		"1h"
		//	};

		//	var pythonDirectories = new List<string> { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Python") };
		//	var venvPath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading";
		//	var pythonDll = "python39.dll";

		//	UtilityService.ExecutePython(pythonFileName, pythonFunctionName, parameterObjs, pythonDirectories, venvPath, pythonDll);

		//	await Task.CompletedTask;
		//}
	}
}