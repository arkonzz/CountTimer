using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Service.ServiceImpl;
using CountTimer.Share;

namespace CountTimer.View
{
    /// <summary>
    /// 番茄钟页签：专注 / 休息计时、统计、设置（事件倒计时在倒数日页）。
    /// 布局全部在 OnLayout 动态计算，窗口缩放时自动居中、圆环自适应
    /// </summary>
    public class PomodoroControl : UserControl, AntdUI.IEventListener
    {
        private static readonly Color FocusRingColor = Color.FromArgb(228, 88, 53);
        private static readonly Color BreakRingColor = Color.FromArgb(103, 194, 58);
        private static readonly Color LongBreakRingColor = Color.FromArgb(64, 140, 255);

        // 设置卡片区（外层包装 + 内层卡片）总占用高度
        private const int SettingsWrapperHeight = 214;
        // 圆环下方元素：操作行(44) + 间距(12) + 统计(26) + 余量(6)
        private const int BelowRingHeight = 44 + 12 + 26 + 6;

        private readonly PomodoroController controller;
        private readonly AppSettingsService settingsService = new AppSettingsService();
        private readonly FocusSessionService sessionService = new FocusSessionServiceImpl();
        private readonly Action<string, string> showBalloon;
        // 与主窗体共用同一个配置对象：两边都会保存，各持一份会互相覆盖
        private AppSettings settings;

        private readonly PomodoroRingControl ring = new PomodoroRingControl();
        private readonly AntdUI.Label lblPhase = new AntdUI.Label();
        private readonly AntdUI.Label lblStats = new AntdUI.Label();
        private readonly AntdUI.Button btnStart = new AntdUI.Button();
        private readonly AntdUI.Button btnSkip = new AntdUI.Button();
        private readonly AntdUI.Button btnReset = new AntdUI.Button();
        // 统计行：近 7 天趋势小图 + 今日统计文字（悬停小图时文字改为显示那一天的数值）
        private readonly FocusTrendControl trend = new();
        private readonly int[] weekCounts = new int[FocusTrendControl.Days];
        private string statsText = string.Empty;
        // 统计行文字区的宽度：取今日统计和各天悬停文案里最宽的一句，一次定好。
        // 悬停只换文字不重排——文字一变长短，居中的整行就跟着挪，小图会从鼠标底下跳走
        private int statsLabelWidth;
        // 统计行的纵坐标由主区域布局算出，文字内容变化重排时复用
        private int statsRowY;
        // 统计是哪一天算的：程序常驻托盘，跨天后要重算，否则"今日"一直显示昨天的数
        private DateTime statsDate = DateTime.MinValue;
        private readonly System.Windows.Forms.Timer dayWatcher = new() { Interval = 30_000 };
        private readonly AntdUI.InputNumber numFocus = new AntdUI.InputNumber();
        private readonly AntdUI.InputNumber numShortBreak = new AntdUI.InputNumber();
        private readonly AntdUI.InputNumber numLongBreak = new AntdUI.InputNumber();
        private readonly AntdUI.InputNumber numRounds = new AntdUI.InputNumber();
        private readonly AntdUI.Switch switchAutoBreak = new AntdUI.Switch();
        private readonly AntdUI.Switch switchAutoFocus = new AntdUI.Switch();
        private readonly AntdUI.Switch switchSound = new AntdUI.Switch();
        private readonly AntdUI.Switch switchDark = new AntdUI.Switch();
        private readonly AntdUI.Switch switchTopMost = new AntdUI.Switch();
        private readonly AntdUI.Switch switchAutostart = new AntdUI.Switch();
        private readonly AntdUI.Switch switchTrayTime = new AntdUI.Switch();
        private readonly AntdUI.Button btnSaveSettings = new AntdUI.Button();
        private readonly AntdUI.Label lblSettingsTitle = new AntdUI.Label();

