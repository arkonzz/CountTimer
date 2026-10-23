using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Service.ServiceImpl;

namespace CountTimer.View
{
    /// <summary>
    /// 番茄钟页签（含事件倒计时整合）：
    /// 选中事件 = 关联番茄钟任务 + 自动开始该事件倒计时
    /// 布局全部在 OnLayout 动态计算，窗口缩放时自动居中、圆环自适应
    /// </summary>
    public class PomodoroControl : UserControl, AntdUI.IEventListener
    {
        private static readonly Color FocusRingColor = Color.FromArgb(228, 88, 53);
        private static readonly Color BreakRingColor = Color.FromArgb(103, 194, 58);
        private static readonly Color LongBreakRingColor = Color.FromArgb(64, 140, 255);
        private static readonly TimeSpan DuplicateNotificationWindow = TimeSpan.FromSeconds(10);

        // 设置卡片区（外层包装 + 内层卡片）总占用高度
        private const int SettingsWrapperHeight = 172;
        // 圆环下方元素：操作行(44) + 间距(10) + 事件行(44) + 间距(12) + 统计(26) + 余量(6)
        private const int BelowRingHeight = 44 + 10 + 44 + 12 + 26 + 6;

        private readonly PomodoroController controller;
        private readonly CountdownController _countdownController = new CountdownController();
        private readonly ExpiredEventCleanupWorker _cleanupWorker = new ExpiredEventCleanupWorker();
        private readonly ToDoThingService todoService = new ToDoThingServiceImpl();
        private readonly AppSettingsService settingsService = new AppSettingsService();
        private readonly FocusSessionService sessionService = new FocusSessionServiceImpl();
        private readonly Action<string, string> showBalloon;
        private AppSettings settings;
        private List<ToDoThing> toDoThings = [];

        // 事件倒计时通知去重
        private readonly HashSet<string> _notifiedEventOccurrences = [];
        private string? _lastNotificationText;
        private DateTime _lastNotificationTime = DateTime.MinValue;
        private bool _isRefreshingEvents = false;
        private bool _eventCountdownPaused = false;
        private EventEditControl? eventEditor;

        private readonly PomodoroRingControl ring = new PomodoroRingControl();
        private readonly AntdUI.Label lblPhase = new AntdUI.Label();
        private readonly AntdUI.Label lblStats = new AntdUI.Label();
        private readonly AntdUI.Button btnStart = new AntdUI.Button();
        private readonly AntdUI.Button btnSkip = new AntdUI.Button();
        private readonly AntdUI.Button btnReset = new AntdUI.Button();
        private readonly AntdUI.Select select_event = new AntdUI.Select();
        private readonly AntdUI.Button btnEventToggle = new AntdUI.Button();
        private readonly AntdUI.Button btnAddEvent = new AntdUI.Button();
        private readonly AntdUI.Button btnEditEvent = new AntdUI.Button();
        private readonly AntdUI.Button btnDelEvent = new AntdUI.Button();
        private readonly AntdUI.Label lblEvent = new AntdUI.Label();
        private readonly AntdUI.InputNumber numFocus = new AntdUI.InputNumber();
        private readonly AntdUI.InputNumber numShortBreak = new AntdUI.InputNumber();
        private readonly AntdUI.InputNumber numLongBreak = new AntdUI.InputNumber();
        private readonly AntdUI.InputNumber numRounds = new AntdUI.InputNumber();
        private readonly AntdUI.Switch switchAutoBreak = new AntdUI.Switch();
        private readonly AntdUI.Switch switchAutoFocus = new AntdUI.Switch();
        private readonly AntdUI.Switch switchSound = new AntdUI.Switch();
        private readonly AntdUI.Switch switchDark = new AntdUI.Switch();
        private readonly AntdUI.Button btnSaveSettings = new AntdUI.Button();
        private readonly AntdUI.Label lblSettingsTitle = new AntdUI.Label();

