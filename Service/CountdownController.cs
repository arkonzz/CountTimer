using System;
using System.Windows.Forms;
using CountTimer.Model;
using Timer = System.Windows.Forms.Timer;

namespace CountTimer.Service
{
    public sealed class CountdownController : IDisposable
    {
        private const int CountdownIntervalMilliseconds = 1000;

        private readonly Timer _timer = new Timer { Interval = CountdownIntervalMilliseconds };
        private DateTime _targetTime;
        private ToDoThing? _currentTodo;

        public CountdownController()
        {
            _timer.Tick += Timer_Tick;
        }

        public event Action<TimeSpan, ToDoThing>? Tick;
        public event Action<ToDoThing>? Completed;

        public bool Start(ToDoThing toDoThing, out string? errorMessage)
        {
            errorMessage = null;

            if (!DateTime.TryParse(toDoThing.endTime, out _targetTime))
            {
                Stop();
                errorMessage = "事件时间格式异常";
                return false;
            }

            if (_targetTime <= DateTime.Now)
            {
                Stop();
                errorMessage = "事件已结束";
                return false;
            }

            _currentTodo = toDoThing;
            _timer.Start();
            return true;
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public static string FormatTimeSpan(TimeSpan remaining, bool isRegular)
        {
            if (isRegular)
            {
                return remaining.TotalSeconds > 0
                    ? $"{remaining.Hours:D2} 时 {remaining.Minutes:D2} 分 {remaining.Seconds:D2} 秒"
                    : "00 时 00 分 00 秒";
            }

            return remaining.TotalSeconds > 0
                ? $"{remaining.Days:D2} 天 {remaining.Hours:D2} 时 {remaining.Minutes:D2} 分 {remaining.Seconds:D2} 秒"
                : "00 天 00 时 00 分 00 秒";
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