        private readonly System.Windows.Forms.Panel settingsWrapper = new System.Windows.Forms.Panel();
        private readonly CardPanel settingsCard = new CardPanel();
        private readonly AntdUI.Label lblLanguage = new AntdUI.Label();
        private readonly AntdUI.Select select_lang = new AntdUI.Select();
        private readonly AntdUI.Label lblHotkey = new AntdUI.Label();
        private readonly AntdUI.Button btnHotkey = new AntdUI.Button();
        private readonly List<(AntdUI.Label label, AntdUI.InputNumber input)> durationFields = [];
        private readonly List<(AntdUI.Switch toggle, AntdUI.Label label)> toggleFields = [];

        /// <summary>主题切换请求（由主窗体统一应用）</summary>
        public event Action<bool>? ThemeChanged;

        /// <summary>托盘状态变化（界面线程）：剩余时间、是否计时中、是否已有进行中的阶段、阶段名（已按当前语言）</summary>
        public event Action<TimeSpan, bool, bool, string>? TrayStateChanged;

        /// <summary>应用级设置（置顶 / 自启 / 托盘时间 / 快捷键等）已保存，由主窗体统一应用</summary>
        public event Action? AppSettingsChanged;

        public PomodoroControl(Action<string, string> showBalloon, AppSettings sharedSettings)
        {
            this.showBalloon = showBalloon;
            settings = sharedSettings;

            // 禁用字体自动缩放：AntdUI 容器会按字体度量对子控件做非等比缩放，把圆环纵向压扁
            AutoScaleMode = AutoScaleMode.None;

            BuildUi();
            this.AddListener();

            controller = new PomodoroController(settings);
            controller.Tick += Controller_Tick;
            controller.PhaseChanged += Controller_PhaseChanged;
            controller.PhaseCompleted += Controller_PhaseCompleted;
            controller.StateChanged += Controller_StateChanged;

            LoadSettingsToInputs();
            UpdateThemeColors();

            RefreshStats();
            SyncPhaseUi(controller.Remaining);
            UpdateStartButton();

            // 初始化完成后再挂开关事件，避免程序化赋值触发保存
            switchDark.CheckedChanged += switchDark_CheckedChanged;
            switchTopMost.CheckedChanged += switchTopMost_CheckedChanged;
            switchAutostart.CheckedChanged += switchAutostart_CheckedChanged;
            switchTrayTime.CheckedChanged += switchTrayTime_CheckedChanged;

            // 跨天（含挂起唤醒）后重算统计
            dayWatcher.Tick += (s, e) =>
            {
                if (Visible)
                {
                    RefreshStatsIfNewDay();
                }
            };
            dayWatcher.Start();
        }

        #region 布局

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            BackColor = UiTheme.PageBg;

            // 阶段标题（颜色随阶段变化）——ApplyLanguage 后由 SyncPhaseUi 按当前阶段重写
            lblPhase.Text = I18n.F("专注 · 第 {0} / {1} 轮", "Focus · Round {0} / {1}", 1, settings.RoundsBeforeLongBreak);
            lblPhase.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            lblPhase.ForeColor = UiTheme.FocusColor;
            lblPhase.Size = new Size(360, 32);
            lblPhase.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(lblPhase);

            ring.Size = new Size(250, 250);
            Controls.Add(ring);

            // 操作行：跳过 + 开始/暂停 + 重置
            btnSkip.Text = I18n.T("跳过", "Skip");
            btnSkip.Ghost = true;
            btnSkip.BorderWidth = 1F;
            btnSkip.Size = new Size(64, 44);
            btnSkip.Click += btnSkip_Click;
            Controls.Add(btnSkip);

            btnStart.Text = I18n.T("开始专注", "Start focus");
            btnStart.Type = AntdUI.TTypeMini.Error;
            btnStart.Size = new Size(128, 44);
            btnStart.Click += btnStart_Click;
            Controls.Add(btnStart);

