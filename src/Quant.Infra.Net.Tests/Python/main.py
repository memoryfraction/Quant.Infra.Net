from Functions import fetch_and_save_financial_data

# Define the parameters
folder_path = "data\\result"
file_name = "AAPL.csv"
start_date = "2023-01-01"
end_date = "2024-08-01"
interval = "1h"  # Daily data
symbol = "AAPL"  # Apple stock data

# Call the function
fetch_and_save_financial_data(folder_path, file_name, start_date, end_date, interval, symbol)