from abc import ABC, abstractmethod
from datetime import datetime
from enum import Enum
from typing import List
import pandas as pd
import statsmodels.api as sm
import numpy as np
from pandas import DataFrame


class ResolutionLevel(Enum):
    Tick = "t"
    Second = "s"
    Minute = "min"
    Hourly = "h"
    Daily = "d"
    Weekly = "wk"
    Monthly = "mo"
    Other = "other"

class TimeSeriesElement:
    def __init__(self, date_time: datetime, value: float):
        self.date_time = date_time
        self.value = value

    def __str__(self):
        return f"DateTime: {self.date_time}, Value: {self.value}"

    def __eq__(self, other):
        if not isinstance(other, TimeSeriesElement):
            return False
        return self.date_time == other.date_time and self.value == other.value

    def __hash__(self):
        return hash((self.date_time, self.value))

class TimeSeries:
    def __init__(self):
        self.series = []

    def __str__(self):
        return f"TimeSeries with {len(self.series)} elements: [{', '.join(str(elm) for elm in self.series)}]"

    def __eq__(self, other):
        if not isinstance(other, TimeSeries):
            return False
        return self.series == other.series

    def __hash__(self):
        return hash(tuple(self.series))

    def add_element(self, element):
        """
        Add a TimeSeriesElement to the series.
        :param element: TimeSeriesElement object
        """
        self.series.append(element)

    def __len__(self):
        """
        Return the number of elements in the TimeSeries.
        :return: int
        """
        return len(self.series)

    def __getitem__(self, index):
        """
        Get a TimeSeriesElement by index.
        :param index: int
        :return: TimeSeriesElement
        """
        return self.series[index]

