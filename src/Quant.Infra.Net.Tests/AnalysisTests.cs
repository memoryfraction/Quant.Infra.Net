using Microsoft.Extensions.DependencyInjection;
﻿using AutoMapper;
using Flurl.Util;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Service;
using ScottPlot;
using System.Diagnostics;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class AnalysisTests
    {
        private readonly ServiceProvider _serviceProvider;
        public AnalysisTests()
        {
            ServiceCollection _serviceCollection = new ServiceCollection();
            _serviceCollection.AddScoped<ISourceDataService, SourceDataService>();
            _serviceCollection.AddScoped<IAnalysisService, AnalysisService>();
            _serviceCollection.AddScoped<UtilityService>();
            _serviceProvider = _serviceCollection.BuildServiceProvider();
        }

        [TestMethod]
        public void CorrelationTest_Should_Work()
        {
            // Arrange
            double[] seriesA = { 1, 2, 3, 4, 5 };
            double[] seriesB = { 2, 4, 6, 8, 10 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            double correlation = _analysisService.CalculateCorrelation(seriesA, seriesB);

            // Assert
            Assert.AreEqual(1.0, correlation);
        }

        [TestMethod]
        public void StationaryTest_Should_Work()
        {
            // Arrange
            double[] stationarySeries = { 1, -1, 1, -1, 1, -1, 1, -1 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            bool isStationary = _analysisService.PerformADFTest(stationarySeries,0.05);

            // Assert
            Assert.AreEqual(true, isStationary);
        }

        [TestMethod]
        public void NonStationaryTest_Should_Work()
        {
            // Arrange
            double[] nonStationarySeries = { 1, 2, 3, 4, 5, 6, 7, 8 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            bool isStationary = _analysisService.PerformADFTest(nonStationarySeries, 0.05);

            // Assert
            Assert.AreEqual(false, isStationary);
        }


        [TestMethod]
        public void LinearRegression1_Should_Work()
        {
            // Arrange
            double[] seriesA = { 1, 2, 3, 4, 5 };
            double[] seriesB = { 2, 4, 6, 8, 10 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            var (slope, intercept) = _analysisService.PerformLinearRegression(seriesA, seriesB);

            // Assert
            // diff = B - 2 * A - 0
            Assert.AreEqual(2.0, slope);
            Assert.AreEqual(0.0, intercept);
            // Console equation
            Console.WriteLine($"diff = B - ({slope} * A) - ({intercept})");
        }

        [TestMethod]
        public void LinearRegression2_Should_Work()
        {
            // Arrange
            double[] seriesA = { 100, 102, 104, 108, 110, 115, 120, 125, 130, 135 };
            double[] seriesB = { 200, 205, 210, 220, 225, 235, 245, 255, 265, 275 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            var (slope, intercept) = _analysisService.PerformLinearRegression(seriesA, seriesB);

            // Assert
            // diff = B - 2.1237583090596757 * A - (-10.519829710956742)
            Assert.AreEqual(2.1237583090596757, slope);
            Assert.AreEqual(-10.519829710956742, intercept);
            // Console equation
            Console.WriteLine($"diff = B - ({slope} * A) - ({intercept})");
        }

        [TestMethod]
        public async Task LinearRegression3_Should_Work()
        {
            // Generate simulated stock price data
            // Fixed simulated stock price data for PepsiCo and Coca-Cola
            double[] pricesA = new double[] { 169.89, 170.0, 169.5, 170.2, 169.8, 170.1, 169.9, 170.3, 169.7, 170.4, 169.6, 170.5, 169.8, 170.2, 169.9, 170.1, 169.7, 170.3, 169.8, 170.0, 169.9, 170.2, 169.7, 170.4, 169.6, 170.5, 169.8, 170.2, 169.9, 170.1 };
            double[] pricesB = new double[] { 65.21, 65.25, 65.1, 65.3, 65.2, 65.4, 65.15, 65.35, 65.2, 65.4, 65.1, 65.3, 65.2, 65.35, 65.15, 65.4, 65.2, 65.35, 65.1, 65.3, 65.2, 65.4, 65.15, 65.35, 65.2, 65.4, 65.1, 65.3, 65.2, 65.35 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            var (slope, intercept) = _analysisService.PerformLinearRegression(pricesA, pricesB);

            // Assert
            Assert.AreEqual(0.31196395040950814, slope);
            Assert.AreEqual(12.221565751700417, intercept);
            Console.WriteLine($"diff = B - ({slope} * A) - ({intercept})");

            // Calculate diff
            double[] diff = new double[pricesA.Length];
            for (int i = 0; i < pricesA.Length; i++)
            {
                diff[i] = pricesB[i] - (slope * pricesA[i] + intercept);
            }

            // Plot diff using ScottPlot
            var plt = new Plot();
            plt.Add.Bars(diff);
            plt.Title($"Differences chart - diff[i] = B - {slope} * A + {intercept}");
            plt.XLabel("Day");
            plt.YLabel("Difference");
            string fullPathFilename = Path.Combine(AppContext.BaseDirectory, "output", "diff_bar_chart.png");
            await UtilityService.IsPathExistAsync(fullPathFilename);
            plt.SaveJpeg(fullPathFilename, 600,400);

            // Open the generated image using the default program
            Process.Start(new ProcessStartInfo(fullPathFilename) { UseShellExecute = true });
        }

        [TestMethod]
        public void NormalDistributionTest_Should_Work()
        {
            // Arrange
            double[] normalDistribution = { 10.0, 12.0, 11.0, 13.0, 10.5, 12.5, 11.5, 13.5, 10.2, 12.2, 11.2, 13.2 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            bool isNormal = _analysisService.PerformShapiroWilkTest(normalDistribution, 0.05);

            // Assert
            Assert.AreEqual(true, isNormal);
        }

        [TestMethod]
        public void NonNormalDistributionTest_Should_Work()
        {
            // Arrange
            double[] nonNormalDistribution = { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            bool isNormal = _analysisService.PerformShapiroWilkTest(nonNormalDistribution, 0.05);

            // Assert
            Assert.AreEqual(false, isNormal);
        }

    }
}
