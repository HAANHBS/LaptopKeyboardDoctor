using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LaptopKeyboardDoctor
{
    internal static class KeyNames
    {
        private static readonly Dictionary<int, string> Custom = new Dictionary<int, string>
        {
            { 0x01, "Touchpad Left" }, { 0x02, "Touchpad Right" }, { 0x04, "Touchpad Middle" },
            { 0x03, "Cancel" }, { 0x08, "Backspace" }, { 0x09, "Tab" }, { 0x0D, "Enter" },
            { 0x10, "Shift" }, { 0x11, "Ctrl" }, { 0x12, "Alt" }, { 0x13, "Pause" },
            { 0x14, "Caps Lock" }, { 0x1B, "Esc" }, { 0x20, "Space" }, { 0x21, "Page Up" },
            { 0x22, "Page Down" }, { 0x23, "End" }, { 0x24, "Home" }, { 0x25, "Left" },
            { 0x26, "Up" }, { 0x27, "Right" }, { 0x28, "Down" }, { 0x2C, "Print Screen" },
            { 0x2D, "Insert" }, { 0x2E, "Delete" }, { 0x5B, "Left Win" }, { 0x5C, "Right Win" },
            { 0x5D, "Menu" }, { 0x5F, "Sleep" }, { 0x90, "Num Lock" }, { 0x91, "Scroll Lock" },
            { 0xA0, "Left Shift" }, { 0xA1, "Right Shift" }, { 0xA2, "Left Ctrl" },
            { 0xA3, "Right Ctrl" }, { 0xA4, "Left Alt" }, { 0xA5, "Right Alt" },
            { 0xA6, "Browser Back" }, { 0xA7, "Browser Forward" }, { 0xA8, "Browser Refresh" },
            { 0xA9, "Browser Stop" }, { 0xAA, "Browser Search" }, { 0xAB, "Browser Favorites" },
            { 0xAC, "Browser Home" }, { 0xAD, "Volume Mute" }, { 0xAE, "Volume Down" },
            { 0xAF, "Volume Up" }, { 0xB0, "Media Next" }, { 0xB1, "Media Previous" },
            { 0xB2, "Media Stop" }, { 0xB3, "Play/Pause" }, { 0xB4, "Launch Mail" },
            { 0xB5, "Launch Media" }, { 0xB6, "Launch App 1" }, { 0xB7, "Launch App 2" },
            { 0xE5, "Process" }, { 0xE7, "Packet" }, { 0xF6, "Attn" }, { 0xF7, "CrSel" },
            { 0xF8, "ExSel" }, { 0xF9, "Erase EOF" }, { 0xFA, "Play" }, { 0xFB, "Zoom" }
        };

        public static string Get(int virtualKey)
        {
            string custom;
            if (Custom.TryGetValue(virtualKey, out custom)) return custom;
            if (virtualKey >= 0x30 && virtualKey <= 0x39) return ((char)virtualKey).ToString();
            if (virtualKey >= 0x41 && virtualKey <= 0x5A) return ((char)virtualKey).ToString();
            if (virtualKey >= 0x60 && virtualKey <= 0x69) return "Num " + (virtualKey - 0x60).ToString();
            if (virtualKey >= 0x70 && virtualKey <= 0x87) return "F" + (virtualKey - 0x6F).ToString();

            try
            {
                string value = ((Keys)virtualKey).ToString();
                if (!string.IsNullOrEmpty(value) && !value.StartsWith("None", StringComparison.OrdinalIgnoreCase)) return value;
            }
            catch { }

            return "VK_" + virtualKey.ToString("X2");
        }

        public static string ScanLabel(int virtualKey, int scanCode, bool extended)
        {
            return Get(virtualKey) + " [VK " + virtualKey.ToString("X2") + ", SC " + scanCode.ToString("X3") + (extended ? " E0]" : "]");
        }
    }
}
