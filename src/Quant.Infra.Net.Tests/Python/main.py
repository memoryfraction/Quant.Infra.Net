from Functions import fetch_and_save_financial_data, fetch_and_save_financial_data_symbols, get_sp500_symbols

"""
# Define the parameters
folder_path = "data\\result"
file_name = "AAPL.csv"
start_date = "2023-01-01"
end_date = "2024-08-01"
interval = "1h"  # Daily data
symbol = "AAPL"  # Apple stock data

# Call the function
fetch_and_save_financial_data(folder_path, file_name, start_date, end_date, interval, symbol)
"""

# 获取sp500 symbols
symbols = get_sp500_symbols()
print(symbols)


# Example usage
#symbols = ['AAPL', 'NVDA']  # List of symbols to fetch
start_date = '2023-01-01'
end_date = '2024-08-01'
folder_path = 'data\\result'  # Folder where the CSV files will be saved
interval = '1h'  # Data interval

# Call the function
fetch_and_save_financial_data_symbols(symbols, start_date, end_date, folder_path, interval)
