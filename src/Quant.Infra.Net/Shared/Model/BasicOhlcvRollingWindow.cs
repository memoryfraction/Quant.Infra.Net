using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Quant.Infra.Net.Shared.Model
{
    /// <summary>
    /// RollingWindow - 量化历史数据的滑动窗口，新数据进入时，历史数据自动移除
    ///    * 初始化定义长度
    ///    * .Add() - OpenDateTime最早的元素会被移除
    ///    * IsReday
    ///    * 继承接口: IEnumerable<>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BasicOhlcvRollingWindow<T> : IEnumerable<T> where T : BasicOhlcv
    {
        private readonly int _size;              // The maximum size of the window
        private readonly List<T> _window;        // The list to store the window's elements
        private bool _isReady;                   // Indicates if the window has reached its full size

        // Property to check if the window has reached its full capacity
        public bool IsReady => _isReady;

        public BasicOhlcvRollingWindow(int size)
        {
            if (size <= 0)
                throw new ArgumentException("Size must be greater than zero.", nameof(size));

            _size = size;
            _window = new List<T>(size);
            _isReady = false;
        }

        // Method to add new elements to the rolling window
        public void Add(T item)
        {
            // Ensure the OHLCV data is valid
            if (!item.IsValid())
            {
                throw new InvalidOperationException("The OHLCV data is invalid.");
            }

            // Add the new item to the list
            _window.Add(item);

            // Sort the window by OpenDateTime, or by CloseDateTime if OpenDateTime is invalid
            _window.Sort((x, y) =>
            {
                // First compare by OpenDateTime if both are valid
                if (x.OpenDateTime != default(DateTime) && y.OpenDateTime != default(DateTime))
                {
                    return x.OpenDateTime.CompareTo(y.OpenDateTime);
                }
                // If one of them has an invalid OpenDateTime, fallback to CloseDateTime comparison
                if (x.OpenDateTime == default(DateTime) && y.OpenDateTime == default(DateTime))
                {
                    return x.CloseDateTime.CompareTo(y.CloseDateTime);
                }
                // If one has invalid OpenDateTime, prioritize the one with valid OpenDateTime
                return x.OpenDateTime == default(DateTime) ? 1 : -1;
            });

            // If the number of items exceeds the window size, remove the oldest item (based on OpenDateTime)
            if (_window.Count > _size)
            {
                _window.RemoveAt(0); // Remove the element with the earliest OpenDateTime
            }

            // Mark the window as ready if it has reached full capacity
            if (_window.Count == _size)
            {
                _isReady = true;
            }
        }

        // Returns the latest OHLCV record based on the close date (most recent OpenDateTime)
        public T Latest()
        {
            if (_window.Count == 0)
                throw new InvalidOperationException("The window is empty.");

            return _window.Last(); // Returns the most recent item based on OpenDateTime
        }

        // Returns the current count of the window
        public int Count => _window.Count;

        // Returns an enumerator to iterate through the window
        public IEnumerator<T> GetEnumerator()
        {
            return _window.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}