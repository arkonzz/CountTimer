using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CountTimer.Model;
using CountTimer.Service.ServiceImpl;

namespace CountTimer.Service
{
    /// <summary>事件的后台维护（倒数日页启动）：到点提醒、把过去的循环事件滚到下一次、清理过期的一次性事件</summary>
    public sealed class ExpiredEventCleanupWorker : IDisposable
    {
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MinimumDelay = TimeSpan.FromMilliseconds(250);
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly SemaphoreSlim _wakeSignal = new SemaphoreSlim(0, 1);
        private readonly ToDoThingService _service;
        private DateTime _lastRunTime = DateTime.Now;

        public ExpiredEventCleanupWorker(ToDoThingService? service = null)
        {
            _service = service ?? new ToDoThingServiceImpl();
        }

        /// <summary>
        /// 后台循环：到点提醒、滚动循环事件、清理过期的一次性事件（提醒完随即清理）。
        /// eventDue：到点的事件，循环的和一次性的都提醒
        /// </summary>
        public void Start(Action eventsChanged, Action<ToDoThing>? eventDue = null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        DateTime now = DateTime.Now;
                        NotifyDueEvents(now, eventDue);

                        if (SpanNewDay(_lastRunTime))
                        {
                            _service.updateRegularEvent();
                        }

                        _service.deleteByTime(now);
                        eventsChanged();
                        _lastRunTime = now;

                        await WaitForNextRun(GetNextDelay(now), _cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }, _cts.Token);
        }

        /// <summary>是否到点：时间合法且不晚于 now</summary>
        internal static bool ShouldNotify(ToDoThing toDoThing, DateTime now)
        {
            return DateTime.TryParse(toDoThing.endTime, out DateTime endTime) && endTime <= now;
        }

        public void Wake()
        {
            try
            {
                if (_wakeSignal.CurrentCount == 0)
                {
                    _wakeSignal.Release();
                }
            }
            catch (SemaphoreFullException)
            {
            }
        }

        private async Task WaitForNextRun(TimeSpan delay, CancellationToken token)
        {
            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            await _wakeSignal.WaitAsync(delay, token);
        }

        /// <summary>睡到最近一个事件到点，最多 30 秒（保证至少每 30 秒检查一遍）；已到点的事件在这一轮就被提醒、滚动或清理，不会让循环空转</summary>
        private TimeSpan GetNextDelay(DateTime now)
        {
            TimeSpan nextDelay = CleanupInterval;
            foreach (ToDoThing toDoThing in _service.GetTodoList())
            {
                if (!DateTime.TryParse(toDoThing.endTime, out DateTime endTime))
                {
                    continue;
                }

                TimeSpan delay = endTime - now;
                if (delay < nextDelay)
                {
                    nextDelay = delay;
                }
            }

            if (nextDelay <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            return nextDelay < MinimumDelay ? MinimumDelay : nextDelay;
        }

        private void NotifyDueEvents(DateTime now, Action<ToDoThing>? eventDue)
        {
            // 先提醒再滚动/清理，避免事件先被删掉导致提醒丢失
            foreach (ToDoThing toDoThing in _service.GetTodoList())
            {
                if (!ShouldNotify(toDoThing, now))
                {
                    continue;
                }

                eventDue?.Invoke(toDoThing);
                if (toDoThing.isRegular)
                {
                    toDoThing.endTime = NextOccurrence(toDoThing, now);
                    _service.UpdateToDoThing(toDoThing);
                }
            }
        }

        private static string NextOccurrence(ToDoThing toDoThing, DateTime now)
        {
            if (!DateTime.TryParse(toDoThing.endTime, out DateTime dueTime))
            {
                return now.AddDays(1).ToString(DateTimeFormat);
            }

            return RepeatRule.Next(toDoThing.Kind, toDoThing.repeatDay, dueTime.TimeOfDay, now).ToString(DateTimeFormat);
        }

        private static bool SpanNewDay(DateTime lastRunTime)
        {
            return DateTime.Today > lastRunTime.Date;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _wakeSignal.Dispose();
            _cts.Dispose();
        }
    }
}