            btnReset.Text = I18n.T("重置", "Reset");
            btnReset.Ghost = true;
            btnReset.BorderWidth = 1F;
            btnReset.Size = new Size(64, 44);
            btnReset.Click += btnReset_Click;
            Controls.Add(btnReset);

            lblStats.Text = I18n.T("今日 0 个番茄 · 0 分钟专注", "0 pomodoros today · 0 min focused");
            lblStats.Font = new Font("Microsoft YaHei UI", 10.5F);
            lblStats.ForeColor = UiTheme.TextSub;
            lblStats.Size = new Size(360, 26);
            lblStats.TextAlign = ContentAlignment.MiddleLeft;
            // 单行显示：宽度按实测文字给足，任何情况下都不能折行把这一行撑高
            lblStats.TextMultiLine = false;
            Controls.Add(lblStats);
            trend.HoverChanged += Trend_HoverChanged;
            Controls.Add(trend);

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

            lblSettingsTitle.Text = I18n.T("设置", "Settings");
            lblSettingsTitle.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            lblSettingsTitle.ForeColor = UiTheme.TextMain;
            lblSettingsTitle.Size = new Size(60, 24);
            lblSettingsTitle.TextAlign = ContentAlignment.MiddleLeft;
            settingsCard.Controls.Add(lblSettingsTitle);

            // 语言切换（标题右侧；每种语言用自己的文字显示，不随语言翻译）
            lblLanguage.Text = I18n.T("语言", "Language");
            lblLanguage.Font = new Font("Microsoft YaHei UI", 9.5F);
            lblLanguage.ForeColor = UiTheme.TextSub;
            lblLanguage.Size = new Size(48, 24);
            lblLanguage.TextAlign = ContentAlignment.MiddleLeft;
            settingsCard.Controls.Add(lblLanguage);

            select_lang.Items.AddRange(new object[] { "中文", "English" });
            select_lang.List = true;
            select_lang.Size = new Size(104, 30);
            select_lang.SelectedIndex = I18n.IsEnglish ? 1 : 0;
            // 初始化完成后再挂事件，避免程序化赋值触发切换（同深色主题开关的做法）
            select_lang.SelectedValueChanged += select_lang_SelectedValueChanged;
            settingsCard.Controls.Add(select_lang);

            durationFields.AddRange([
                MakeDurationField(I18n.T("专注(分)", "Focus (min)"), numFocus, 240),
                MakeDurationField(I18n.T("短休(分)", "Short (min)"), numShortBreak, 120),
                MakeDurationField(I18n.T("长休(分)", "Long (min)"), numLongBreak, 180),
                MakeDurationField(I18n.T("长休(轮)", "Rounds"), numRounds, 12)
            ]);

            toggleFields.AddRange([
                MakeToggleField(switchAutoBreak, I18n.T("自动休息", "Auto break")),
                MakeToggleField(switchAutoFocus, I18n.T("自动专注", "Auto focus")),
                MakeToggleField(switchSound, I18n.T("提示音", "Sound")),
                MakeToggleField(switchDark, I18n.T("深色主题", "Dark mode")),
                // 应用级开关：改动即时生效并保存
                MakeToggleField(switchTopMost, I18n.T("窗口置顶", "Always on top")),
                MakeToggleField(switchAutostart, I18n.T("开机自启", "Auto start")),
                MakeToggleField(switchTrayTime, I18n.T("托盘时间", "Tray time"))
            ]);

            // 全局快捷键：按钮上显示当前组合，点击后按下新的组合（Esc 取消，Backspace/Delete 清除）
            lblHotkey.Text = I18n.T("快捷键", "Hotkey");
            lblHotkey.Font = new Font("Microsoft YaHei UI", 9.5F);
            lblHotkey.ForeColor = UiTheme.TextSub;
            lblHotkey.Size = new Size(56, 24);
            lblHotkey.TextAlign = ContentAlignment.MiddleLeft;
            settingsCard.Controls.Add(lblHotkey);

