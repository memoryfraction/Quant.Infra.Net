using Microsoft.Data.Analysis;
using System;

namespace Quant.Infra.Net.Shared.Extension
{
    public static class DataFrameExtensions
    {

        /// <summary>
        /// 从 DataFrame 中删除指定索引的行，并返回一个新的 DataFrame。
        /// </summary>
        /// <param name="dataFrame">要操作的 DataFrame。</param>
        /// <param name="index">要删除的行的索引。</param>
        /// <returns>删除指定行后的新 DataFrame。</returns>
        /// <exception cref="ArgumentOutOfRangeException">如果 index 超出了 DataFrame 的行范围。</exception>
        public static DataFrame RemoveAt(this DataFrame dataFrame, int index)
        {
            if (index < 0 || index >= dataFrame.Rows.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
            }

            DataFrame newDataFrame = new DataFrame();
            for (int i = 0; i < dataFrame.Rows.Count; i++)
            {
                if (i != index)
                {
                    newDataFrame.Append(dataFrame.Rows[i]);
                }
            }

            return newDataFrame;
        }

        /// <summary>
        /// 输入列名，输入值，返回行index。如果找不到，返回-1
        /// </summary>
        /// <param name="dataFrame"></param>
        /// <param name="columnName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int GetRowIndex<T>(this DataFrame dataFrame, string columnName, T value)
        {
            for (int i = 0; i < dataFrame.Rows.Count; i++)
            {
                if (dataFrame[columnName][i].Equals(value))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}