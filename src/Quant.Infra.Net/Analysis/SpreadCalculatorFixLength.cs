using Microsoft.Data.Analysis;
using Quant.Infra.Net.Shared.Extension;
using Quant.Infra.Net.Shared.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quant.Infra.Net.Analysis
{
    /// <summary>
    /// PairTrading的残差计算器，定长窗体外推法
    /// </summary>
    public abstract class SpreadCalculatorFixLength
    {
        /// <summary>
        /// 默认定长183， 默认单位：天
        /// </summary>
        public virtual int FixedWindowDays { get; set; } = 183;

        public virtual double BusinessHoursDaily { get; set; } = 24;

        public int FixedWindowLength { get; set; }
        public ResolutionLevel ResolutionLevel { get; set; } = ResolutionLevel.Daily;
        public string Symbol1 { get; set; }
        public string Symbol2 { get; set; }

        protected DataFrame _dataFrame;

        public DataFrame DataFrame
        {
            get
            {
                return _dataFrame;
            }
            set
            {
                _dataFrame = value;
            }
        }

        protected SpreadCalculatorFixLength(
            string symbol1,
            string symbol2,
            DataFrame df1,
            DataFrame df2,
            ResolutionLevel resolutionLevel = ResolutionLevel.Daily)
        {
            // Check if DataFrames have DateTime and Close columns
            if (!df1.Columns.Any(x => x.Name == "DateTime") || !df1.Columns.Any(x => x.Name == "Close"))
            {
                throw new ArgumentException($"DataFrame for {symbol1} must contain 'DateTime' and 'Close' columns.");
            }
            if (!df2.Columns.Any(x => x.Name == "DateTime") || !df2.Columns.Any(x => x.Name == "Close"))
            {
                throw new ArgumentException($"DataFrame for {symbol2} must contain 'DateTime' and 'Close' columns.");
            }

            Symbol1 = symbol1;
            Symbol2 = symbol2;

            ResolutionLevel = resolutionLevel;
            this.FixedWindowLength = CalcuWindowLength(ResolutionLevel);

            // Merge DataFrames on DateTime
            _dataFrame = MergeDataFrames(symbol1, symbol2, df1, df2);
        }

        /// <summary>
        /// 根据DateTime合并两个df，合并后保留三列：DateTime, $"{symbol1}Close",$"{symbol2}Close"
        /// </summary>
        /// <param name="symbol1"></param>
        /// <param name="symbol2"></param>
        /// <param name="df1"></param>
        /// <param name="df2"></param>
        /// <returns></returns>
        protected DataFrame MergeDataFrames(string symbol1, string symbol2, DataFrame df1, DataFrame df2)
        {
            // Rename Close columns to include symbol names
            df1.Columns["Close"].SetName($"{symbol1}Close");
            df2.Columns["Close"].SetName($"{symbol2}Close");

            // Merge DataFrames on DateTime using inner join and keep only the necessary columns
            var mergedDf = df1.Merge(
                df2,
                new string[] { "DateTime" },
                new string[] { "DateTime" },
                joinAlgorithm: JoinAlgorithm.Inner);

            // 改名DateTime_left为DateTime， 删除列：DateTime_right;
            mergedDf.Columns["DateTime_left"].SetName("DateTime");

            // Remove DateTime_right column
            mergedDf.Columns.Remove("DateTime_right");

            return mergedDf;
        }

        /// <summary>
        /// _dataFrame有三列：DateTime, $"{symbol1}Close",$"{symbol2}Close"；
        /// 根据DateTime更新或插入_dataFrame的值，如果不存在，则插入; 如果存在，则更新
        /// </summary>
        /// <param name="dateTime"></param>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        public void UpsertRow(DateTime dateTime, double symbol1Value, double symbol2Value)
        {
            // 验证_dateFrame已经初始化并包含三列：DateTime, {symbol1}Close, {symbol2}Close,如果没有，则抛出异常
            // 验证 _dataFrame 是否初始化以及是否包含所需的三列
            if (_dataFrame == null)
            {
                throw new InvalidOperationException("_dataFrame is not initialized.");
            }

            // 确保 DataFrame 包含必要的列
            if (!(_dataFrame.Columns.Any(x => x.Name == "DateTime") &&
                  _dataFrame.Columns.Any(x => x.Name == $"{Symbol1}Close") &&
                  _dataFrame.Columns.Any(x => x.Name == $"{Symbol2}Close")))
            {
                throw new ArgumentException($"DataFrame must contain 'DateTime', '{Symbol1}Close', and '{Symbol2}Close' columns.");
            }

            var rowIndex = this._dataFrame.GetRowIndex<DateTime>("DateTime", dateTime);

            if (rowIndex >= 0)
            {
                // 行已存在，更新 symbol1Close 和 symbol2Close 列的值
                _dataFrame.Columns[$"{Symbol1}Close"][rowIndex] = symbol1Value;
                _dataFrame.Columns[$"{Symbol2}Close"][rowIndex] = symbol2Value;
            }
            else
            {
                // 行不存在，插入新行
                var newRow = new List<object>
                {
                    dateTime,
                    symbol1Value,
                    symbol2Value
                };
                _dataFrame = _dataFrame.Append(newRow);
            }

            //  如果_dataFrame中：DateTime, $"{symbol1}Close",$"{symbol2}Close"，任何一行一列的值为Null或者默认值， 则删除改行
            _dataFrame = DropRowsWithNullsOrDefaults();

            UpsertSpreadAndEquation();
        }

        /// <summary>
        /// 根据输入的endDateTime,向前FixedWindowLength，得到[startDateTime, endDateTime],计算spread
        /// </summary>
        /// <param name="endDateTime"></param>
        /// <returns>结束日期endDateTime，如果为null，说明：取数据源最新的日期</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public double CalculateSpread(DateTime? endDateTime = null)
        {
            ValidateSourceData();

            // 如果dateTime == null, 则dateTime为_dataFrame中最晚的DateTime
            if (endDateTime == null)
            {
                var dateTimeColumn = _dataFrame.Columns["DateTime"];
                endDateTime = dateTimeColumn.Cast<DateTime>().Max();
            }

            var (seriesA, seriesB) = GetSeries(endDateTime.Value);

            // 计算spread,注意：此时SeriesA和SeriesB为DateTime倒序;
            var (spread, equation) = CalculateSpreadAndEquation(Symbol1, Symbol2, seriesA, seriesB);

            return spread;
        }

        /// <summary>
        /// 根据_dataFrame有三列：DateTime, $"{symbol1}Close", $"{symbol2}Close"和现有记录，添加和更新Spread和Equation列。
        /// 已经有值的行会自动跳过，以节约算力;
        /// </summary>
        public void UpsertSpreadAndEquation()
        {
            ValidateSourceData(); // 验证_dataFrame的数据前提

            // Ensure the DataFrame contains the necessary columns
            if (!_dataFrame.Columns.Any(x => x.Name == "Spread"))
            {
                // Create the new column for Spread with double type
                var spreadColumn = new DoubleDataFrameColumn("Spread", _dataFrame.Rows.Count);
                _dataFrame.Columns.Add(spreadColumn);
            }

            if (!_dataFrame.Columns.Any(x => x.Name == "Equation"))
            {
                // Create the new column for Equation with double type
                var equationColumn = new StringDataFrameColumn("Equation", _dataFrame.Rows.Count);
                _dataFrame.Columns.Add(equationColumn);
            }

            for (int i = 0; i < _dataFrame.Rows.Count; i++)
            {
                if (i <= FixedWindowLength)
                    continue;

                // Get the current DateTime value
                var currentDateTime = (DateTime)_dataFrame.Columns["DateTime"][i];
                // 获取改行Spread和Equation，如果有任何一个不是默认值，则赋值，否则continue;
                // Check if Spread or Equation is already set
                var currentSpread = _dataFrame.Columns["Spread"][i] != null ? (double)_dataFrame.Columns["Spread"][i] : default(double);
                var currentEquation = _dataFrame.Columns["Equation"][i] != null ? (string)_dataFrame.Columns["Equation"][i] : default(string);

                if (currentSpread != default && currentEquation != default)
                    continue;

                var (seriesA, seriesB) = GetSeries(currentDateTime);
                var (spread, equation) = CalculateSpreadAndEquation(Symbol1, Symbol2, seriesA, seriesB);

                // Update the DataFrame with calculated Spread and Equation
                _dataFrame.Columns["Spread"][i] = spread;
                _dataFrame.Columns["Equation"][i] = equation;
            }
        }

        /// <summary>
        /// 根据endDateTime获取SeriesA, SeriesB
        /// </summary>
        /// <param name="endDateTime"></param>
        /// <returns></returns>
        private (List<double>, List<double>) GetSeries(DateTime endDateTime)
        {
            // 找到 endDateTime 所在行的索引
            var dateTimeColumnData = _dataFrame.Columns["DateTime"];
            int endIndex = -1;
            for (int i = 0; i < dateTimeColumnData.Length; i++)
            {
                if ((DateTime)dateTimeColumnData[i] == endDateTime)
                {
                    endIndex = i;
                    break;
                }
            }

            if (endIndex == -1)
            {
                throw new ArgumentException($"endDateTime {endDateTime} not found in the DataFrame.");
            }

            // 计算 startIndex 向前移动 FixedWindowLength 行
            int startIndex = Math.Max(endIndex - FixedWindowLength + 1, 0);

            // 根据[startDateTime, endDateTime]范围提取数据
            var seriesA = new List<double>();
            var seriesB = new List<double>();

            for (int i = startIndex; i <= endIndex; i++)
            {
                var row = _dataFrame.Rows[i];
                seriesA.Add((double)row[$"{Symbol1}Close"]);
                seriesB.Add((double)row[$"{Symbol2}Close"]);
            }

            return (seriesA, seriesB);
        }

        /// <summary>
        /// 计算spread和equation
        /// </summary>
        /// <param name="seriesA"></param>
        /// <param name="seriesB"></param>
        /// <returns></returns>
        private (double, string) CalculateSpreadAndEquation(string symbol1, string symbol2, IEnumerable<double> seriesA, IEnumerable<double> seriesB)
        {
            // 执行线性回归，获取slope和interception
            var (slope, intercept) = (new Analysis.Service.AnalysisService()).PerformOLSRegression(seriesA, seriesB);

            // 计算spread,注意：此时SeriesA和SeriesB为DateTime倒序;
            var lastElmInSeriesA = seriesA.FirstOrDefault();
            var lastElmInSeriesB = seriesB.FirstOrDefault();
            var spread = lastElmInSeriesB - slope * lastElmInSeriesA - intercept;
            var equation = $"{symbol2} -  {slope}*{symbol1} - {intercept}";
            return (spread, equation);
        }

        /// <summary>
        /// 生成并返回Equation公式
        /// </summary>
        /// <param name="endDateTime"></param>
        /// <returns>结束日期endDateTime，如果为null，说明：取数据源最新的日期</returns>
        /// <exception cref="ArgumentException"></exception>
        public string PrintEquation(DateTime? endDateTime = null)
        {
            ValidateSourceData(); // 验证_dataFrame的数据前提

            // 更新Spread和Equation
            UpsertSpreadAndEquation();

            // 如果dateTime == null, 则dateTime为_dataFrame中最晚的DateTime
            if (endDateTime == null)
            {
                var dateTimeColumn = _dataFrame.Columns["DateTime"];
                endDateTime = dateTimeColumn.Cast<DateTime>().Max();
            }

            // 找到对应的行
            int rowIndex = -1;
            for (int i = 0; i < _dataFrame.Rows.Count; i++)
            {
                var rowDateTime = (DateTime)_dataFrame.Columns["DateTime"][i];
                if (rowDateTime == endDateTime.Value)
                {
                    rowIndex = i;
                    break;
                }
            }

            if (rowIndex == -1)
            {
                throw new ArgumentException($"No row found for the specified endDateTime: {endDateTime}");
            }

            // 获取方程公式
            var equationColumn = _dataFrame.Columns["Equation"];
            string equation = (string)equationColumn[rowIndex];

            return equation;
        }

        /// <summary>
        /// 检验数据源是否合格？
        /// </summary>
        /// <returns></returns>
        private void ValidateSourceData()
        {
            // 检查 _dataFrame 是否为空
            if (_dataFrame == null || _dataFrame.Rows.Count == 0)
            {
                throw new InvalidOperationException("数据框为空或没有数据");
            }

            // 定义需要检查的列名
            var requiredColumns = new[] { "DateTime", $"{Symbol1}Close", $"{Symbol2}Close" };
            // 检查列名是否存在
            foreach (var column in requiredColumns)
            {
                if (!_dataFrame.Columns.Any(x => x.Name == column))
                {
                    throw new InvalidOperationException($"列名 {column} 不存在");
                }
            }

            // 检查 _dataFrame 元素个数是否超过 FixedWindowLength
            if (_dataFrame.Rows.Count <= FixedWindowLength)
            {
                throw new InvalidOperationException("元素个数不符合要求");
            }
        }

        /// <summary>
        /// 根据resolutionLevel计算window的长度;
        /// </summary>
        /// <param name="resolutionLevel"></param>
        /// <returns></returns>
        protected abstract int CalcuWindowLength(ResolutionLevel resolutionLevel = ResolutionLevel.Daily);

        /// <summary>
        /// 删除空置行，类似与Python中的Dropna()
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public DataFrame DropRowsWithNullsOrDefaults()
        {
            // Ensure the DataFrame contains the necessary columns
            if (!_dataFrame.Columns.Any(x => x.Name == "DateTime") ||
                !_dataFrame.Columns.Any(x => x.Name == $"{Symbol1}Close") ||
                !_dataFrame.Columns.Any(x => x.Name == $"{Symbol2}Close"))
            {
                throw new InvalidOperationException("The DataFrame must contain the DateTime and Close columns.");
            }

            var dateTimeColumn = _dataFrame.Columns["DateTime"];
            var closeColumn1 = _dataFrame.Columns[$"{Symbol1}Close"];
            var closeColumn2 = _dataFrame.Columns[$"{Symbol2}Close"];

            // List to hold the rows that need to be removed
            var rowsToRemove = new List<int>();

            for (int i = 0; i < _dataFrame.Rows.Count; i++)
            {
                // Check for null or default values
                var dateTimeValue = dateTimeColumn[i];
                var closeValue1 = closeColumn1[i];
                var closeValue2 = closeColumn2[i];

                if (dateTimeValue == null ||
                    IsDefaultValue(closeValue1) ||
                    IsDefaultValue(closeValue2))
                {
                    rowsToRemove.Add(i);
                }
            }

            // Remove rows starting from the last row to the first to avoid index shifting issues
            foreach (var rowIndex in rowsToRemove.OrderByDescending(index => index))
            {
                _dataFrame = _dataFrame.RemoveAt(rowIndex);
            }

            return _dataFrame;
        }

        /// <summary>
        /// 判断value是否为默认值
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool IsDefaultValue(object value)
        {
            if (value == null)
                return true;

            // Check if value is a nullable type
            var type = value.GetType();

            // Use default value comparison for common types
            if (type == typeof(double))
                return (double)value == default(double);

            if (type == typeof(DateTime))
                return (DateTime)value == default(DateTime);

            if (type == typeof(string))
                return string.IsNullOrEmpty((string)value);

            if (type == typeof(int))
                return (int)value == default(int);

            if (type == typeof(bool))
                return (bool)value == default(bool);

            if (type == typeof(float))
                return (float)value == default(float);

            if (type == typeof(long))
                return (long)value == default(long);

            var defaultValue = Activator.CreateInstance(type);
            return value.Equals(defaultValue);
        }
    }
}