class SpreadCalculator(ABC):
    def __init__(self, symbol1: str, symbol2: str, resolution: ResolutionLevel = ResolutionLevel.Daily):
        self.symbol1 = symbol1
        self.symbol2 = symbol2
        self.resolution = resolution
        self.FixedWindowLength = 0
        self.df = DataFrame()

    def update_time_series(self, time_series1: List[TimeSeriesElement], time_series2: List[TimeSeriesElement]):
        if len(time_series1) != len(time_series2):
            raise ValueError("time_series1 and time_series2 must have the same number of elements.")

        for i in range(len(time_series1)):
            if time_series1[i].date_time != time_series2[i].date_time:
                raise ValueError(f"Element {i} of time_series1 and time_series2 have different DateTime values.")

        # update self.df using self.symbol1 , self.symbol2, and the given parameters
        df1 = pd.DataFrame({
            "date_time": [element.date_time for element in time_series1],
            self.symbol1: [element.value for element in time_series1]
        })

        # Convert time_series2 to DataFrame
        df2 = pd.DataFrame({
            "date_time": [element.date_time for element in time_series2],
            self.symbol2: [element.value for element in time_series2]
        })

        # Merge the DataFrames on date_time
        self.df = pd.merge(df1, df2, on="date_time")

        # Convert the date_time column to datetime from Timestamp
        self.df["date_time"] = pd.to_datetime(self.df["date_time"], errors='coerce')

        # Drop rows with NaN values in any of the columns
        self.df = self.df.dropna()

        # Convert the relevant columns to numeric values and find non-numeric rows
        for symbol in [self.symbol1, self.symbol2]:
            self.df[symbol] = pd.to_numeric(self.df[symbol], errors='coerce')

        # Find and print rows with NaN values in self.symbol1 or self.symbol2
        non_numeric_rows = self.df[self.df[[self.symbol1, self.symbol2]].isna().any(axis=1)]
        if not non_numeric_rows.empty:
            print("Rows with non-numeric values:")
            print(non_numeric_rows)

        # Drop rows where self.symbol1 or self.symbol2 contains NaN values
        self.df = self.df.dropna(subset=[self.symbol1, self.symbol2])

        # Set the 'date_time' column as the index
        self.df.set_index('date_time', inplace=True)

    def update_time_series_element(self, time_series_elm1: TimeSeriesElement, time_series_elm2: TimeSeriesElement):
        """
        单个元素更新self.df的symbol1, symbol2两列，如果符合条件，更新spread和equation列;
        :param time_series_elm1:
        :param time_series_elm2:
        :return:
        """
        #  如果 time_series_elm1和 time_series_elm2的date_time不同，则报错。
        # Check if date_time values are the same
        if time_series_elm1.date_time != time_series_elm2.date_time:
            raise ValueError(f"DateTime mismatch: {time_series_elm1.date_time} vs {time_series_elm2.date_time}")

        # 根据输入值插入或者更新self.df
        # Create a DataFrame for the new element
        new_row = pd.DataFrame({
            "date_time": [time_series_elm1.date_time],
            self.symbol1: [time_series_elm1.value],
            self.symbol2: [time_series_elm2.value]
        })

        #  date_time is index,  If date_time already exists in self.df, update the existing row
        # Set 'date_time' as the index for the new row
        new_row.set_index('date_time', inplace=True)

        # Check if the date_time already exists in self.df
        if time_series_elm1.date_time in self.df.index:
            # Update the existing row
            self.df.loc[time_series_elm1.date_time, [self.symbol1, self.symbol2]] = new_row.loc[
                time_series_elm1.date_time, [self.symbol1, self.symbol2]]
        else:
            # Append the new row to the DataFrame
            self.df = pd.concat([self.df, new_row])

        # Drop rows with NaN values if any
        self.df = self.df.dropna()

        # 如果self.df中time_series_elm1.date_time之前的记录数 >= self.FixedWindowLength，计算并更新spread和equation列;
        # 如果self.df.at[time_series_elm1.date_time, 'spread']和self.df.at[time_series_elm1.date_time, 'equation']是None，再更新
        if ('spread' not in self.df.columns
                or 'equation' not in self.df.columns
                or time_series_elm1.date_time not in self.df.index
                or pd.isna(self.df.at[time_series_elm1.date_time, 'spread'])
                or pd.isna(self.df.at[time_series_elm1.date_time, 'equation'])):
            if len(self.df[self.df.index < time_series_elm1.date_time]) >= self.FixedWindowLength:
                # Extract the relevant series for the calculation
                relevant_df = self.df[self.df.index < time_series_elm1.date_time]
                series_a = relevant_df[self.symbol1].iloc[-self.FixedWindowLength:]
                series_b = relevant_df[self.symbol2].iloc[-self.FixedWindowLength:]

                # Perform OLS regression and update spread and equation
                ols_result = self.ols_regression(series_a.values, series_b.values)
                slope = ols_result["slope"]
                intercept = ols_result["intercept"]

                self.df.at[time_series_elm1, 'slope'] = slope
                self.df.at[time_series_elm1, 'intercept'] = intercept

                # Calculate the spread for the current date_time
                last_value_a = time_series_elm1.value
                last_value_b = time_series_elm2.value
                calculated_spread = last_value_a - (slope * last_value_b + intercept)

                # Update spread and equation columns
                self.df.at[time_series_elm1.date_time, 'spread'] = calculated_spread
                self.df.at[
                    time_series_elm1.date_time, 'equation'] = f"spread = {self.symbol1} - ({slope:.4f} * {self.symbol2} + {intercept:.4f})"

    def upsert_spread_and_equation(self):
        """
        在self.df都完全的前提下，根据self.FixedWindowLength更新self.df中的spread列和Equation列
        """
        # 如果self.df中没有对应的self.symbol1, self.symbol2列，则报错.
        if self.symbol1 not in self.df.columns or self.symbol2 not in self.df.columns:
            raise ValueError(f"DataFrame must contain columns for {self.symbol1} and {self.symbol2}.")

        # 跳过self.FixedWindowLength行
        for i in range(1, len(self.df)):
            if i < self.FixedWindowLength:
                continue
            current_datetime = self.df.index[i]
            prev_datetime = self.df.index[i - 1]

            # self.spread中，当前日期(current_datetime)向前取self.FixedWindowLength个数据点，得到seriesA，seriesB
            window_end = i
            window_start = i - self.FixedWindowLength
            series_a = self.df.iloc[window_start:window_end][self.symbol1].values
            series_b = self.df.iloc[window_start:window_end][self.symbol2].values

            # 调用ols_regression(seriesA，seriesB)，得到ols_result
            ols_result = self.ols_regression(series_a, series_b)
            slope = ols_result["slope"]
            intercept = ols_result["intercept"]

            self.df.at[current_datetime, 'slope'] = slope
            self.df.at[current_datetime, 'intercept'] = intercept

            # 根据ols_result，循环计算: spread = y(the last) - (slope * x(the last) + intercept)
            last_value_a = self.df.at[current_datetime, self.symbol1]
            last_value_b = self.df.at[current_datetime, self.symbol2]

            # Calculate spread
            calculated_spread = last_value_a - (slope * last_value_b + intercept)

            # 根据spread,更新对应行的spread值，并根据公式
            self.df.at[current_datetime, 'spread'] = calculated_spread

            # 根据公式: spread = A - (slope * B + intercept) 更新equation列;
            equation = f"spread = {self.symbol1} - ({slope} * {self.symbol2} + {intercept})"
            self.df.at[current_datetime, 'equation'] = equation

    @abstractmethod
    def calculate_window_length(self, resolution_level: ResolutionLevel) -> int:
        """
        根据输入的级别，计算window的结果;
        :param resolution_level:
        :return:
        """
        pass

    def ols_regression(self, seriesA, seriesB):
        # Convert series to numpy arrays if they are not already
        series_a = np.array(seriesA)
        series_b = np.array(seriesB)

        # Verify that series_a and series_b are not empty
        if series_a.size == 0 or series_b.size == 0:
            raise ValueError("seriesA and seriesB must not be empty.")

        # Verify that series_a and series_b have the same length
        if len(series_a) != len(series_b):
            raise ValueError("seriesA and seriesB must have the same length.")

        # Check for non-numeric elements
        non_numeric_A = [x for x in series_a if not isinstance(x, (int, float))]
        if non_numeric_A:
            print(f"Non-numeric elements in seriesA: {non_numeric_A}")

        non_numeric_B = [x for x in series_b if not isinstance(x, (int, float))]
        if non_numeric_B:
            print(f"Non-numeric elements in seriesB: {non_numeric_B}")

        # Validate that all elements are numeric
        if not np.issubdtype(series_a.dtype, np.number):
            raise ValueError("seriesA must contain only numeric values.")
        if not np.issubdtype(series_b.dtype, np.number):
            raise ValueError("seriesB must contain only numeric values.")

        # Verify seriesA and seriesB contain at least two data points
        if len(series_a) < 2 or len(series_b) < 2:
            raise ValueError("seriesA and seriesB must contain at least two data points for OLS regression.")

        # Add a constant term for the intercept
        series_b = sm.add_constant(series_b)

        # Fit the OLS model
        model = sm.OLS(series_a, series_b)
        results = model.fit()

        # Return the regression coefficients and intercept
        return {
            "slope": results.params[1],  # Slope
            "intercept": results.params[0]  # Intercept
        }


