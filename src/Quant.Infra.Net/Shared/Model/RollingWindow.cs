using System;
using System.Collections;
using System.Collections.Generic;

namespace Quant.Infra.Net.Shared.Model
{
    /// <summary>
    /// 使用队列实现的滑动窗口，新数据进入时自动移除最早的数据。
    /// A sliding window implemented with a queue; old data is automatically removed when new data enters.
    /// </summary>
    /// <typeparam name="T">窗口元素的类型 / The type of elements in the window.</typeparam>
    public class RollingWindow<T> : IEnumerable<T>
    {
        private readonly int _size;              // 窗口的最大大小
        private readonly Queue<T> _window;       // 存储窗口元素的队列
        private bool _isReady;                    // 窗口是否已填满

        /// <summary>
        /// 初始化滑动窗口，指定窗口大小。
        /// Initializes the rolling window with the specified size.
        /// </summary>
        /// <param name="size">窗口的最大大小，必须大于零 / The maximum window size, must be greater than zero.</param>
        /// <exception cref="ArgumentException">当 size 小于等于零时抛出 / Thrown when size is less than or equal to zero.</exception>
        public RollingWindow(int size)
        {
            if (size <= 0)
                throw new ArgumentException("Size must be greater than zero.", nameof(size));

            _size = size;
            _window = new Queue<T>(size);
            _isReady = false;
        }

        /// <summary>
        /// 向窗口中添加新元素，超出窗口大小时自动移除最早的元素。
        /// Adds a new element to the window; the oldest element is removed when the window size is exceeded.
        /// </summary>
        /// <param name="item">要添加的元素 / The element to add.</param>
        public void Add(T item)
        {
            // 添加新项到队列
            _window.Enqueue(item);

            // 如果元素数量超过窗口大小，移除最早添加的元素
            if (_window.Count > _size)
            {
                _window.Dequeue(); // 移除第一个元素
            }

            // 更新 IsReady 属性
            _isReady = _window.Count == _size;
        }

        /// <summary>
        /// 返回当前窗口的元素数量。
        /// Returns the current number of elements in the window.
        /// </summary>
        public int Count => _window.Count;

        /// <summary>
        /// 检查窗口是否已填满。
        /// Checks whether the window has reached its full capacity.
        /// </summary>
        public bool IsReady => _isReady;

        /// <summary>
        /// 返回用于迭代窗口元素的枚举器。
        /// Returns an enumerator for iterating over the window elements.
        /// </summary>
        /// <returns>枚举器 / The enumerator.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            return _window.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 返回窗口中最新的元素。
        /// Returns the most recent element in the window.
        /// </summary>
        /// <returns>最新的元素 / The most recent element.</returns>
        /// <exception cref="InvalidOperationException">当窗口为空时抛出 / Thrown when the window is empty.</exception>
        public T Latest()
        {
            if (_window.Count == 0)
                throw new InvalidOperationException("The window is empty.");

            return _window.ToArray()[_window.Count - 1]; // Returns the most recent element (last in the queue)
        }

    }
}