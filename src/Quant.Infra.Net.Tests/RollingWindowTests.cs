using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Linq;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class RollingWindowTests
    {
        /// <summary>
        /// Tests that items are successfully added to the rolling window.
        /// 测试项是否成功添加到滚动窗口中。
        /// </summary>
        [TestMethod]
        public void Test_RollingWindow_Add_SuccessfullyAddsItems()
        {
            // Arrange
            var windowSize = 3;
            var rollingWindow = new RollingWindow<BasicOhlcv>(windowSize);

            var item1 = new BasicOhlcv
            {
                Symbol = "AAPL",
                OpenDateTime = DateTime.Parse("2024-09-01"),
                CloseDateTime = DateTime.Parse("2024-09-01 15:30:00"),
                Open = 150,
                High = 155,
                Low = 148,
                Close = 154,
                Volume = 1000000
            };

            var item2 = new BasicOhlcv
            {
                Symbol = "AAPL",
                OpenDateTime = DateTime.Parse("2024-09-02"),
                CloseDateTime = DateTime.Parse("2024-09-02 15:30:00"),
                Open = 152,
                High = 157,
                Low = 149,
                Close = 156,
                Volume = 1100000
            };

            var item3 = new BasicOhlcv
            {
                Symbol = "AAPL",
                OpenDateTime = DateTime.Parse("2024-09-03"),
                CloseDateTime = DateTime.Parse("2024-09-03 15:30:00"),
                Open = 155,
                High = 160,
                Low = 150,
                Close = 158,
                Volume = 1200000
            };

            // Act
            rollingWindow.Add(item1);
            rollingWindow.Add(item2);
            rollingWindow.Add(item3);

            // Assert
            Assert.AreEqual(3, rollingWindow.Count);
            Assert.IsTrue(rollingWindow.IsReady); // Window should be ready after 3 elements
        }

        /// <summary>
        /// Tests that the oldest item is removed when the rolling window is full and a new item is added.
        /// 测试在滚动窗口满时，添加新项会移除最旧的项。
        /// </summary>
        [TestMethod]
        public void Test_RollingWindow_Add_RemovesOldestWhenFull()
        {
            // Arrange
            var windowSize = 2;
            var rollingWindow = new RollingWindow<BasicOhlcv>(windowSize);

            var item1 = new BasicOhlcv
            {
                Symbol = "AAPL",
                OpenDateTime = DateTime.Parse("2024-09-01"),
                CloseDateTime = DateTime.Parse("2024-09-01 15:30:00"),
                Open = 150,
                High = 155,
                Low = 148,
                Close = 154,
                Volume = 1000000
            };

            var item2 = new BasicOhlcv
            {
                Symbol = "AAPL",
                OpenDateTime = DateTime.Parse("2024-09-02"),
                CloseDateTime = DateTime.Parse("2024-09-02 15:30:00"),
                Open = 152,
                High = 157,
                Low = 149,
                Close = 156,
                Volume = 1100000
            };

            var item3 = new BasicOhlcv
            {
                Symbol = "AAPL",
                OpenDateTime = DateTime.Parse("2024-09-03"),
                CloseDateTime = DateTime.Parse("2024-09-03 15:30:00"),
                Open = 155,
                High = 160,
                Low = 150,
                Close = 158,
                Volume = 1200000
            };

            // Act
            rollingWindow.Add(item1);
            rollingWindow.Add(item2);
            rollingWindow.Add(item3); // This should remove item1 (oldest OpenDateTime)

            // Assert
            Assert.AreEqual(2, rollingWindow.Count); // Ensure the window size is correct

            var items = rollingWindow.ToList(); // Convert to list to inspect the contents

            // Assert that item1 has been removed and item2 and item3 remain
            Assert.IsFalse(items.Contains(item1)); // item1 should be removed
            Assert.IsTrue(items.Contains(item2)); // item2 should be present
            Assert.IsTrue(items.Contains(item3)); // item3 should be present

            // Ensure items are in the expected order (item2 should be the oldest)
            Assert.AreEqual(item2, items.First()); // item2 should be the first (oldest)
            Assert.AreEqual(item3, items.Last());  // item3 should be the last (most recent)
        }

        /// <summary>
        /// Tests that the Latest method returns the most recent item in the rolling window.
        /// 测试 Latest 方法返回滚动窗口中最新的项。
        /// </summary>
        [TestMethod]
        public void Test_RollingWindow_Latest_ReturnsMostRecent()
        {
            // Arrange
            var windowSize = 3;
            var rollingWindow = new RollingWindow<BasicOhlcv>(windowSize);

            var item1 = new BasicOhlcv
            {
                Symbol = "AAPL",
                OpenDateTime = DateTime.Parse("2024-09-01"),
                CloseDateTime = DateTime.Parse("2024-09-01 15:30:00"),
                Open = 150,
                High = 155,
                Low = 148,
                Close = 154,
                Volume = 1000000
            };

            var item2 = new BasicOhlcv
            {
                Symbol = "AAPL",
                OpenDateTime = DateTime.Parse("2024-09-02"),
                CloseDateTime = DateTime.Parse("2024-09-02 15:30:00"),
                Open = 152,
                High = 157,
                Low = 149,
                Close = 156,
                Volume = 1100000
            };

            var item3 = new BasicOhlcv
            {
                Symbol = "AAPL",
                OpenDateTime = DateTime.Parse("2024-09-03"),
                CloseDateTime = DateTime.Parse("2024-09-03 15:30:00"),
                Open = 155,
                High = 160,
                Low = 150,
                Close = 158,
                Volume = 1200000
            };

            // Act
            rollingWindow.Add(item1);
            rollingWindow.Add(item2);
            rollingWindow.Add(item3);

            var latestItem = rollingWindow.Latest();

            // Assert
            Assert.AreEqual(item3, latestItem); // item3 is the most recent
        }

        /// <summary>
        /// Tests that the Latest method throws an exception when the rolling window is empty.
        /// 测试当滚动窗口为空时，Latest 方法抛出异常。
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Test_RollingWindow_Latest_ThrowsExceptionWhenEmpty()
        {
            // Arrange
            var rollingWindow = new RollingWindow<BasicOhlcv>(3);

            // Act
            var latestItem = rollingWindow.Latest(); // Should throw an exception

            // Assert - Expects an InvalidOperationException
        }

    }
}