class SpreadCalculatorSP500(SpreadCalculator):

    def __init__(self, symbol1: str, symbol2: str, resolution: ResolutionLevel = ResolutionLevel.Daily):
        super().__init__(symbol1, symbol2, resolution)
        self.FixedWindowLength = self.calculate_window_length(resolution)

    def calculate_window_length(self, resolution_level: ResolutionLevel) -> int:
        """
        根据输入的级别，计算窗口的长度。
        :param resolution_level: 分辨率级别
        :return: 窗口长度
        """
        fixed_window_length = 126  # 固定窗口长度, 半年的交易日

        if resolution_level == ResolutionLevel.Daily:
            return fixed_window_length
        elif resolution_level == ResolutionLevel.Weekly:
            return fixed_window_length * 7
        elif resolution_level == ResolutionLevel.Monthly:
            return fixed_window_length * 30
        elif resolution_level == ResolutionLevel.Hourly:
            # 每天6.5小时
            return int(fixed_window_length * 6.5)
        elif resolution_level == ResolutionLevel.Minute:
            # 每天6.5小时 * 60分钟
            return int(fixed_window_length * 6.5 * 60)
        elif resolution_level == ResolutionLevel.Second:
            # 每天6.5小时 * 3600秒
            return int(fixed_window_length * 6.5 * 3600)
        elif resolution_level == ResolutionLevel.Tick:
            # 每次交易的窗口长度取决于实际的实现需求，这里没有具体实现
            raise NotImplementedError("Tick resolution is not implemented.")
        else:
            raise ValueError(f"Unsupported resolution level: {resolution_level}")


class SpreadCalculatorCrypto(SpreadCalculator):
    def __init__(self, symbol1: str, symbol2: str, resolution: ResolutionLevel = ResolutionLevel.Daily):
        super().__init__(symbol1, symbol2, resolution)
        self.FixedWindowLength = self.calculate_window_length(resolution)

    def calculate_window_length(self, resolution_level: ResolutionLevel) -> int:
        """
        根据输入的级别，计算窗口的长度。
        :param resolution_level: 分辨率级别
        :return: 窗口长度
        """
        fixed_window_length = 183  # 固定窗口长度, 半年的交易日

        if resolution_level == ResolutionLevel.Daily:
            return fixed_window_length
        elif resolution_level == ResolutionLevel.Weekly:
            return fixed_window_length * 7
        elif resolution_level == ResolutionLevel.Monthly:
            return fixed_window_length * 30
        elif resolution_level == ResolutionLevel.Hourly:
            # 加密货币市场每天24小时交易
            return fixed_window_length * 24
        elif resolution_level == ResolutionLevel.Minute:
            # 每天24小时 * 60分钟
            return fixed_window_length * 24 * 60
        elif resolution_level == ResolutionLevel.Second:
            # 每天24小时 * 3600秒
            return fixed_window_length * 24 * 3600
        elif resolution_level == ResolutionLevel.Tick:
            # 每次交易的窗口长度取决于实际的实现需求，这里没有具体实现
            raise NotImplementedError("Tick resolution is not implemented.")
        else:
            raise ValueError(f"Unsupported resolution level: {resolution_level}")
