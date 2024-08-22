import os
import pandas as pd
import yfinance as yf
from bs4 import BeautifulSoup

def fetch_and_save_financial_data(folder_path, file_name, start_date, end_date, interval, symbol):
    """
    Fetch financial data from Yahoo Finance and save it to a CSV file.
    :param folder_path: Directory where the CSV file will be saved
    :param file_name: Name of the CSV file
    :param start_date: Start date for the data (format: 'YYYY-MM-DD')
    :param end_date: End date for the data (format: 'YYYY-MM-DD')
    :param interval: Data interval (e.g., '1d', '1h', '1m')
    :param symbol: Financial symbol (e.g., 'AAPL', 'BTC-USD')
    """
    # Ensure the folder path exists
    if not os.path.exists(folder_path):
        os.makedirs(folder_path)

    # Construct the full file path
    file_path = os.path.join(folder_path, file_name)

    # Download data from Yahoo Finance
    data = yf.download(symbol, start=start_date, end=end_date, interval=interval)

    # Check if data is empty
    if data.empty:
        print("No data found for the given parameters.")
        return

    # Save data to CSV
    data.to_csv(file_path)
    print(f"Data saved to {file_path}")


def fetch_and_save_financial_data_symbols(symbols, start_date, end_date, folder_path, interval):
    """
    Fetch financial data from Yahoo Finance and save it to separate CSV files for each symbol.
    :param symbols: List of financial symbols (e.g., ['AAPL', 'BTC-USD'])
    :param start_date: Start date for the data (format: 'YYYY-MM-DD')
    :param end_date: End date for the data (format: 'YYYY-MM-DD')
    :param folder_path: Directory where the CSV files will be saved
    :param interval: Data interval (e.g., '1d', '1h', '1m')
    """
    # Ensure symbols is not None and not empty
    if symbols is None or len(symbols) == 0:
        print("Error: No symbols provided.")
        return

    # Ensure the folder path exists
    if not os.path.exists(folder_path):
        os.makedirs(folder_path)

    # Download data from Yahoo Finance
    data = yf.download(symbols, start=start_date, end=end_date, interval=interval, group_by='ticker')

    # Check if data is empty
    if data.empty:
        print("No data found for the given parameters.")
        return

    # Save data to CSV
    for symbol in symbols:
        if symbol in data:
            symbol_data = data[symbol]
            symbol_file_path = os.path.join(folder_path, f"{symbol}.csv")
            symbol_data.to_csv(symbol_file_path)
            print(f"Data for {symbol} saved to {symbol_file_path}")
        else:
            print(f"Data for {symbol} not found.")


def get_sp500_symbols():
    """
    获取标准普尔500指数的成分股符号列表
    :return: List of S&P 500 stock symbols
    """
    # 从维基百科抓取S&P 500成分股数据
    url = "https://en.wikipedia.org/wiki/List_of_S%26P_500_companies"
    tables = pd.read_html(url)

    # 通常第一个表格是S&P 500成分股列表
    sp500_table = tables[0]

    # 获取'Symbol'列中的股票符号
    sp500_symbols = sp500_table['Symbol'].tolist()

    return sp500_symbols