        private readonly System.Windows.Forms.Panel settingsWrapper = new System.Windows.Forms.Panel();
        private readonly CardPanel settingsCard = new CardPanel();
        private readonly List<(AntdUI.Label label, AntdUI.InputNumber input)> durationFields = [];
        private readonly List<(AntdUI.Switch toggle, AntdUI.Label label)> toggleFields = [];

        /// <summary>主题切换请求（由主窗体统一应用）</summary>
        public event Action<bool>? ThemeChanged;

        public PomodoroControl(Action<string, string> showBalloon)
        {
            this.showBalloon = showBalloon;
            settings = settingsService.Load();

            // 禁用字体自动缩放：AntdUI 容器会按字体度量对子控件做非等比缩放，把圆环纵向压扁
            AutoScaleMode = AutoScaleMode.None;

            BuildUi();
            this.AddListener();

            controller = new PomodoroController(settings);
            controller.Tick += Controller_Tick;
            controller.PhaseChanged += Controller_PhaseChanged;
            controller.PhaseCompleted += Controller_PhaseCompleted;
            controller.StateChanged += Controller_StateChanged;

            _countdownController.Tick += CountdownController_Tick;
            _countdownController.Completed += CountdownController_Completed;

            LoadSettingsToInputs();
            UpdateThemeColors();

            RefreshTasks();
            RefreshStats();
            SyncPhaseUi(controller.Remaining);
            UpdateStartButton();

            // 事件倒计时后台维护 + 初始化事件列表
            todoService.updateRegularEvent();
            _cleanupWorker.Start(RefreshEventsFromWorker, NotifyRegularEventFromWorker);
            InitSelectEvent();

            // 初始化完成后再挂开关事件，避免程序化赋值触发保存
            switchDark.CheckedChanged += switchDark_CheckedChanged;
        }

        #region 布局

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            BackColor = UiTheme.PageBg;

            // 阶段标题（颜色随阶段变化）
            lblPhase.Text = "专注 · 第 1 / 4 轮";
            lblPhase.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            lblPhase.ForeColor = UiTheme.FocusColor;
            lblPhase.Size = new Size(360, 32);
            lblPhase.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(lblPhase);

            ring.Size = new Size(250, 250);
            Controls.Add(ring);

            // 操作行：事件下拉（任务+倒计时）+ 跳过 + 开始/暂停 + 重置
            select_event.List = true;
            select_event.MaxCount = 6;
            select_event.PlaceholderText = "选择事件（任务 / 倒计时）";
            select_event.Size = new Size(260, 40);
            select_event.SelectedValueChanged += select_event_SelectedValueChanged;
            Controls.Add(select_event);

            btnSkip.Text = "跳过";
            btnSkip.Ghost = true;
            btnSkip.BorderWidth = 1F;
            btnSkip.Size = new Size(64, 44);
            btnSkip.Click += btnSkip_Click;
            Controls.Add(btnSkip);

            btnStart.Text = "开始专注";
            btnStart.Type = AntdUI.TTypeMini.Error;
            btnStart.Size = new Size(128, 44);
            btnStart.Click += btnStart_Click;
            Controls.Add(btnStart);

            btnReset.Text = "重置";
            btnReset.Ghost = true;
            btnReset.BorderWidth = 1F;
            btnReset.Size = new Size(64, 44);
            btnReset.Click += btnReset_Click;
            Controls.Add(btnReset);

            // 事件倒计时行：开关 + 管理 + 剩余时间
            btnEventToggle.Text = "倒计时";
            btnEventToggle.Ghost = true;
            btnEventToggle.BorderWidth = 1F;
            btnEventToggle.Size = new Size(96, 44);
            btnEventToggle.Click += btnEventToggle_Click;
            Controls.Add(btnEventToggle);

            btnAddEvent.Text = "新增";
            btnAddEvent.Type = AntdUI.TTypeMini.Primary;
            btnAddEvent.Size = new Size(56, 44);
            btnAddEvent.Click += btnAddEvent_Click;
            Controls.Add(btnAddEvent);

