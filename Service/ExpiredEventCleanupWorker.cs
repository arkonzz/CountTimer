using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CountTimer.Model;
using CountTimer.Service.ServiceImpl;

namespace CountTimer.Service
{
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
        /// 后台循环：到点提醒、跨天滚动循环事件、清理过期的一次性事件。
        /// eventDue：到点事件（循环事件一律提醒；一次性事件仅在倒计时正跟随时提醒）。
        /// isCountingDown：倒计时是否正在跟随该事件
        /// </summary>
        public void Start(Action eventsChanged, Action<ToDoThing>? eventDue = null, Func<ToDoThing, bool>? isCountingDown = null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        DateTime now = DateTime.Now;
                        NotifyDueEvents(now, eventDue, isCountingDown);

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

        /// <summary>
        /// 到点事件是否由后台提醒：循环事件一律提醒；一次性事件只有倒计时正在跟随时才提醒。
        /// 后者是兜底——它本该由倒计时自己提醒，但清理可能先一步把它删掉并停掉倒计时
        /// </summary>
        internal static bool ShouldNotify(ToDoThing toDoThing, DateTime now, Func<ToDoThing, bool>? isCountingDown)
        {
            if (!DateTime.TryParse(toDoThing.endTime, out DateTime endTime) || endTime > now)
            {
                return false;
            }

            return toDoThing.isRegular || isCountingDown?.Invoke(toDoThing) == true;
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

        private TimeSpan GetNextDelay(DateTime now)
        {
            TimeSpan nextDelay = CleanupInterval;
            foreach (ToDoThing toDoThing in _service.GetTodoList())
            {
                if (!toDoThing.isRegular || !DateTime.TryParse(toDoThing.endTime, out DateTime endTime))
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

        private void NotifyDueEvents(DateTime now, Action<ToDoThing>? eventDue, Func<ToDoThing, bool>? isCountingDown)
        {
            // 先提醒再滚动/清理，避免事件先被删掉导致提醒丢失
            foreach (ToDoThing toDoThing in _service.GetTodoList())
            {
                if (!ShouldNotify(toDoThing, now, isCountingDown))
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
