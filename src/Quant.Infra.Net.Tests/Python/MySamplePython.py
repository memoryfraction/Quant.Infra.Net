import statsmodels.api as sm
import numpy as np

def ols_regression(data):
    # 提取SeriesA和SeriesB
    series_a = np.array(data.SeriesA)
    series_b = np.array(data.SeriesB)

    # 在自变量中添加常数项以拟合截距
    series_b = sm.add_constant(series_b)

    # 拟合OLS模型
    model = sm.OLS(series_a, series_b)
    results = model.fit()

    # 返回回归系数和截距
    return {
        "a": results.params[1],  # 系数
        "constant": results.params[0]  # 截距
    }
