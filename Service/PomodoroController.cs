using System;
using System.Windows.Forms;
using CountTimer.Model;
using Timer = System.Windows.Forms.Timer;

namespace CountTimer.Service
{
    public enum PomodoroPhase
    {
        /// <summary>专注中</summary>
        Focus,
        /// <summary>短休息</summary>
        ShortBreak,
        /// <summary>长休息</summary>
        LongBreak
    }

    /// <summary>
    /// 番茄钟状态机：专注 → 短休 → 专注 → …（每 N 轮插入长休）
    /// </summary>
    public sealed class PomodoroController : IDisposable
    {
        private const int TickIntervalMilliseconds = 250;

        private readonly Timer _timer = new Timer { Interval = TickIntervalMilliseconds };
        private AppSettings _settings;
        private DateTime? _endAt;
        private TimeSpan _remaining = TimeSpan.Zero;

        public PomodoroController(AppSettings settings)
        {
            _settings = settings;
            _timer.Tick += Timer_Tick;
            Reset();
        }

        public event Action<TimeSpan, PomodoroPhase, int, int>? Tick;
        public event Action<PomodoroPhase, int>? PhaseCompleted;
        public event Action? StateChanged;
        public event Action? PhaseChanged;

        /// <summary>当前阶段</summary>
        public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Focus;

        /// <summary>当前是第几轮专注（从 1 开始）</summary>
        public int RoundIndex { get; private set; } = 1;

        public bool IsRunning => _timer.Enabled;

        /// <summary>当前阶段的总时长</summary>
        public TimeSpan PhaseDuration => GetPhaseDuration(Phase);

        /// <summary>当前阶段剩余时间</summary>
        public TimeSpan Remaining => IsRunning && _endAt.HasValue ? _endAt.Value - DateTime.Now : _remaining;

        public void StartOrPause()
        {
            if (IsRunning)
            {
                Pause();
            }
            else
            {
                Start();
            }
        }

        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            if (_remaining <= TimeSpan.Zero)
            {
                _remaining = PhaseDuration;
            }

            _endAt = DateTime.Now + _remaining;
            _timer.Start();
            StateChanged?.Invoke();
        }

        public void Pause()
        {
            if (!IsRunning)
            {
                return;
            }

            _remaining = Remaining;
            _endAt = null;
            _timer.Stop();
            StateChanged?.Invoke();
        }

        /// <summary>跳过当前阶段，直接进入下一阶段</summary>
        public void Skip()
        {
            Advance(false);
        }

        /// <summary>重置回第 1 轮专注并停止</summary>
        public void Reset()
        {
            _timer.Stop();
            _endAt = null;
            Phase = PomodoroPhase.Focus;
            RoundIndex = 1;
            _remaining = GetPhaseDuration(PomodoroPhase.Focus);
            StateChanged?.Invoke();
            PhaseChanged?.Invoke();
            Tick?.Invoke(_remaining, Phase, RoundIndex, _settings.RoundsBeforeLongBreak);
        }

        /// <summary>应用新配置（停止计时并按新时长重置）</summary>
        public void ApplySettings(AppSettings settings)
        {
            _settings = settings;
            Reset();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_endAt.HasValue)
            {
                _timer.Stop();
                return;
            }

            TimeSpan remaining = _endAt.Value - DateTime.Now;
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            Tick?.Invoke(remaining, Phase, RoundIndex, _settings.RoundsBeforeLongBreak);

            if (remaining > TimeSpan.Zero)
            {
                return;
            }

            _timer.Stop();
            PhaseCompleted?.Invoke(Phase, RoundIndex);
            Advance(shouldAutoStart: Phase is PomodoroPhase.Focus
                ? _settings.AutoStartBreak
                : _settings.AutoStartFocus,
                completedPhase: Phase);
        }

        private void Advance(bool shouldAutoStart)
        {
            Advance(shouldAutoStart, Phase);
        }

        private void Advance(bool shouldAutoStart, PomodoroPhase completedPhase)
        {
            switch (completedPhase)
            {
                case PomodoroPhase.Focus:
                    Phase = RoundIndex % _settings.RoundsBeforeLongBreak == 0
                        ? PomodoroPhase.LongBreak
                        : PomodoroPhase.ShortBreak;
                    break;
                case PomodoroPhase.ShortBreak:
                case PomodoroPhase.LongBreak:
                    RoundIndex++;
                    Phase = PomodoroPhase.Focus;
                    break;
            }

            _endAt = null;
            _remaining = PhaseDuration;
            PhaseChanged?.Invoke();
            Tick?.Invoke(_remaining, Phase, RoundIndex, _settings.RoundsBeforeLongBreak);

            if (shouldAutoStart)
            {
                Start();
            }
            else
            {
                StateChanged?.Invoke();
            }
        }

        private TimeSpan GetPhaseDuration(PomodoroPhase phase) => phase switch
        {
            PomodoroPhase.Focus => TimeSpan.FromMinutes(_settings.FocusMinutes),
            PomodoroPhase.ShortBreak => TimeSpan.FromMinutes(_settings.ShortBreakMinutes),
            PomodoroPhase.LongBreak => TimeSpan.FromMinutes(_settings.LongBreakMinutes),
            _ => TimeSpan.Zero
        };

        public void Dispose()
        {
            _timer.Tick -= Timer_Tick;
            _timer.Dispose();
        }
    }
}
