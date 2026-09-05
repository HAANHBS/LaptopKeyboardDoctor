using System;
using System.Collections.Generic;

namespace LaptopKeyboardDoctor
{
    internal enum InputSourceKind
    {
        RawKeyboard,
        LowLevelHook,
        Poll,
        AppCommand,
        RawHid,
        RawMouse,
        DeviceChange
    }

    internal enum DiagnosticSeverity
    {
        Info,
        Warning,
        Critical
    }

    internal enum TestMode
    {
        Normal,
        IdleContact,
        MatrixPair
    }

    internal sealed class KeyEvidence
    {
        public DateTime TimeUtc;
        public long MonotonicMs;
        public InputSourceKind Source;
        public int VirtualKey;
        public int ScanCode;
        public bool Extended;
        public bool IsDown;
        public bool IsUp;
        public bool Injected;
        public string DeviceId;
        public string Detail;
        public string ControlName;
        public ushort MouseButtonFlags;
        public int DeltaX;
        public int DeltaY;
        public int WheelDelta;

        public string Identity
        {
            get
            {
                return VirtualKey.ToString("X2") + ":" + ScanCode.ToString("X3") + (Extended ? ":E" : ":N");
            }
        }
    }

    internal sealed class DiagnosticAlert
    {
        public DateTime TimeUtc;
        public DiagnosticSeverity Severity;
        public string Code;
        public int VirtualKey;
        public string KeyName;
        public string Message;
        public string Evidence;
    }

    internal sealed class KeyStateInfo
    {
        public int VirtualKey;
        public int ScanCode;
        public bool Extended;
        public bool IsDown;
        public long DownAtMs;
        public long LastDownMs = -1;
        public long LastUpMs = -1;
        public long LastPhysicalMs = -1;
        public int DownCount;
        public int UpCount;
        public int RepeatCount;
        public int ChatterCount;
        public bool StuckAlerted;
        public string DeviceId;
        public long LastDwellMs;
    }

    internal sealed class AnalyzerSettings
    {
        public int ChatterWindowMs = 45;
        public int StuckThresholdMs = 2500;
        public int PollMismatchMs = 180;
        public int IdleGraceMs = 1200;
        public int RepeatStormCount = 20;
        public int RepeatStormWindowMs = 1000;
        public int MinimumHumanDwellMs = 12;
        public int AdaptiveSampleMinimum = 20;
        public int RapidPulseEvidenceCount = 2;
        public int RapidPulseEvidenceWindowMs = 1500;
        public int SimultaneousBurstWindowMs = 8;
        public int SimultaneousDistinctKeyCount = 4;
        public int RepeatDelayMs = 500;
        public double ExpectedRepeatRateHz = 15.0;
    }

    internal sealed class MatrixPairStep
    {
        public int FirstVirtualKey;
        public int SecondVirtualKey;
        public string Label;
        public bool Passed;
        public bool Failed;
    }

    internal sealed class DeviceRecord
    {
        public string Handle;
        public string Name;
        public string Type;
        public DateTime LastSeenUtc;
        public long EventCount;
    }

    internal sealed class HumanTimingSnapshot
    {
        public int SampleCount;
        public double MedianDwellMs;
        public double MedianAbsoluteDeviationMs;
        public double AdaptiveLowerBoundMs;
        public string Status;
    }

    internal sealed class PointerStateSnapshot
    {
        public bool LeftDown;
        public bool RightDown;
        public bool MiddleDown;
        public int LeftClicks;
        public int RightClicks;
        public int MiddleClicks;
        public long MoveEvents;
        public long ScrollEvents;
        public long TotalDistance;
        public string LastDeviceId;
    }

    internal sealed class SessionSnapshot
    {
        public readonly List<KeyEvidence> Events = new List<KeyEvidence>();
        public readonly List<DiagnosticAlert> Alerts = new List<DiagnosticAlert>();
        public readonly Dictionary<string, DeviceRecord> Devices = new Dictionary<string, DeviceRecord>();
    }
}
