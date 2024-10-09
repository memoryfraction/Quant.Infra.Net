using Microsoft.Data.Analysis;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;


namespace Quant.Infra.Net.SourceData.Service.Historical
{
    public class HistoricalDataSourceServiceMySql : IHistoricalDataSourceServiceCryptoMySql
    {
        private readonly IConfiguration _configuration;
        private string _connectionString;

        public HistoricalDataSourceServiceMySql(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration["DataSource:Historical:MySQL:ConnectionString"];
        }

        public Currency BaseCurrency { get; set; }

        /// <summary>
        /// 从MySql读取历史数据，
        /// </summary>
        /// <param name="underlying"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="resolutionLevel"></param>
        /// <returns></returns>
        public async Task<DataFrame> GetHistoricalDataFrameAsync(Underlying underlying, DateTime startDate, DateTime endDate, ResolutionLevel resolutionLevel)
        {
            var ohlcvList = await GetOhlcvListAsync(underlying, startDate, endDate, resolutionLevel);
            // 创建 DataFrame 列，只需要 DateTime 和 Close 列
            var dateTimeColumn = new PrimitiveDataFrameColumn<DateTime>("DateTime");
            var closeColumn = new DoubleDataFrameColumn("Close");

            // 遍历 ohlcvList，向 DataFrame 的列中填充数据
            foreach (var ohlcv in ohlcvList)
            {
                dateTimeColumn.Append(ohlcv.CloseDateTime);  // DateTime 列
                closeColumn.Append((double)ohlcv.Close);     // Close 列
            }

            // 创建 DataFrame 并添加 DateTime 和 Close 列
            var dataFrame = new DataFrame();
            dataFrame.Columns.Add(dateTimeColumn);
            dataFrame.Columns.Add(closeColumn);
           return dataFrame;
        }

        public async Task<IEnumerable<Ohlcv>> GetOhlcvListAsync(Underlying underlying, DateTime startDt, DateTime endDt, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
        {
            var allOhlcvList = new List<Ohlcv>();
            try
            {
                using (var connection = Quant.Infra.Net.Shared.MySqlHelper.GetConnection(_connectionString))
                {
                    connection.Open(); // 打开连接

                    for (var year = startDt.Year; year <= endDt.Year; year++)
                    {
                        var tableName = $"Ohlcv_{year}";

                        // Adjust the start and end date for each year to ensure they are within bounds
                        var yearStartDt = year == startDt.Year ? startDt : new DateTime(year, 1, 1);
                        var yearEndDt = year == endDt.Year ? endDt : new DateTime(year, 12, 31, 23, 59, 59);

                        // Build the SQL query for the current year's table without pagination
                        var cmdText = $@"
                            SELECT
                                symbol, open_date_time, close_date_time, open, high, low, close, volume
                            FROM
                                {tableName}
                            WHERE
                                symbol = @symbol AND
                                open_date_time BETWEEN @startDt AND @endDt";

                        // Define command parameters
                        var commandParameters = new[]
                        {
                            new MySqlParameter("@symbol", underlying.Symbol),
                            new MySqlParameter("@startDt", yearStartDt),
                            new MySqlParameter("@endDt", yearEndDt)
                        };

                        using (var command = new MySqlCommand(cmdText, connection))
                        {
                            command.CommandTimeout = 300; // 设置超时时间为 120 秒
                            command.Parameters.AddRange(commandParameters);

                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var ohlcv = new Ohlcv
                                    {
                                        Symbol = underlying.Symbol,
                                        OpenDateTime = reader.GetDateTime("open_date_time"),
                                        CloseDateTime = reader.GetDateTime("close_date_time"),
                                        Open = reader.GetDecimal("open"),
                                        High = reader.GetDecimal("high"),
                                        Low = reader.GetDecimal("low"),
                                        Close = reader.GetDecimal("close"),
                                        Volume = reader.GetDecimal("volume")
                                    };
                                    allOhlcvList.Add(ohlcv);
                                }
                            }
                        }
                    }
                }

                // Convert the list to a HashSet to remove duplicates, if necessary
                allOhlcvList = new List<Ohlcv>(allOhlcvList);

                // TODO: Transform data based on the resolution level if necessary

                return allOhlcvList;
            }
            catch (Exception ex)
            {
                // Log the exception (you may need to implement your own logging mechanism)
                Serilog.Log.Error($"An error occurred: {ex.Message}");
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw;
            }
        }
    }
}
