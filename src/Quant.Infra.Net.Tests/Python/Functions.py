import os
import yfinance as yf


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