            btnEditEvent.Enabled = false;
            btnEditEvent.Text = "编辑";
            btnEditEvent.Ghost = true;
            btnEditEvent.BorderWidth = 1F;
            btnEditEvent.Size = new Size(56, 44);
            btnEditEvent.Click += btnEditEvent_Click;
            Controls.Add(btnEditEvent);

            btnDelEvent.Enabled = false;
            btnDelEvent.Text = "删除";
            btnDelEvent.Ghost = true;
            btnDelEvent.BorderWidth = 1F;
            btnDelEvent.Type = AntdUI.TTypeMini.Error;
            btnDelEvent.Size = new Size(56, 44);
            btnDelEvent.Click += btnDelEvent_Click;
            Controls.Add(btnDelEvent);

            lblEvent.Text = "选择事件后自动开始倒计时";
            lblEvent.Font = new Font("Microsoft YaHei UI", 10F);
            lblEvent.ForeColor = UiTheme.TextSub;
            lblEvent.Size = new Size(210, 44);
            lblEvent.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lblEvent);

            lblStats.Text = "今日 0 个番茄 · 0 分钟专注";
            lblStats.Font = new Font("Microsoft YaHei UI", 10.5F);
            lblStats.ForeColor = UiTheme.TextSub;
            lblStats.Size = new Size(360, 26);
            lblStats.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(lblStats);