            btnHotkey.Ghost = true;
            btnHotkey.BorderWidth = 1F;
            btnHotkey.Size = new Size(110, 30);
            btnHotkey.Click += btnHotkey_Click;
            settingsCard.Controls.Add(btnHotkey);
            RefreshHotkeyText();

            btnSaveSettings.Text = I18n.T("保存设置", "Save");
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

            // 操作行：跳过(64) 开始(128) 重置(64)，间距 10
            int y = ring.Bottom + 10;
            int x = Math.Max(4, centerX - (64 + 10 + 128 + 10 + 64) / 2);
            btnSkip.Location = new Point(x, y);
            btnStart.Location = new Point(btnSkip.Right + 10, y);
            btnReset.Location = new Point(btnStart.Right + 10, y);

            statsRowY = btnReset.Bottom + 12;
            LayoutStats(centerX, statsRowY);

            // 布局后强制全量重绘：控件"只移动不变尺寸"时 WinForms 会走位图平移而不重绘，
            // 圆环等自绘控件会留下移动残影（弧线碎片）
            Invalidate(true);
        }

        /// <summary>统计行：趋势小图 + 文字，整体水平居中；文字区宽度固定（见 statsLabelWidth）</summary>
        private void LayoutStats(int centerX, int y)
        {
            int startX = Math.Max(4, centerX - (trend.Width + 10 + statsLabelWidth) / 2);
            // 小图比文字行高（基线下还有星期标注），两者对齐中线，不占额外的行高
            trend.Location = new Point(startX, y - (trend.Height - lblStats.Height) / 2);
            lblStats.Location = new Point(startX + trend.Width + 10, y);
            lblStats.Size = new Size(statsLabelWidth, 26);
        }

        /// <summary>设置卡片内部布局：首行标题/语言/快捷键/保存，其余为 4 列网格</summary>
        private void SettingsCard_Layout(object? sender, LayoutEventArgs e)
        {
            int innerX = 20;
            int columnWidth = (settingsCard.Width - innerX * 2) / 4;

            // 语言选择器跟随标题右侧（标题宽度随语言变化）
            lblSettingsTitle.Location = new Point(innerX, 10);
            lblLanguage.Location = new Point(lblSettingsTitle.Right + 12, 12);
            select_lang.Location = new Point(lblLanguage.Right + 6, 6);
            btnSaveSettings.Location = new Point(settingsCard.Width - 20 - btnSaveSettings.Width, 6);

            // 快捷键紧靠保存按钮左侧，按钮宽度随组合文字变化
            btnHotkey.Size = new Size(Math.Max(96, TextRenderer.MeasureText(btnHotkey.Text, btnHotkey.Font).Width + 24), 30);
            lblHotkey.Location = new Point(btnSaveSettings.Left - 16 - btnHotkey.Width - 6 - lblHotkey.Width, 12);
            btnHotkey.Location = new Point(lblHotkey.Right + 6, 6);

            for (int i = 0; i < durationFields.Count; i++)
            {
                int columnCenter = innerX + i * columnWidth + columnWidth / 2;
                var (label, input) = durationFields[i];
                label.Location = new Point(columnCenter - label.Width / 2, 42);
                input.Location = new Point(columnCenter - input.Width / 2, 64);
            }

            for (int i = 0; i < toggleFields.Count; i++)
            {
                int row = i / 4, column = i % 4;
                int columnCenter = innerX + column * columnWidth + columnWidth / 2;
                var (toggle, label) = toggleFields[i];
                int groupWidth = toggle.Width + 8 + label.Width;
                int x = Math.Max(innerX, Math.Min(columnCenter - groupWidth / 2, settingsCard.Width - innerX - groupWidth));
                int y = 114 + row * 36;
                toggle.Location = new Point(x, y);
                label.Location = new Point(x + toggle.Width + 8, y);
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            // 隐藏状态下窗口缩放不会触发本页布局，切回时强制重排并补算跨天
            if (Visible)
            {
                PerformLayout();
                RefreshStatsIfNewDay();
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

        private void switchTopMost_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            settings.AlwaysOnTop = switchTopMost.Checked;
            SaveAppSettings();
        }

        private void switchAutostart_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            settings.StartWithWindows = switchAutostart.Checked;
            SaveAppSettings();
        }

        private void switchTrayTime_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            settings.ShowTrayTime = switchTrayTime.Checked;
            SaveAppSettings();
        }

