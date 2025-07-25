using Microsoft.Data.Analysis;
using Quant.Infra.Net.Analysis.Models;
using Quant.Infra.Net.Shared.Extension;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
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
        public virtual double BusinessHoursDaily { get; set; } = 24;

        public virtual int HalfLifeWindowLength { get; set; } = 20; // 计算半衰期的窗口长度，通常用1个月比较合适; 

        public virtual int CointegrationFixedWindowLength { get; set; } = 183;

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



        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbol1"></param>
        /// <param name="symbol2"></param>
        /// <param name="df1">需要包括:DateTime, Close列</param>
        /// <param name="df2">需要包括:DateTime, Close列</param>
        /// <param name="resolutionLevel"></param>
        /// <exception cref="ArgumentException"></exception>
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
            this.CointegrationFixedWindowLength = CalcuWindowLength(ResolutionLevel);

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

            var (seriesA, seriesB) = GetSpreadAndEquationSeries(endDateTime.Value);

            // 计算spread
            var row = CalculateSpreadAndEquation(Symbol1, Symbol2, seriesA, seriesB);

            return row.Spread;
        }

        /// <summary>
        /// 根据_dataFrame有三列：DateTime, $"{symbol1}Close", $"{symbol2}Close"和现有记录，添加和更新Spread、Equation和HalfLife列。
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

            if (!_dataFrame.Columns.Any(x => x.Name == "Slope"))
            {
                // Create the new column for Equation with double type
                var slopeColumn = new DoubleDataFrameColumn("Slope", _dataFrame.Rows.Count);
                _dataFrame.Columns.Add(slopeColumn);
            }

            if (!_dataFrame.Columns.Any(x => x.Name == "Intercept"))
            {
                // Create the new column for Equation with double type
                var interceptColumn = new DoubleDataFrameColumn("Intercept", _dataFrame.Rows.Count);
                _dataFrame.Columns.Add(interceptColumn);
            }

            // 如果不存在，增加column: HalfLife
            if (!_dataFrame.Columns.Any(x => x.Name == "HalfLife"))
            {
                // Create the new column for Equation with double type
                var halfLifeColumn = new DoubleDataFrameColumn("HalfLife", _dataFrame.Rows.Count);
                _dataFrame.Columns.Add(halfLifeColumn);
            }

            for (int i = 0; i < _dataFrame.Rows.Count; i++)
            {
                if (i <= CointegrationFixedWindowLength)
                    continue;

                // Get the current DateTime value
                var currentDateTime = (DateTime)_dataFrame.Columns["DateTime"][i];
                // 获取改行Spread和Equation，如果有任何一个不是默认值，则赋值，否则continue;
                // Check if Spread or Equation is already set
                var currentSpread = _dataFrame.Columns["Spread"][i] != null ? (double)_dataFrame.Columns["Spread"][i] : default(double);
                var currentEquation = _dataFrame.Columns["Equation"][i] != null ? (string)_dataFrame.Columns["Equation"][i] : default(string);

                if (currentSpread != default && currentEquation != default)
                    continue;

                var (seriesA, seriesB) = GetSpreadAndEquationSeries(currentDateTime);
                var row = CalculateSpreadAndEquation(Symbol1, Symbol2, seriesA, seriesB);

                // Update the DataFrame with calculated Spread and Equation
                _dataFrame.Columns["Spread"][i] = row.Spread;
                _dataFrame.Columns["Equation"][i] = row.Equation;
                _dataFrame.Columns["Slope"][i] = row.Slope;
                _dataFrame.Columns["Intercept"][i] = row.Intercept;
            }


            // 循环行，赋值 HalfLife 列的值;
            for (int i = 0; i < _dataFrame.Rows.Count; i++)
            {
                if (i <= Math.Max(HalfLifeWindowLength, CointegrationFixedWindowLength) + HalfLifeWindowLength)
                    continue;

                if (i >= _dataFrame.Rows.Count)
                    break;

                // 从当前日期向前获取HalfLifeWindowLength个Elements, 作为halflife的输入;
                var spreads = new List<Element>();
                var currentDateTime = (DateTime)_dataFrame.Columns["DateTime"][i];
                spreads = GetSpreadSeries(currentDateTime, HalfLifeWindowLength).OrderBy(x => x.DateTime).Select(x => x).ToList(); 
                var halfLife = UtilityService.CalculateHalfLife(spreads, HalfLifeWindowLength);
                _dataFrame.Columns["HalfLife"][i] = halfLife;
            }
        }


        /// <summary>
        /// 根据DateTime列的排序，从Spread列获得数据， 并返回; 需要在GetSpreadsFromColumn以后调用;
        /// </summary>
        /// <returns></returns>
        public IEnumerable<double> GetSpreadsFromColumn()
        {
            var spreads = new List<double>();
            for (int i = 0; i < _dataFrame.Rows.Count; i++)
            {
                var spreadObj = _dataFrame.Columns["Spread"][i];
                if (spreadObj != null && Convert.ToDouble(spreadObj) != 0)
                {
                    spreads.Add(Convert.ToDouble(spreadObj));
                }
            }
            return spreads;
        }

        /// <summary>
        /// 获取最晚的单元格的记录
        /// </summary>
        /// <param name="colName"></param>
        /// <returns></returns>
        public Object GetTheLastCellValue(string colName)
        {
            if (string.IsNullOrEmpty(colName))
            {
                throw new ArgumentNullException();
            }
            if (this.DataFrame.Columns.Any(x => x.Name == colName) == false)
            {
                throw new ArgumentOutOfRangeException($"column name:{colName} does not exist.");
            }

            var dateTimeColumn = this.DataFrame.Columns["DateTime"];
            var endDateTime = dateTimeColumn.Cast<DateTime>().Max();

            // Fetch the row index for the given endDateTime
            int rowIndex = this.DataFrame.GetRowIndex("DateTime", endDateTime);
            Object obj = rowIndex != -1 ? (Object)this.DataFrame[colName][rowIndex] : default(Object);
            return obj;
        }

        /// <summary>
        /// 根据endDateTime获取SeriesA, SeriesB
        /// </summary>
        /// <param name="endDateTime"></param>
        /// <returns></returns>
        private (List<Element>, List<Element>) GetSpreadAndEquationSeries(DateTime endDateTime)
        {
            var listA = new List<Element>();
            var listB = new List<Element>();

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
            int startIndex = Math.Max(endIndex - CointegrationFixedWindowLength + 1, 0);

            // 根据[startDateTime, endDateTime]范围提取数据
            for (int i = startIndex; i <= endIndex; i++)
            {
                var row = _dataFrame.Rows[i];
                var dateTime = (DateTime)row["DateTime"];
                var elmA = new Element(Symbol1,dateTime, Convert.ToDouble(row[$"{Symbol1}Close"]));
                listA.Add(elmA);

                var elmB = new Element(Symbol2,dateTime, Convert.ToDouble(row[$"{Symbol2}Close"]));
                listB.Add(elmB);
            }

            return (listA, listB);
        }

        private List<Element> GetSpreadSeries(DateTime inclusiveEndDateTime, int halfLifeWindowLength)
        {
            var list = new List<Element>();
            // 找到 endDateTime 所在行的索引
            var dateTimeColumnData = _dataFrame.Columns["DateTime"];
            int endIndex = -1;
            for (int i = 0; i < dateTimeColumnData.Length; i++)
            {
                if ((DateTime)dateTimeColumnData[i] == inclusiveEndDateTime)
                {
                    endIndex = i;
                    break;
                }
            }

            if (endIndex == -1)
            {
                throw new ArgumentException($"endDateTime {inclusiveEndDateTime} not found in the DataFrame.");
            }

            // 计算 startIndex 向前移动 FixedWindowLength 行
            int startIndex = endIndex - halfLifeWindowLength + 1;

            // 根据[startDateTime, endDateTime]范围提取数据
            for (int i = startIndex; i <= endIndex; i++)
            {
                var row = _dataFrame.Rows[i];
                var dateTime = (DateTime)row["DateTime"];
                var elm = new Element("Spread", dateTime, Convert.ToDouble(row["Spread"]));
                list.Add(elm);
            }

            return list;
        }


        /// <summary>
        /// 计算spread和equation
        /// </summary>
        /// <param name="seriesA"></param>
        /// <param name="seriesB"></param>
        /// <returns></returns>
        private SpreadCalculatorRow CalculateSpreadAndEquation(string symbol1, string symbol2, IEnumerable<Element> seriesA, IEnumerable<Element> seriesB)
        {
            var sortedSeriesValueA = seriesA.OrderBy(x => x.DateTime).Select(x => x.Value).ToList(); // 确保SeriesA按DateTime正序排列
            var sortedSeriesValueB = seriesB.OrderBy(x => x.DateTime).Select(x => x.Value).ToList(); // 确保SeriesB按DateTime正序排列

            var row = new SpreadCalculatorRow();
            // 执行线性回归，获取slope和interception
            var (slope, intercept) = (new Analysis.Service.AnalysisService()).PerformOLSRegression(sortedSeriesValueA, sortedSeriesValueB);
            row.Slope = slope;
            row.Intercept = intercept;

            // 计算spread,注意：此时SeriesA和SeriesB为DateTime正序;
            var lastElmInSeriesA = sortedSeriesValueA.LastOrDefault();
            var lastElmInSeriesB = sortedSeriesValueB.LastOrDefault();
            var spread = lastElmInSeriesB - slope * lastElmInSeriesA - intercept;
            var equation = $"spread = {symbol2} -  {slope} * {symbol1} - {intercept}";
            row.Spread = spread;
            row.Equation = equation;
            return row;
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
                throw new InvalidOperationException("Empty database");
            }

            // 定义需要检查的列名
            var requiredColumns = new[] { "DateTime", $"{Symbol1}Close", $"{Symbol2}Close" };
            // 检查列名是否存在
            foreach (var column in requiredColumns)
            {
                if (!_dataFrame.Columns.Any(x => x.Name == column))
                {
                    throw new InvalidOperationException($"column: {column} does not exist.");
                }
            }

            // 检查 _dataFrame 元素个数是否超过 FixedWindowLength
            if (_dataFrame.Rows.Count <= CointegrationFixedWindowLength)
            {
                throw new InvalidOperationException("element number is not verified.");
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