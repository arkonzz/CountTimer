using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CountTimer.Model
{
    /// <summary>
    /// 全局快捷键组合，配置里以 "Ctrl+Alt+Space" 形式保存。
    /// 只支持 Ctrl / Alt / Shift 三个修饰键：按键消息里不带"Win 键正按着"的标记，无法可靠识别
    /// </summary>
    public readonly record struct Hotkey(Keys Key, bool Ctrl, bool Alt, bool Shift)
    {
        private static readonly Dictionary<string, Keys> Aliases = new()
        {
            ["space"] = Keys.Space, ["esc"] = Keys.Escape, ["escape"] = Keys.Escape,
            ["enter"] = Keys.Enter, ["tab"] = Keys.Tab,
            ["0"] = Keys.D0, ["1"] = Keys.D1, ["2"] = Keys.D2, ["3"] = Keys.D3, ["4"] = Keys.D4,
            ["5"] = Keys.D5, ["6"] = Keys.D6, ["7"] = Keys.D7, ["8"] = Keys.D8, ["9"] = Keys.D9,
        };

        public bool IsNone => Key == Keys.None;

        /// <summary>必须有主键和至少一个修饰键，否则会吞掉普通按键</summary>
        public bool IsValid => Key != Keys.None && (Ctrl || Alt || Shift);

        /// <summary>拆出按键消息里的组合；只按了修饰键时 Key 为 None</summary>
        public static Hotkey FromKeys(Keys keys)
        {
            // 去掉修饰位后剩下的是单个键码；修饰键自己的键码是独立的值（不是位标志），只能按相等比较
            var key = (Keys)((int)keys & ~(int)Keys.Modifiers);
            if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu
                or Keys.LControlKey or Keys.RControlKey or Keys.LShiftKey or Keys.RShiftKey or Keys.LMenu or Keys.RMenu)
            {
                key = Keys.None;
            }

            return new Hotkey(key, keys.HasFlag(Keys.Control), keys.HasFlag(Keys.Alt), keys.HasFlag(Keys.Shift));
        }

        public static bool TryParse(string? text, out Hotkey hotkey)
        {
            hotkey = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            bool ctrl = false, alt = false, shift = false, hasKey = false;
            Keys key = Keys.None;
            foreach (string part in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                switch (part.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control": ctrl = true; break;
                    case "alt": alt = true; break;
                    case "shift": shift = true; break;
                    default:
                        string name = part.ToLowerInvariant();
                        if (hasKey || (!Aliases.TryGetValue(name, out key) && !Enum.TryParse(part, true, out key)))
                        {
                            return false;
                        }

                        hasKey = true;
                        break;
                }
            }

            if (!hasKey)
            {
                return false;
            }

            hotkey = new Hotkey(key, ctrl, alt, shift);
            return true;
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            parts.Add(KeyName(Key));
            return string.Join("+", parts);
        }

        private static string KeyName(Keys key) => key switch
        {
            >= Keys.D0 and <= Keys.D9 => ((int)(key - Keys.D0)).ToString(),
            >= Keys.NumPad0 and <= Keys.NumPad9 => "Num" + (int)(key - Keys.NumPad0),
            _ => key.ToString(),
        };
    }
}