        /// <summary>应用级设置改动即时保存，并通知主窗体应用（置顶 / 自启 / 托盘 / 快捷键）</summary>
        private void SaveAppSettings()
        {
            settingsService.Save(settings);
            AppSettingsChanged?.Invoke();
        }

        private void RefreshHotkeyText()
        {
            btnHotkey.Text = Hotkey.TryParse(settings.Hotkey, out Hotkey hotkey)
                ? hotkey.ToString()
                : I18n.T("未设置", "Off");
        }

        private void btnHotkey_Click(object? sender, EventArgs e)
        {
            btnHotkey.Text = I18n.T("按下快捷键…", "Press keys…");
            btnHotkey.Enabled = false;
            HotkeyRecorder.Start(result =>
            {
                btnHotkey.Enabled = true;
                if (result is not null)
                {
                    if (result.Value.IsNone)
                    {
                        settings.Hotkey = string.Empty;
                        SaveAppSettings();
                    }
                    else if (!result.Value.IsValid)
                    {
                        AntdUI.Message.warn(FindForm()!, I18n.T("快捷键需要同时包含 Ctrl / Alt / Shift 中的一个", "Include Ctrl, Alt or Shift in the hotkey"), autoClose: 2);
                    }
                    else
                    {
                        settings.Hotkey = result.Value.ToString();
                        SaveAppSettings();
                    }
                }

                RefreshHotkeyText();
                settingsCard.PerformLayout();
            });
        }

        /// <summary>全局快捷键触发：与点击"开始/暂停"等价</summary>
        public void ToggleFromHotkey() => controller.StartOrPause();

        /// <summary>语言切换：持久化 + 全局广播（主窗体统一重刷各页签文案）</summary>
        private void select_lang_SelectedValueChanged(object sender, ObjectNEventArgs e)
        {
            var language = select_lang.SelectedIndex == 1 ? AppLanguage.English : AppLanguage.Chinese;
            if (language == I18n.Language)
            {
                return;
            }

            settings.Language = language == AppLanguage.English ? "en" : "zh";
            settingsService.Save(settings);
            I18n.SetLanguage(language);
        }

