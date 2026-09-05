using System;
using System.Collections.Generic;

namespace LaptopKeyboardDoctor
{
    internal sealed class PointerAnalyzer
    {
        private sealed class ButtonState
        {
            public string Name;
            public int VirtualKey;
            public bool IsDown;
            public long DownAtMs;
            public long LastUpMs = -1;
            public bool StuckAlerted;
        }

        private readonly ButtonState _left = new ButtonState { Name = "Touchpad Left", VirtualKey = 0x01 };
        private readonly ButtonState _right = new ButtonState { Name = "Touchpad Right", VirtualKey = 0x02 };
        private readonly ButtonState _middle = new ButtonState { Name = "Touchpad Middle", VirtualKey = 0x04 };
        private readonly Dictionary<string, long> _lastAlertMs = new Dictionary<string, long>();
        private PointerStateSnapshot _snapshot = new PointerStateSnapshot();

        public int ChatterWindowMs = 45;
        public int StuckThresholdMs = 2500;
        public string ActiveDeviceId = string.Empty;

        public event Action<DiagnosticAlert> AlertRaised;
        public event Action<PointerStateSnapshot> StateChanged;

        public PointerStateSnapshot Snapshot { get { return _snapshot; } }

        public void Reset()
        {
            _left.IsDown = false; _left.LastUpMs = -1; _left.StuckAlerted = false;
            _right.IsDown = false; _right.LastUpMs = -1; _right.StuckAlerted = false;
            _middle.IsDown = false; _middle.LastUpMs = -1; _middle.StuckAlerted = false;
            _lastAlertMs.Clear();
            _snapshot = new PointerStateSnapshot();
            Publish();
        }

        public void Process(KeyEvidence evidence)
        {
            if (evidence == null || evidence.Source != InputSourceKind.RawMouse) return;
            if (!string.IsNullOrEmpty(ActiveDeviceId) && !string.Equals(ActiveDeviceId, evidence.DeviceId, StringComparison.OrdinalIgnoreCase)) return;

            _snapshot.LastDeviceId = evidence.DeviceId;
            if (evidence.DeltaX != 0 || evidence.DeltaY != 0)
            {
                _snapshot.MoveEvents++;
                _snapshot.TotalDistance += Math.Abs((long)evidence.DeltaX) + Math.Abs((long)evidence.DeltaY);
            }
            if (evidence.WheelDelta != 0) _snapshot.ScrollEvents++;

            ushort flags = evidence.MouseButtonFlags;
            ApplyButton(evidence, _left, flags, NativeMethods.RI_MOUSE_LEFT_BUTTON_DOWN, NativeMethods.RI_MOUSE_LEFT_BUTTON_UP);
            ApplyButton(evidence, _right, flags, NativeMethods.RI_MOUSE_RIGHT_BUTTON_DOWN, NativeMethods.RI_MOUSE_RIGHT_BUTTON_UP);
            ApplyButton(evidence, _middle, flags, NativeMethods.RI_MOUSE_MIDDLE_BUTTON_DOWN, NativeMethods.RI_MOUSE_MIDDLE_BUTTON_UP);

            _snapshot.LeftDown = _left.IsDown;
            _snapshot.RightDown = _right.IsDown;
            _snapshot.MiddleDown = _middle.IsDown;
            Publish();
        }

        private void ApplyButton(KeyEvidence evidence, ButtonState state, ushort flags, ushort downFlag, ushort upFlag)
        {
            if ((flags & downFlag) != 0)
            {
                if (state.LastUpMs >= 0)
                {
                    long gap = evidence.MonotonicMs - state.LastUpMs;
                    if (gap >= 0 && gap <= ChatterWindowMs)
                    {
                        RaiseOnce(evidence.MonotonicMs, "POINTER_CHATTER:" + state.VirtualKey, 500, new DiagnosticAlert
                        {
                            TimeUtc = evidence.TimeUtc,
                            Severity = DiagnosticSeverity.Warning,
                            Code = "TOUCHPAD_CHATTER",
                            VirtualKey = state.VirtualKey,
                            KeyName = state.Name,
                            Message = "Nút touchpad phát lần nhấn mới quá sát lần nhả trước.",
                            Evidence = "Khoảng Up-Down=" + gap + " ms; device=" + evidence.DeviceId
                        });
                    }
                }
                state.IsDown = true;
                state.DownAtMs = evidence.MonotonicMs;
                state.StuckAlerted = false;
                if (state == _left) _snapshot.LeftClicks++;
                if (state == _right) _snapshot.RightClicks++;
                if (state == _middle) _snapshot.MiddleClicks++;
            }
            if ((flags & upFlag) != 0)
            {
                state.IsDown = false;
                state.LastUpMs = evidence.MonotonicMs;
                state.StuckAlerted = false;
            }
        }

        public void Tick(long nowMs)
        {
            CheckStuck(_left, nowMs);
            CheckStuck(_right, nowMs);
            CheckStuck(_middle, nowMs);
        }

        private void CheckStuck(ButtonState state, long nowMs)
        {
            if (!state.IsDown || state.StuckAlerted || nowMs - state.DownAtMs < StuckThresholdMs) return;
            state.StuckAlerted = true;
            RaiseOnce(nowMs, "POINTER_STUCK:" + state.VirtualKey, StuckThresholdMs, new DiagnosticAlert
            {
                TimeUtc = DateTime.UtcNow,
                Severity = DiagnosticSeverity.Critical,
                Code = "TOUCHPAD_STUCK",
                VirtualKey = state.VirtualKey,
                KeyName = state.Name,
                Message = "Nút touchpad giữ trạng thái Down quá ngưỡng.",
                Evidence = "Giữ > " + StuckThresholdMs + " ms; device=" + _snapshot.LastDeviceId
            });
        }

        private void Publish()
        {
            Action<PointerStateSnapshot> changed = StateChanged;
            if (changed != null) changed(_snapshot);
        }

        private void RaiseOnce(long nowMs, string signature, int silenceMs, DiagnosticAlert alert)
        {
            long last;
            if (_lastAlertMs.TryGetValue(signature, out last) && nowMs - last < silenceMs) return;
            _lastAlertMs[signature] = nowMs;
            Action<DiagnosticAlert> raised = AlertRaised;
            if (raised != null) raised(alert);
        }
    }
}
