using System;
using System.Collections;
using System.Collections.Generic;

namespace Quant.Infra.Net.Shared.Model
{
    /// <summary>
    /// RollingWindow - 使用队列实现的滑动窗口
    /// </summary>
    public class RollingWindow<T> : IEnumerable<T>
    {
        private readonly int _size;              // 窗口的最大大小
        private readonly Queue<T> _window;       // 存储窗口元素的队列
        private bool _isReady;                    // 窗口是否已填满

        public RollingWindow(int size)
        {
            if (size <= 0)
                throw new ArgumentException("Size must be greater than zero.", nameof(size));

            _size = size;
            _window = new Queue<T>(size);
            _isReady = false;
        }

        // 向窗口中添加新元素
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

        // 返回当前窗口的元素数量
        public int Count => _window.Count;

        // 检查窗口是否已填满
        public bool IsReady => _isReady;

        // 返回一个枚举器，用于迭代窗口
        public IEnumerator<T> GetEnumerator()
        {
            return _window.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public T Latest()
        {
            if (_window.Count == 0)
                throw new InvalidOperationException("The window is empty.");

            return _window.ToArray()[_window.Count - 1]; // Returns the most recent element (last in the queue)
        }

    }
}