using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LaptopKeyboardDoctor
{
    internal static class NativeMethods
    {
        public const int WM_INPUT = 0x00FF;
        public const int WM_INPUT_DEVICE_CHANGE = 0x00FE;
        public const int WM_APPCOMMAND = 0x0319;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;
        public const int WH_KEYBOARD_LL = 13;

        public const uint RID_INPUT = 0x10000003;
        public const uint RIDI_DEVICENAME = 0x20000007;
        public const uint RIDEV_INPUTSINK = 0x00000100;
        public const uint RIDEV_DEVNOTIFY = 0x00002000;
        public const uint RIM_TYPEMOUSE = 0;
        public const uint RIM_TYPEKEYBOARD = 1;
        public const uint RIM_TYPEHID = 2;
        public const ushort RI_KEY_BREAK = 0x0001;
        public const ushort RI_KEY_E0 = 0x0002;
        public const ushort RI_KEY_E1 = 0x0004;
        public const uint LLKHF_EXTENDED = 0x01;
        public const uint LLKHF_INJECTED = 0x10;
        public const uint SPI_GETKEYBOARDSPEED = 0x000A;
        public const uint SPI_GETKEYBOARDDELAY = 0x0016;

        public const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
        public const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
        public const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
        public const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
        public const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
        public const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;
        public const ushort RI_MOUSE_BUTTON_4_DOWN = 0x0040;
        public const ushort RI_MOUSE_BUTTON_4_UP = 0x0080;
        public const ushort RI_MOUSE_BUTTON_5_DOWN = 0x0100;
        public const ushort RI_MOUSE_BUTTON_5_UP = 0x0200;
        public const ushort RI_MOUSE_WHEEL = 0x0400;
        public const ushort RI_MOUSE_HWHEEL = 0x0800;

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTHEADER
        {
            public uint Type;
            public uint Size;
            public IntPtr Device;
            public IntPtr WParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct RAWMOUSE
        {
            [FieldOffset(0)] public ushort Flags;
            [FieldOffset(4)] public uint Buttons;
            [FieldOffset(4)] public ushort ButtonFlags;
            [FieldOffset(6)] public ushort ButtonData;
            [FieldOffset(8)] public uint RawButtons;
            [FieldOffset(12)] public int LastX;
            [FieldOffset(16)] public int LastY;
            [FieldOffset(20)] public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint VirtualKey;
            public uint ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        public delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterRawInputDevices(
            [In] RAWINPUTDEVICE[] devices,
            uint deviceCount,
            uint size);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(
            IntPtr rawInput,
            uint command,
            IntPtr data,
            ref uint size,
            uint headerSize);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetRawInputDeviceInfo(
            IntPtr device,
            uint command,
            StringBuilder data,
            ref uint size);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(
            int hookId,
            LowLevelKeyboardProc callback,
            IntPtr module,
            uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint action, uint parameter, out uint value, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string moduleName);
    }

    internal sealed class RawInputReader
    {
        private readonly Dictionary<IntPtr, string> _deviceNames = new Dictionary<IntPtr, string>();

        public void Register(IntPtr windowHandle)
        {
            NativeMethods.RAWINPUTDEVICE[] devices = new NativeMethods.RAWINPUTDEVICE[3];
            devices[0].UsagePage = 0x01;
            devices[0].Usage = 0x06;
            devices[0].Flags = NativeMethods.RIDEV_INPUTSINK | NativeMethods.RIDEV_DEVNOTIFY;
            devices[0].Target = windowHandle;

            // Consumer controls cover many volume/media/assistant keys used by newer laptops.
            devices[1].UsagePage = 0x0C;
            devices[1].Usage = 0x01;
            devices[1].Flags = NativeMethods.RIDEV_INPUTSINK | NativeMethods.RIDEV_DEVNOTIFY;
            devices[1].Target = windowHandle;

            // Generic Desktop Mouse also receives pointer/button events produced by many touchpads.
            devices[2].UsagePage = 0x01;
            devices[2].Usage = 0x02;
            devices[2].Flags = NativeMethods.RIDEV_INPUTSINK | NativeMethods.RIDEV_DEVNOTIFY;
            devices[2].Target = windowHandle;

            if (!NativeMethods.RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf(typeof(NativeMethods.RAWINPUTDEVICE))))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Không đăng ký được Raw Input.");
            }
        }

        public KeyEvidence Read(IntPtr rawHandle, long nowMs)
        {
            uint size = 0;
            uint headerSize = (uint)Marshal.SizeOf(typeof(NativeMethods.RAWINPUTHEADER));
            uint first = NativeMethods.GetRawInputData(rawHandle, NativeMethods.RID_INPUT, IntPtr.Zero, ref size, headerSize);
            if (first != 0 || size < headerSize) return null;

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                uint copied = NativeMethods.GetRawInputData(rawHandle, NativeMethods.RID_INPUT, buffer, ref size, headerSize);
                if (copied == UInt32.MaxValue || copied != size) return null;

                NativeMethods.RAWINPUTHEADER header = (NativeMethods.RAWINPUTHEADER)Marshal.PtrToStructure(buffer, typeof(NativeMethods.RAWINPUTHEADER));
                string device = GetDeviceName(header.Device);
                IntPtr payload = IntPtr.Add(buffer, (int)headerSize);

                if (header.Type == NativeMethods.RIM_TYPEKEYBOARD)
                {
                    NativeMethods.RAWKEYBOARD keyboard = (NativeMethods.RAWKEYBOARD)Marshal.PtrToStructure(payload, typeof(NativeMethods.RAWKEYBOARD));
                    bool isUp = (keyboard.Flags & NativeMethods.RI_KEY_BREAK) != 0;
                    bool extended = (keyboard.Flags & (NativeMethods.RI_KEY_E0 | NativeMethods.RI_KEY_E1)) != 0;
                    int virtualKey = keyboard.VKey;
                    if (virtualKey == 0x10) virtualKey = keyboard.MakeCode == 0x36 ? 0xA1 : 0xA0;
                    if (virtualKey == 0x11) virtualKey = extended ? 0xA3 : 0xA2;
                    if (virtualKey == 0x12) virtualKey = extended ? 0xA5 : 0xA4;
                    return new KeyEvidence
                    {
                        TimeUtc = DateTime.UtcNow,
                        MonotonicMs = nowMs,
                        Source = InputSourceKind.RawKeyboard,
                        VirtualKey = virtualKey,
                        ScanCode = keyboard.MakeCode,
                        Extended = extended,
                        IsDown = !isUp,
                        IsUp = isUp,
                        Injected = false,
                        DeviceId = device,
                        Detail = "raw flags=0x" + keyboard.Flags.ToString("X4") + ", msg=0x" + keyboard.Message.ToString("X4")
                    };
                }

                if (header.Type == NativeMethods.RIM_TYPEHID && size >= headerSize + 8)
                {
                    uint reportSize = (uint)Marshal.ReadInt32(payload, 0);
                    uint reportCount = (uint)Marshal.ReadInt32(payload, 4);
                    long available = (long)size - (long)headerSize - 8L;
                    long wanted = (long)reportSize * (long)reportCount;
                    int byteCount = (int)Math.Min(Math.Min(available, wanted), 128L);
                    byte[] bytes = new byte[Math.Max(0, byteCount)];
                    if (byteCount > 0) Marshal.Copy(IntPtr.Add(payload, 8), bytes, 0, byteCount);
                    return new KeyEvidence
                    {
                        TimeUtc = DateTime.UtcNow,
                        MonotonicMs = nowMs,
                        Source = InputSourceKind.RawHid,
                        VirtualKey = 0,
                        ScanCode = 0,
                        IsDown = true,
                        IsUp = false,
                        DeviceId = device,
                        Detail = "HID size=" + reportSize + ", count=" + reportCount + ", bytes=" + BitConverter.ToString(bytes)
                    };
                }

                if (header.Type == NativeMethods.RIM_TYPEMOUSE)
                {
                    NativeMethods.RAWMOUSE mouse = (NativeMethods.RAWMOUSE)Marshal.PtrToStructure(payload, typeof(NativeMethods.RAWMOUSE));
                    int wheel = 0;
                    if ((mouse.ButtonFlags & (NativeMethods.RI_MOUSE_WHEEL | NativeMethods.RI_MOUSE_HWHEEL)) != 0)
                    {
                        wheel = unchecked((short)mouse.ButtonData);
                    }
                    return new KeyEvidence
                    {
                        TimeUtc = DateTime.UtcNow,
                        MonotonicMs = nowMs,
                        Source = InputSourceKind.RawMouse,
                        DeviceId = device,
                        ControlName = MouseControlName(mouse.ButtonFlags),
                        MouseButtonFlags = mouse.ButtonFlags,
                        DeltaX = mouse.LastX,
                        DeltaY = mouse.LastY,
                        WheelDelta = wheel,
                        IsDown = (mouse.ButtonFlags & (NativeMethods.RI_MOUSE_LEFT_BUTTON_DOWN | NativeMethods.RI_MOUSE_RIGHT_BUTTON_DOWN | NativeMethods.RI_MOUSE_MIDDLE_BUTTON_DOWN | NativeMethods.RI_MOUSE_BUTTON_4_DOWN | NativeMethods.RI_MOUSE_BUTTON_5_DOWN)) != 0,
                        IsUp = (mouse.ButtonFlags & (NativeMethods.RI_MOUSE_LEFT_BUTTON_UP | NativeMethods.RI_MOUSE_RIGHT_BUTTON_UP | NativeMethods.RI_MOUSE_MIDDLE_BUTTON_UP | NativeMethods.RI_MOUSE_BUTTON_4_UP | NativeMethods.RI_MOUSE_BUTTON_5_UP)) != 0,
                        Detail = "mouse flags=0x" + mouse.ButtonFlags.ToString("X4") + ", dx=" + mouse.LastX + ", dy=" + mouse.LastY + ", wheel=" + wheel
                    };
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return null;
        }

        private static string MouseControlName(ushort flags)
        {
            List<string> names = new List<string>();
            if ((flags & NativeMethods.RI_MOUSE_LEFT_BUTTON_DOWN) != 0) names.Add("Left Down");
            if ((flags & NativeMethods.RI_MOUSE_LEFT_BUTTON_UP) != 0) names.Add("Left Up");
            if ((flags & NativeMethods.RI_MOUSE_RIGHT_BUTTON_DOWN) != 0) names.Add("Right Down");
            if ((flags & NativeMethods.RI_MOUSE_RIGHT_BUTTON_UP) != 0) names.Add("Right Up");
            if ((flags & NativeMethods.RI_MOUSE_MIDDLE_BUTTON_DOWN) != 0) names.Add("Middle Down");
            if ((flags & NativeMethods.RI_MOUSE_MIDDLE_BUTTON_UP) != 0) names.Add("Middle Up");
            if ((flags & NativeMethods.RI_MOUSE_BUTTON_4_DOWN) != 0) names.Add("Button 4 Down");
            if ((flags & NativeMethods.RI_MOUSE_BUTTON_4_UP) != 0) names.Add("Button 4 Up");
            if ((flags & NativeMethods.RI_MOUSE_BUTTON_5_DOWN) != 0) names.Add("Button 5 Down");
            if ((flags & NativeMethods.RI_MOUSE_BUTTON_5_UP) != 0) names.Add("Button 5 Up");
            if ((flags & NativeMethods.RI_MOUSE_WHEEL) != 0) names.Add("Vertical Scroll");
            if ((flags & NativeMethods.RI_MOUSE_HWHEEL) != 0) names.Add("Horizontal Scroll");
            if (names.Count == 0) names.Add("Pointer Move");
            return string.Join(" + ", names.ToArray());
        }

        public static void ReadKeyboardRepeatSettings(out int delayMs, out double rateHz)
        {
            uint delaySetting;
            uint speedSetting;
            bool delayOk = NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETKEYBOARDDELAY, 0, out delaySetting, 0);
            bool speedOk = NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETKEYBOARDSPEED, 0, out speedSetting, 0);
            delayMs = delayOk ? ((int)Math.Min(3U, delaySetting) + 1) * 250 : 500;
            uint safeSpeed = Math.Min(31U, speedSetting);
            rateHz = speedOk ? 2.5 + (27.5 * safeSpeed / 31.0) : 15.0;
        }

        public string GetDeviceName(IntPtr device)
        {
            if (device == IntPtr.Zero) return "SYSTEM";
            string cached;
            if (_deviceNames.TryGetValue(device, out cached)) return cached;

            uint chars = 0;
            NativeMethods.GetRawInputDeviceInfo(device, NativeMethods.RIDI_DEVICENAME, null, ref chars);
            if (chars == 0)
            {
                cached = "HANDLE_0x" + device.ToInt64().ToString("X");
            }
            else
            {
                StringBuilder builder = new StringBuilder((int)chars + 1);
                uint actual = NativeMethods.GetRawInputDeviceInfo(device, NativeMethods.RIDI_DEVICENAME, builder, ref chars);
                cached = actual == UInt32.MaxValue ? "HANDLE_0x" + device.ToInt64().ToString("X") : builder.ToString();
            }

            _deviceNames[device] = cached;
            return cached;
        }

        public void ForgetDevice(IntPtr device)
        {
            if (_deviceNames.ContainsKey(device)) _deviceNames.Remove(device);
        }
    }

    internal sealed class KeyboardHook : IDisposable
    {
        private NativeMethods.LowLevelKeyboardProc _callback;
        private IntPtr _hook;

        public event Action<KeyEvidence> Evidence;

        public void Start()
        {
            if (_hook != IntPtr.Zero) return;
            _callback = HookCallback;
            IntPtr module = NativeMethods.GetModuleHandle(null);
            _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _callback, module, 0);
            if (_hook == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Không cài được low-level keyboard hook.");
            }
        }

        private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                int message = unchecked((int)wParam.ToInt64());
                bool isDown = message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN;
                bool isUp = message == NativeMethods.WM_KEYUP || message == NativeMethods.WM_SYSKEYUP;
                if (isDown || isUp)
                {
                    NativeMethods.KBDLLHOOKSTRUCT data = (NativeMethods.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.KBDLLHOOKSTRUCT));
                    Action<KeyEvidence> handler = Evidence;
                    if (handler != null)
                    {
                        handler(new KeyEvidence
                        {
                            TimeUtc = DateTime.UtcNow,
                            MonotonicMs = Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency,
                            Source = InputSourceKind.LowLevelHook,
                            VirtualKey = (int)data.VirtualKey,
                            ScanCode = (int)data.ScanCode,
                            Extended = (data.Flags & NativeMethods.LLKHF_EXTENDED) != 0,
                            IsDown = isDown,
                            IsUp = isUp,
                            Injected = (data.Flags & NativeMethods.LLKHF_INJECTED) != 0,
                            DeviceId = "GLOBAL_HOOK",
                            Detail = "hook flags=0x" + data.Flags.ToString("X2")
                        });
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
            _callback = null;
        }
    }
}