            BuildSettingsArea();
        }

        private void BuildSettingsArea()
        {
            settingsWrapper.Dock = DockStyle.Bottom;
            settingsWrapper.Height = SettingsWrapperHeight;
            settingsWrapper.BackColor = UiTheme.PageBg;
            settingsWrapper.Padding = new Padding(20, 10, 20, 16);
            Controls.Add(settingsWrapper);
            settingsWrapper.BringToFront();

            settingsCard.Dock = DockStyle.Fill;
            settingsCard.Layout += SettingsCard_Layout;
            settingsWrapper.Controls.Add(settingsCard);

            lblSettingsTitle.Text = "设置";
            lblSettingsTitle.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            lblSettingsTitle.ForeColor = UiTheme.TextMain;
            lblSettingsTitle.Size = new Size(60, 24);
            lblSettingsTitle.TextAlign = ContentAlignment.MiddleLeft;
            settingsCard.Controls.Add(lblSettingsTitle);

            durationFields.AddRange([
                MakeDurationField("专注(分)", numFocus, 240),
                MakeDurationField("短休(分)", numShortBreak, 120),
                MakeDurationField("长休(分)", numLongBreak, 180),
                MakeDurationField("长休(轮)", numRounds, 12)
            ]);

            toggleFields.AddRange([
                MakeToggleField(switchAutoBreak, "自动休息"),
                MakeToggleField(switchAutoFocus, "自动专注"),
                MakeToggleField(switchSound, "提示音"),
                MakeToggleField(switchDark, "深色主题")
            ]);

            btnSaveSettings.Text = "保存设置";
            btnSaveSettings.Type = AntdUI.TTypeMini.Primary;
            btnSaveSettings.Size = new Size(84, 34);
            btnSaveSettings.Click += btnSaveSettings_Click;
            settingsCard.Controls.Add(btnSaveSettings);
        }

        private (AntdUI.Label, AntdUI.InputNumber) MakeDurationField(string text, AntdUI.InputNumber input, int max)
        {
            AntdUI.Label label = new AntdUI.Label
            {
                Text = text,
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = UiTheme.TextSub,
                Size = new Size(90, 18),
                TextAlign = ContentAlignment.MiddleCenter
            };
            settingsCard.Controls.Add(label);

            input.Minimum = 1;
            input.Maximum = max;
            input.Increment = 1;
            input.DecimalPlaces = 0;
            input.InterceptArrowKeys = true;
            input.Size = new Size(76, 36);
            settingsCard.Controls.Add(input);
            return (label, input);
        }

        private (AntdUI.Switch, AntdUI.Label) MakeToggleField(AntdUI.Switch toggle, string text)
        {
            AntdUI.Label label = new AntdUI.Label
            {
                Text = text,
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = UiTheme.TextSub,
                Size = new Size(64, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };
            settingsCard.Controls.Add(label);
            toggle.Size = new Size(44, 24);
            settingsCard.Controls.Add(toggle);
            return (toggle, label);
        }

        /// <summary>主区域布局：水平居中 + 垂直整体居中，圆环大小随可用高度自适应，坐标钳制在可视区内</summary>
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            int width = ClientSize.Width;
            int centerX = width / 2;
            int contentBottom = Math.Max(ClientSize.Height - SettingsWrapperHeight, 200);

            int headerHeight = lblPhase.Height + 12;
            int ringSize = Math.Clamp(Math.Min(width - 60, contentBottom - headerHeight - BelowRingHeight - 12), 120, 300);
            int contentHeight = headerHeight + ringSize + BelowRingHeight;
            int offsetY = Math.Max(6, (contentBottom - contentHeight) / 2);

            lblPhase.Location = new Point(Math.Max(4, centerX - lblPhase.Width / 2), offsetY);
            ring.Size = new Size(ringSize, ringSize);
            ring.Location = new Point(Math.Max(4, centerX - ringSize / 2), lblPhase.Bottom + 12);

            // 操作行：下拉(260) 跳过(64) 开始(128) 重置(64)，间距 10
            int y = ring.Bottom + 10;
            int x = Math.Max(4, centerX - (260 + 10 + 64 + 10 + 128 + 10 + 64) / 2);
            select_event.Location = new Point(x, y + 2);
            btnSkip.Location = new Point(x + 260 + 10, y);
            btnStart.Location = new Point(btnSkip.Right + 10, y);
            btnReset.Location = new Point(btnStart.Right + 10, y);

            // 事件倒计时行：倒计时(96) 新增(56) 编辑(56) 删除(56) + 剩余时间(210)，间距 10
            y = btnReset.Bottom + 10;
            int eventRowWidth = 96 + 10 + 56 + 10 + 56 + 10 + 56 + 10 + 210;
            x = Math.Max(4, centerX - eventRowWidth / 2);
            btnEventToggle.Location = new Point(x, y);
            btnAddEvent.Location = new Point(x + 96 + 10, y);
            btnEditEvent.Location = new Point(btnAddEvent.Right + 10, y);
            btnDelEvent.Location = new Point(btnEditEvent.Right + 10, y);
            lblEvent.Location = new Point(btnDelEvent.Right + 10, y);

            lblStats.Location = new Point(Math.Max(4, centerX - lblStats.Width / 2), y + 44 + 12);

            // 布局后强制全量重绘：控件"只移动不变尺寸"时 WinForms 会走位图平移而不重绘，
            // 圆环等自绘控件会留下移动残影（弧线碎片）
            Invalidate(true);
        }

        /// <summary>设置卡片内部布局：4 列网格</summary>
        private void SettingsCard_Layout(object? sender, LayoutEventArgs e)
        {
            int innerX = 20;
            int columnWidth = (settingsCard.Width - innerX * 2) / 4;

            lblSettingsTitle.Location = new Point(innerX, 10);
            btnSaveSettings.Location = new Point(settingsCard.Width - 20 - btnSaveSettings.Width, 6);

            for (int i = 0; i < durationFields.Count; i++)
            {
                int columnCenter = innerX + i * columnWidth + columnWidth / 2;
                var (label, input) = durationFields[i];
                label.Location = new Point(columnCenter - label.Width / 2, 42);
                input.Location = new Point(columnCenter - input.Width / 2, 64);
            }

            for (int i = 0; i < toggleFields.Count; i++)
            {
                int columnCenter = innerX + i * columnWidth + columnWidth / 2;
                var (toggle, label) = toggleFields[i];
                int groupWidth = toggle.Width + 8 + label.Width;
                int x = columnCenter - groupWidth / 2;
                toggle.Location = new Point(x, 114);
                label.Location = new Point(x + toggle.Width + 8, 114);
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            // 隐藏状态下窗口缩放不会触发本页布局，切回时强制重排
            if (Visible)
            {
                PerformLayout();
            }
        }

        #endregion

        #region 番茄钟交互

        private void btnStart_Click(object? sender, EventArgs e)
        {
            controller.StartOrPause();
        }

        private void btnSkip_Click(object? sender, EventArgs e)
        {
            controller.Skip();
        }

        private void btnReset_Click(object? sender, EventArgs e)
        {
            controller.Reset();
        }

        private void switchDark_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            settings.DarkMode = switchDark.Checked;
            settingsService.Save(settings);
            ThemeChanged?.Invoke(settings.DarkMode);
        }

        private void btnSaveSettings_Click(object? sender, EventArgs e)
        {
            settings.FocusMinutes = (int)numFocus.Value;
            settings.ShortBreakMinutes = (int)numShortBreak.Value;
            settings.LongBreakMinutes = (int)numLongBreak.Value;
            settings.RoundsBeforeLongBreak = (int)numRounds.Value;
            settings.AutoStartBreak = switchAutoBreak.Checked;
            settings.AutoStartFocus = switchAutoFocus.Checked;
            settings.SoundEnabled = switchSound.Checked;

            settingsService.Save(settings);
            controller.ApplySettings(settings);
            LoadSettingsToInputs();
            RefreshStats();
            AntdUI.Message.success(FindForm()!, "设置已保存，计时已重置", autoClose: 2);
        }

        private void Controller_Tick(TimeSpan remaining, PomodoroPhase phase, int round, int totalRounds)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => SyncPhaseUi(remaining));
                return;
            }

            SyncPhaseUi(remaining);
        }

        private void Controller_PhaseChanged()
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => SyncPhaseUi(controller.Remaining));
                return;
            }

            SyncPhaseUi(controller.Remaining);
        }

        private void Controller_StateChanged()
        {
            if (InvokeRequired)
            {
                BeginInvoke(UpdateStartButton);
                return;
            }

            UpdateStartButton();
        }

        private void Controller_PhaseCompleted(PomodoroPhase phase, int round)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => OnPhaseCompleted(phase));
                return;
            }

            OnPhaseCompleted(phase);
        }

        private void OnPhaseCompleted(PomodoroPhase phase)
        {
            if (settings.SoundEnabled)
            {
                System.Media.SystemSounds.Asterisk.Play();
            }

            switch (phase)
            {
                case PomodoroPhase.Focus:
                    sessionService.AddFocusSession(new FocusSession
                    {
                        sessionDate = DateTime.Today.ToString("yyyy-MM-dd"),
                        endTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        focusMinutes = settings.FocusMinutes
                    });
                    string taskName = (select_event.SelectedValue as ToDoThing)?.toDoInfo ?? string.Empty;
                    string taskSuffix = string.IsNullOrWhiteSpace(taskName) ? "" : $"\n任务：{taskName}";
                    showBalloon("专注完成 🍅", $"完成了 {settings.FocusMinutes} 分钟专注，休息一下吧！{taskSuffix}");
                    RefreshStats();
                    break;
                case PomodoroPhase.ShortBreak:
                    showBalloon("休息结束", "短休息结束，开始新的专注！");
                    break;
                case PomodoroPhase.LongBreak:
                    showBalloon("长休息结束", "充电完成，开始新一轮专注！");
                    break;
            }
        }

        private void SyncPhaseUi(TimeSpan remaining)
        {
            (string title, Color color, AntdUI.TTypeMini buttonType) = controller.Phase switch
            {
                PomodoroPhase.Focus => ("专注", UiTheme.FocusColor, AntdUI.TTypeMini.Error),
                PomodoroPhase.ShortBreak => ("短休息", UiTheme.BreakColor, AntdUI.TTypeMini.Success),
                PomodoroPhase.LongBreak => ("长休息", UiTheme.LongBreakColor, AntdUI.TTypeMini.Primary),
                _ => ("专注", UiTheme.FocusColor, AntdUI.TTypeMini.Error)
            };

            ring.TitleText = title;
            ring.RingColor = color;
            ring.TimeText = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
            double phaseSeconds = controller.PhaseDuration.TotalSeconds;
            ring.Progress = phaseSeconds > 0 ? (float)(remaining.TotalSeconds / phaseSeconds) : 0f;

            int displayRound = ((controller.RoundIndex - 1) % settings.RoundsBeforeLongBreak) + 1;
            lblPhase.Text = $"{title} · 第 {displayRound} / {settings.RoundsBeforeLongBreak} 轮";
            lblPhase.ForeColor = color;
            btnStart.Type = buttonType;
            UpdateStartButton();
        }

        private void UpdateStartButton()
        {
            btnStart.Text = controller.IsRunning
                ? "暂停"
                : (controller.Remaining < controller.PhaseDuration ? "继续" : (controller.Phase == PomodoroPhase.Focus ? "开始专注" : "开始休息"));
        }

        #endregion

        #region 事件倒计时（整合自原事件页签）

        private void select_event_SelectedValueChanged(object sender, ObjectNEventArgs e)
        {
            if (_isRefreshingEvents)
            {
                return;
            }

            if (select_event.SelectedValue is not ToDoThing)
            {
                btnEditEvent.Enabled = false;
                btnDelEvent.Enabled = false;
                _countdownController.Stop();
                lblEvent.Text = "选择事件后自动开始倒计时";
                btnEventToggle.Text = "倒计时";
                _eventCountdownPaused = false;
                return;
            }

            btnEditEvent.Enabled = true;
            btnDelEvent.Enabled = true;
            // 选中事件：自动开始倒计时
            StartEventCountdown();
        }

        private void StartEventCountdown()
        {
            if (select_event.SelectedValue is not ToDoThing toDoThing)
            {
                return;
            }

            if (!_countdownController.Start(toDoThing, out string? errorMessage))
            {
                lblEvent.Text = errorMessage ?? "倒计时启动失败";
                btnEventToggle.Text = "倒计时";
                _eventCountdownPaused = false;
                return;
            }

            _eventCountdownPaused = false;
            btnEventToggle.Text = "暂停倒计时";
        }

        private void btnEventToggle_Click(object? sender, EventArgs e)
        {
            if (select_event.SelectedValue is not ToDoThing)
            {
                AntdUI.Message.warn(FindForm()!, "请先选择事件", autoClose: 2);
                return;
            }

            if (_eventCountdownPaused)
            {
                StartEventCountdown();
            }
            else
            {
                _countdownController.Stop();
                _eventCountdownPaused = true;
                btnEventToggle.Text = "继续倒计时";
                lblEvent.Text = "倒计时已暂停";
            }
        }

        private void CountdownController_Tick(TimeSpan remaining, ToDoThing toDoThing)
        {
            lblEvent.Text = $"「{toDoThing.toDoInfo}」剩余 {FormatRemainingCompact(remaining)}";
        }

        private void CountdownController_Completed(ToDoThing toDoThing)
        {
            lblEvent.Text = $"「{toDoThing.toDoInfo}」时间到！";
            NotifyEventEnded(toDoThing);
        }

        private void NotifyEventEnded(ToDoThing toDoThing)
        {
            string occurrenceKey = GetEventOccurrenceKey(toDoThing);
            if (!_notifiedEventOccurrences.Add(occurrenceKey))
            {
                return;
            }

            ShowNotification(toDoThing.toDoInfo);
        }

        private static string GetEventOccurrenceKey(ToDoThing toDoThing)
        {
            string endTime = DateTime.TryParse(toDoThing.endTime, out DateTime parsedEndTime)
                ? parsedEndTime.ToString("yyyy-MM-dd HH:mm:ss")
                : toDoThing.endTime;

            return $"{toDoThing.Id}:{endTime}";
        }

        private void ShowNotification(string eventName)
        {
            string notificationText = string.IsNullOrWhiteSpace(eventName)
                ? "时间到！"
                : $"{eventName} 事件结束";
            DateTime now = DateTime.Now;
            if (_lastNotificationText == notificationText &&
                now - _lastNotificationTime < DuplicateNotificationWindow)
            {
                return;
            }

            _lastNotificationText = notificationText;
            _lastNotificationTime = now;
            showBalloon("倒计时结束", notificationText);
        }

        private void RefreshEventsFromWorker()
        {
            if (IsDisposed || Disposing || !select_event.IsHandleCreated)
            {
                return;
            }

            try
            {
                select_event.BeginInvoke((Action)InitSelectEvent);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void NotifyRegularEventFromWorker(ToDoThing toDoThing)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke((Action)(() =>
                {
                    NotifyEventEnded(toDoThing);
                }));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void InitSelectEvent()
        {
            _isRefreshingEvents = true;
            try
            {
                int? selectedId = (select_event.SelectedValue as ToDoThing)?.Id;
                select_event.Items.Clear();
                toDoThings = todoService.GetTodoList();

                select_event.Items.AddRange([.. toDoThings]);

                if (toDoThings.Count == 0)
                {
                    btnEditEvent.Enabled = false;
                    btnDelEvent.Enabled = false;
                    _countdownController.Stop();
                    lblEvent.Text = "暂无事件，点击新增创建";
                    return;
                }

                if (selectedId.HasValue)
                {
                    int selectedIndex = toDoThings.FindIndex(it => it.Id == selectedId.Value);
                    if (selectedIndex >= 0)
                    {
                        select_event.SelectedIndex = selectedIndex;
                        return;
                    }

                    btnEditEvent.Enabled = false;
                    btnDelEvent.Enabled = false;
                    _countdownController.Stop();
                    lblEvent.Text = "事件已结束";
                }
            }
            finally
            {
                _isRefreshingEvents = false;
            }
        }

        private void btnAddEvent_Click(object? sender, EventArgs e)
        {
            OpenEventEditor(false);
        }

        private void btnEditEvent_Click(object? sender, EventArgs e)
        {
            if (select_event.SelectedValue == null)
            {
                AntdUI.Message.warn(FindForm()!, "请先选择要编辑的事件", autoClose: 2);
                return;
            }

            OpenEventEditor(true);
        }

        /// <summary>在主窗体右侧打开内嵌抽屉编辑事件（新增/编辑共用）</summary>
        private void OpenEventEditor(bool editMode)
        {
            eventEditor ??= new EventEditControl();
            // 先取消订阅，避免重复订阅
            eventEditor.DataUpdated -= OnDataUpdated;
            eventEditor.DataUpdated += OnDataUpdated;

            if (editMode && select_event.SelectedValue is ToDoThing selected)
            {
                eventEditor.SetEditEvent(selected);
            }
            else
            {
                eventEditor.PrepareAddEvent();
            }

            AntdUI.Drawer.open(new AntdUI.Drawer.Config(FindForm()!, eventEditor)
            {
                Align = AntdUI.TAlignMini.Right,
                Mask = true,
                MaskClosable = true,
                Dispose = false
            });
        }

        private void btnDelEvent_Click(object? sender, EventArgs e)
        {
            if (select_event.SelectedValue == null)
            {
                AntdUI.Message.warn(FindForm()!, "请先选择要删除的事件", autoClose: 2);
                return;
            }

            ToDoThing selectedTodo = (ToDoThing)select_event.SelectedValue;
            var parentForm = FindForm()!;

            // 使用 Modal.Config 配置对话框
            var config = new AntdUI.Modal.Config(parentForm, "确认删除", $"确定要删除事件 \"{selectedTodo.toDoInfo}\" 吗？")
            {
                OnOk = (cfg) => {
                    todoService.deleteById(selectedTodo.Id);
                    AntdUI.Message.success(parentForm, "删除成功", autoClose: 2);
                    _countdownController.Stop();
                    lblEvent.Text = "事件已删除";
                    btnEventToggle.Text = "倒计时";
                    _eventCountdownPaused = false;
                    InitSelectEvent();
                    return true; // 返回 true 关闭对话框
                }
            };
            AntdUI.Modal.open(config);
        }

        private void OnDataUpdated()
        {
            InitSelectEvent();
            _cleanupWorker.Wake();
        }

        private static string FormatRemainingCompact(TimeSpan remaining)
        {
            if (remaining.TotalSeconds <= 0)
            {
                return "00:00:00";
            }

            return remaining.Days > 0
                ? $"{remaining.Days}天 {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
                : $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }

        #endregion

        #region 数据与主题

        private void LoadSettingsToInputs()
        {
            numFocus.Value = settings.FocusMinutes;
            numShortBreak.Value = settings.ShortBreakMinutes;
            numLongBreak.Value = settings.LongBreakMinutes;
            numRounds.Value = settings.RoundsBeforeLongBreak;
            switchAutoBreak.Checked = settings.AutoStartBreak;
            switchAutoFocus.Checked = settings.AutoStartFocus;
            switchSound.Checked = settings.SoundEnabled;
            switchDark.Checked = settings.DarkMode;
        }

        /// <summary>主题事件：AntdUI 明暗切换时刷新本控件内显式指定颜色的元素（卡片/圆环自行监听）</summary>
        public void HandleEvent(AntdUI.EventType id, object? tag)
        {
            if (id == AntdUI.EventType.THEME)
            {
                UpdateThemeColors();
            }
        }

        event EventHandler? AntdUI.IEventListener.Disposed
        {
            add => Disposed += value;
            remove => Disposed -= value;
        }

        int AntdUI.IEventListener.GetHashCode() => base.GetHashCode();

        private void UpdateThemeColors()
        {
            // 页面与内嵌面板统一实色背景（避免透明在主题切换时产生残影）
            BackColor = UiTheme.PageBg;
            settingsWrapper.BackColor = UiTheme.PageBg;
            lblStats.ForeColor = UiTheme.TextSub;
            lblEvent.ForeColor = UiTheme.TextSub;
            lblSettingsTitle.ForeColor = UiTheme.TextMain;
            foreach (var (label, _) in durationFields)
            {
                label.ForeColor = UiTheme.TextSub;
            }

            foreach (var (_, label) in toggleFields)
            {
                label.ForeColor = UiTheme.TextSub;
            }
        }

        #region E2E 自测（仅 CT_E2E=1 时由主窗体调用）

        internal bool E2EIsRunning => controller.IsRunning;

        /// <summary>圆环控件实际尺寸（验证未被非等比缩放）</summary>
        internal System.Drawing.Size E2ERingSize => ring.Size;

        internal void E2EStart() => controller.Start();

        internal void E2EOpenDrawer() => OpenEventEditor(false);

        internal void E2ECloseDrawer() => eventEditor?.CloseDrawer();

        #endregion

        /// <summary>
        /// 刷新可关联的事件列表
        /// </summary>
        public void RefreshTasks()
        {
            int? selectedId = (select_event.SelectedValue as ToDoThing)?.Id;
            toDoThings = todoService.GetTodoList();
            select_event.Items.Clear();
            select_event.Items.AddRange([.. toDoThings]);

            if (selectedId.HasValue)
            {
                int index = toDoThings.FindIndex(thing => thing.Id == selectedId.Value);
                if (index >= 0)
                {
                    select_event.SelectedIndex = index;
                }
            }
        }

        private void RefreshStats()
        {
            int count = sessionService.GetTodayCount();
            int minutes = sessionService.GetTodayMinutes();
            lblStats.Text = $"今日 {count} 个番茄 · {minutes} 分钟专注";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                controller.Dispose();
                _countdownController.Dispose();
                _cleanupWorker.Dispose();
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
