using System;
using System.Windows.Forms;
using CountTimer.Model;

namespace CountTimer.View
{
    /// <summary>
    /// 快捷键录制：挂为消息过滤器，拦下下一次按键作为组合。
    /// Esc 取消，Backspace/Delete 清除，只按修饰键时继续等待，鼠标按下取消
    /// </summary>
    internal sealed class HotkeyRecorder : IMessageFilter
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSysKeyDown = 0x0104;
        private const int WmLButtonDown = 0x0201;
        private const int WmRButtonDown = 0x0204;
        private const int WmMButtonDown = 0x0207;

        private readonly Action<Hotkey?> finished;
        private bool done;

        private HotkeyRecorder(Action<Hotkey?> finished)
        {
            this.finished = finished;
        }

        public static void Start(Action<Hotkey?> finished)
        {
            Application.AddMessageFilter(new HotkeyRecorder(finished));
        }

        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WmKeyDown:
                case WmSysKeyDown:
                    Hotkey hotkey = Hotkey.FromKeys((Keys)(long)m.WParam | Control.ModifierKeys);
                    if (hotkey.Key == Keys.None)
                    {
                        return false;
                    }

                    if (hotkey.Key == Keys.Escape)
                    {
                        Finish(null);
                    }
                    else if (hotkey.Key is Keys.Back or Keys.Delete)
                    {
                        Finish(Hotkey.FromKeys(Keys.None));
                    }
                    else
                    {
                        Finish(hotkey);
                    }

                    return true;
                case WmLButtonDown:
                case WmRButtonDown:
                case WmMButtonDown:
                    Finish(null);
                    return false;
                default:
                    return false;
            }
        }

        private void Finish(Hotkey? result)
        {
            if (done)
            {
                return;
            }

            done = true;
            Application.RemoveMessageFilter(this);
            finished(result);
        }
    }
}
