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

        public void Start(Action eventsChanged, Action<ToDoThing>? regularEventDue = null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        DateTime now = DateTime.Now;
                        NotifyDueRegularEvents(now, regularEventDue);

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

        private void NotifyDueRegularEvents(DateTime now, Action<ToDoThing>? regularEventDue)
        {
            List<ToDoThing> dueRegularEvents = _service.GetTodoList().FindAll(it =>
                it.isRegular &&
                DateTime.TryParse(it.endTime, out DateTime endTime) &&
                endTime <= now);

            foreach (ToDoThing toDoThing in dueRegularEvents)
            {
                regularEventDue?.Invoke(toDoThing);
                toDoThing.endTime = GetNextRegularEndTime(toDoThing, now);
                _service.UpdateToDoThing(toDoThing);
            }
        }

        private static string GetNextRegularEndTime(ToDoThing toDoThing, DateTime now)
        {
            if (!DateTime.TryParse(toDoThing.endTime, out DateTime endTime))
            {
                return now.AddDays(1).ToString(DateTimeFormat);
            }

            DateTime nextOccurrence = now.Date.Add(endTime.TimeOfDay);
            if (nextOccurrence <= now)
            {
                nextOccurrence = nextOccurrence.AddDays(1);
            }

            return nextOccurrence.ToString(DateTimeFormat);
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