        /// <summary>语言切换后重刷本页静态文案（动态文案由对应状态刷新逻辑重算）</summary>
        public void ApplyLanguage()
        {
            btnSkip.Text = I18n.T("跳过", "Skip");
            btnReset.Text = I18n.T("重置", "Reset");
            lblSettingsTitle.Text = I18n.T("设置", "Settings");
            lblLanguage.Text = I18n.T("语言", "Language");
            lblHotkey.Text = I18n.T("快捷键", "Hotkey");
            btnSaveSettings.Text = I18n.T("保存设置", "Save");
            RefreshHotkeyText();
            // 防御性同步：语言被程序化修改时下拉框保持一致（再次触发时同语言会空操作，无死循环）
            select_lang.SelectedIndex = I18n.IsEnglish ? 1 : 0;

            // 标签宽度按文本实测（中文宽度写死会让英文换行/截断）
            lblSettingsTitle.Size = new Size(TextRenderer.MeasureText(lblSettingsTitle.Text, lblSettingsTitle.Font).Width + 8, 24);
            lblLanguage.Size = new Size(TextRenderer.MeasureText(lblLanguage.Text, lblLanguage.Font).Width + 8, 24);
            lblHotkey.Size = new Size(TextRenderer.MeasureText(lblHotkey.Text, lblHotkey.Font).Width + 8, 24);

            string[] durationTexts =
            {
                I18n.T("专注(分)", "Focus (min)"),
                I18n.T("短休(分)", "Short (min)"),
                I18n.T("长休(分)", "Long (min)"),
                I18n.T("长休(轮)", "Rounds")
            };
            for (int i = 0; i < durationFields.Count; i++)
            {
                durationFields[i].label.Text = durationTexts[i];
                durationFields[i].label.Size = new Size(
                    Math.Max(90, TextRenderer.MeasureText(durationTexts[i], durationFields[i].label.Font).Width + 8), 18);
            }

            string[] toggleTexts =
            {
                I18n.T("自动休息", "Auto break"),
                I18n.T("自动专注", "Auto focus"),
                I18n.T("提示音", "Sound"),
                I18n.T("深色主题", "Dark mode"),
                I18n.T("窗口置顶", "Always on top"),
                I18n.T("开机自启", "Auto start"),
                I18n.T("托盘时间", "Tray time")
            };
            for (int i = 0; i < toggleFields.Count; i++)
            {
                toggleFields[i].label.Text = toggleTexts[i];
                toggleFields[i].label.Size = new Size(TextRenderer.MeasureText(toggleTexts[i], toggleFields[i].label.Font).Width + 6, 24);
            }

            // 按当前状态重算动态文本：阶段行/开始按钮/统计
            SyncPhaseUi(controller.Remaining);
            RefreshStats();
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
            AntdUI.Message.success(FindForm()!, I18n.T("设置已保存，计时已重置", "Settings saved, timer reset"), autoClose: 2);
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
                    showBalloon(
                        I18n.T("专注完成 🍅", "Focus complete 🍅"),
                        I18n.F("完成了 {0} 分钟专注，休息一下吧！", "Focused {0} min — take a break!", settings.FocusMinutes));
                    RefreshStats();
                    break;
                case PomodoroPhase.ShortBreak:
                    showBalloon(I18n.T("休息结束", "Break over"), I18n.T("短休息结束，开始新的专注！", "Short break over — start a new focus!"));
                    break;
                case PomodoroPhase.LongBreak:
                    showBalloon(I18n.T("长休息结束", "Long break over"), I18n.T("充电完成，开始新一轮专注！", "Recharged — start a new round!"));
                    break;
            }
        }

        private void SyncPhaseUi(TimeSpan remaining)
        {
            (Color color, AntdUI.TTypeMini buttonType) = controller.Phase switch
            {
                PomodoroPhase.Focus => (UiTheme.FocusColor, AntdUI.TTypeMini.Error),
                PomodoroPhase.ShortBreak => (UiTheme.BreakColor, AntdUI.TTypeMini.Success),
                PomodoroPhase.LongBreak => (UiTheme.LongBreakColor, AntdUI.TTypeMini.Primary),
                _ => (UiTheme.FocusColor, AntdUI.TTypeMini.Error)
            };
            string title = PhaseTitle(controller.Phase);

            ring.TitleText = title;
            ring.RingColor = color;
            ring.TimeText = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
            double phaseSeconds = controller.PhaseDuration.TotalSeconds;
            ring.Progress = phaseSeconds > 0 ? (float)(remaining.TotalSeconds / phaseSeconds) : 0f;

            int displayRound = ((controller.RoundIndex - 1) % settings.RoundsBeforeLongBreak) + 1;
            lblPhase.Text = I18n.F("{0} · 第 {1} / {2} 轮", "{0} · Round {1} / {2}", title, displayRound, settings.RoundsBeforeLongBreak);
            lblPhase.ForeColor = color;
            btnStart.Type = buttonType;
            UpdateStartButton();
        }

        private string PhaseTitle(PomodoroPhase phase) => phase switch
        {
            PomodoroPhase.Focus => I18n.T("专注", "Focus"),
            PomodoroPhase.ShortBreak => I18n.T("短休息", "Short break"),
            PomodoroPhase.LongBreak => I18n.T("长休息", "Long break"),
            _ => I18n.T("专注", "Focus")
        };

        private void UpdateStartButton()
        {
            btnStart.Text = controller.IsRunning
                ? I18n.T("暂停", "Pause")
                : (controller.Remaining < controller.PhaseDuration
                    ? I18n.T("继续", "Resume")
                    : (controller.Phase == PomodoroPhase.Focus ? I18n.T("开始专注", "Start focus") : I18n.T("开始休息", "Start break")));
            RaiseTrayState();
        }

        /// <summary>托盘状态：剩余时间 + 是否计时中 + 是否已有进行中的阶段（已开始、未重置）</summary>
        private void RaiseTrayState()
        {
            bool active = controller.IsRunning || controller.Remaining < controller.PhaseDuration;
            TrayStateChanged?.Invoke(controller.Remaining, controller.IsRunning, active, PhaseTitle(controller.Phase));
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
            switchTopMost.Checked = settings.AlwaysOnTop;
            switchAutostart.Checked = settings.StartWithWindows;
            switchTrayTime.Checked = settings.ShowTrayTime;
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
            lblSettingsTitle.ForeColor = UiTheme.TextMain;
            // 首行的两个标签同样显式指定了文字色，主题切换后要跟着换，否则深色下几乎看不见
            lblLanguage.ForeColor = UiTheme.TextSub;
            lblHotkey.ForeColor = UiTheme.TextSub;
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

        #endregion

        private void RefreshStats()
        {
            statsDate = DateTime.Today;
            int count = sessionService.GetTodayCount();
            int minutes = sessionService.GetTodayMinutes();

            // 近 7 天每天完成的次数，从 6 天前排到今天
            Dictionary<string, int> daily = sessionService.GetDailyCounts(FocusTrendControl.Days);
            for (int i = 0; i < weekCounts.Length; i++)
            {
                weekCounts[i] = daily.GetValueOrDefault(statsDate.AddDays(i - (weekCounts.Length - 1)).ToString("yyyy-MM-dd"));
            }

            statsText = I18n.F(
                "今日 {0} 个番茄 · {1} 分钟专注 · 近 7 天 {2} 个",
                "Today {0} pomodoros · {1} min focused · {2} in the last 7 days",
                count, minutes, weekCounts.Sum());
            trend.SetCounts(weekCounts);

            // 文字区宽度留出控件内边距；取各句里最宽的，悬停时换成哪一句都放得下
            statsLabelWidth = Enumerable.Range(-1, weekCounts.Length + 1)
                .Max(index => TextRenderer.MeasureText(StatsText(index), lblStats.Font).Width) + 16;
            lblStats.Text = StatsText(trend.HoverIndex);
            LayoutStats(ClientSize.Width / 2, statsRowY);
        }

        private void RefreshStatsIfNewDay()
        {
            if (DateTime.Today != statsDate)
            {
                RefreshStats();
            }
        }

        /// <summary>统计行文字：没悬停时是今日统计，悬停趋势小图时是那一天的完成数</summary>
        private string StatsText(int hoverIndex)
        {
            if (hoverIndex < 0)
            {
                return statsText;
            }

            DateTime day = statsDate.AddDays(hoverIndex - (weekCounts.Length - 1));
            string weekday = I18n.IsEnglish
                ? day.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)
                : "周" + "日一二三四五六"[(int)day.DayOfWeek];
            return I18n.F("{0}（{1:M月d日}）· {2} 个番茄", "{0}, {1:M/d} · {2} pomodoros", weekday, day, weekCounts[hoverIndex]);
        }

        /// <summary>悬停趋势小图：文字改为显示那一天的数值，移开恢复今日统计；整行位置不变</summary>
        private void Trend_HoverChanged(int index)
        {
            lblStats.Text = StatsText(index);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                dayWatcher.Dispose();
                controller.Dispose();
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
