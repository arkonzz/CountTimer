using System;
using System.Windows.Forms;
using CountTimer.Model;
using Timer = System.Windows.Forms.Timer;

namespace CountTimer.Service
{
    /// <summary>事件倒计时启动结果（文案由 UI 层按当前语言映射）</summary>
    public enum CountdownStartStatus
    {
        Started,
        InvalidTime,
        AlreadyEnded
    }

    public sealed class CountdownController : IDisposable
    {
        private const int CountdownIntervalMilliseconds = 1000;

        private readonly Timer _timer = new Timer { Interval = CountdownIntervalMilliseconds };
        private DateTime _targetTime;
        private ToDoThing? _currentTodo;
        private volatile int _currentId;

        /// <summary>倒计时正在跟随的事件 Id，0 表示未在计时。后台线程也会读，所以用 volatile</summary>
        public int CurrentId => _currentId;

        public CountdownController()
        {
            _timer.Tick += Timer_Tick;
        }

        public event Action<TimeSpan, ToDoThing>? Tick;
        public event Action<ToDoThing>? Completed;

        public CountdownStartStatus Start(ToDoThing toDoThing)
        {
            if (!DateTime.TryParse(toDoThing.endTime, out _targetTime))
            {
                Stop();
                return CountdownStartStatus.InvalidTime;
            }

            if (_targetTime <= DateTime.Now)
            {
                Stop();
                return CountdownStartStatus.AlreadyEnded;
            }

            _currentTodo = toDoThing;
            _currentId = toDoThing.Id;
            _timer.Start();
            return CountdownStartStatus.Started;
        }

        public void Stop()
        {
            _timer.Stop();
            _currentId = 0;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_currentTodo == null)
            {
                Stop();
                return;
            }

            TimeSpan remaining = _targetTime - DateTime.Now;
            Tick?.Invoke(remaining, _currentTodo);

            if (remaining.TotalSeconds > 0)
            {
                return;
            }

            Stop();
            Completed?.Invoke(_currentTodo);
        }

        public void Dispose()
        {
            _timer.Tick -= Timer_Tick;
            _timer.Dispose();
        }
    }
}
