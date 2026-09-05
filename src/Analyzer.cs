using System;
using System.Collections.Generic;
using System.Linq;

namespace LaptopKeyboardDoctor
{
    internal sealed class KeyboardAnalyzer
    {
        private readonly Dictionary<string, KeyStateInfo> _states = new Dictionary<string, KeyStateInfo>();
        private readonly Dictionary<string, Queue<long>> _repeatTimes = new Dictionary<string, Queue<long>>();
        private readonly Dictionary<string, Queue<long>> _rapidPulseTimes = new Dictionary<string, Queue<long>>();
        private readonly Dictionary<string, Queue<long>> _adaptiveOutlierTimes = new Dictionary<string, Queue<long>>();
        private readonly Dictionary<string, Queue<long>> _initialDownTimes = new Dictionary<string, Queue<long>>();
        private readonly Dictionary<int, bool> _pollStates = new Dictionary<int, bool>();
        private readonly Dictionary<int, long> _pollChangedMs = new Dictionary<int, long>();
        private readonly Dictionary<string, long> _lastAlertMs = new Dictionary<string, long>();
        private readonly HashSet<int> _matrixKeysDown = new HashSet<int>();
        private readonly Queue<KeyValuePair<long, int>> _recentDistinctDowns = new Queue<KeyValuePair<long, int>>();
        private readonly List<long> _dwellSamples = new List<long>();

        private long _modeStartedMs;
        private MatrixPairStep _matrixStep;

        public readonly AnalyzerSettings Settings = new AnalyzerSettings();
        public TestMode Mode { get; private set; }
        public string ActiveDeviceId { get; set; }

        public event Action<DiagnosticAlert> AlertRaised;
        public event Action<KeyStateInfo> StateChanged;
        public event Action<MatrixPairStep> MatrixStepChanged;
        public event Action<HumanTimingSnapshot> HumanProfileChanged;

        public KeyboardAnalyzer()
        {
            Mode = TestMode.Normal;
            ActiveDeviceId = string.Empty;
        }

        public IList<KeyStateInfo> States
        {
            get { return _states.Values.ToList(); }
        }

        public HumanTimingSnapshot HumanProfile
        {
            get { return BuildHumanProfile(); }
        }

        public void Reset()
        {
            _states.Clear();
            _repeatTimes.Clear();
            _rapidPulseTimes.Clear();
            _adaptiveOutlierTimes.Clear();
            _initialDownTimes.Clear();
            _pollStates.Clear();
            _pollChangedMs.Clear();
            _lastAlertMs.Clear();
            _matrixKeysDown.Clear();
            _recentDistinctDowns.Clear();
            _dwellSamples.Clear();
            _matrixStep = null;
            Mode = TestMode.Normal;
        }

        public void StartIdleContactTest(long nowMs)
        {
            ReleaseAllStates();
            Mode = TestMode.IdleContact;
            _modeStartedMs = nowMs;
            _matrixStep = null;
        }

        public void StartMatrixPairTest(MatrixPairStep step, long nowMs)
        {
            ReleaseAllStates();
            Mode = TestMode.MatrixPair;
            _modeStartedMs = nowMs;
            _matrixStep = step;
            _matrixKeysDown.Clear();
        }

        public void StopGuidedTest()
        {
            Mode = TestMode.Normal;
            _matrixStep = null;
            _matrixKeysDown.Clear();
        }

        public bool IsIdleMonitoring(long nowMs)
        {
            return Mode == TestMode.IdleContact && nowMs - _modeStartedMs >= Settings.IdleGraceMs;
        }

        public void ProcessPrimary(KeyEvidence evidence)
        {
            if (evidence == null || evidence.Source != InputSourceKind.RawKeyboard) return;
            if (!string.IsNullOrEmpty(ActiveDeviceId) && !string.Equals(ActiveDeviceId, evidence.DeviceId, StringComparison.OrdinalIgnoreCase)) return;

            KeyStateInfo state;
            if (!_states.TryGetValue(evidence.Identity, out state))
            {
                state = new KeyStateInfo
                {
                    VirtualKey = evidence.VirtualKey,
                    ScanCode = evidence.ScanCode,
                    Extended = evidence.Extended,
                    DeviceId = evidence.DeviceId
                };
                _states[evidence.Identity] = state;
            }

            state.LastPhysicalMs = evidence.MonotonicMs;
            state.DeviceId = evidence.DeviceId;

            if (evidence.IsDown)
            {
                HandleDown(evidence, state);
            }
            else if (evidence.IsUp)
            {
                HandleUp(evidence, state);
            }

            Action<KeyStateInfo> changed = StateChanged;
            if (changed != null) changed(state);
        }

        private void HandleDown(KeyEvidence evidence, KeyStateInfo state)
        {
            if (state.IsDown)
            {
                state.RepeatCount++;
                long heldMs = evidence.MonotonicMs - state.DownAtMs;
                int earlyLimit = Math.Max(80, Settings.RepeatDelayMs - 80);
                if (state.RepeatCount == 1 && heldMs >= 0 && heldMs < earlyLimit)
                {
                    RaiseOnce(evidence.MonotonicMs, "EARLY_REPEAT:" + evidence.Identity, 1000, new DiagnosticAlert
                    {
                        TimeUtc = evidence.TimeUtc,
                        Severity = DiagnosticSeverity.Warning,
                        Code = "EARLY_REPEAT",
                        VirtualKey = evidence.VirtualKey,
                        KeyName = KeyNames.Get(evidence.VirtualKey),
                        Message = "Phím phát repeat sớm hơn độ trễ lặp đang cấu hình trong Windows.",
                        Evidence = "Repeat sau " + heldMs + " ms; Windows delay xấp xỉ " + Settings.RepeatDelayMs + " ms"
                    });
                }
                Queue<long> times;
                if (!_repeatTimes.TryGetValue(evidence.Identity, out times))
                {
                    times = new Queue<long>();
                    _repeatTimes[evidence.Identity] = times;
                }
                times.Enqueue(evidence.MonotonicMs);
                while (times.Count > 0 && evidence.MonotonicMs - times.Peek() > Settings.RepeatStormWindowMs) times.Dequeue();
                if (times.Count >= Settings.RepeatStormCount)
                {
                    RaiseOnce(evidence.MonotonicMs, "REPEAT_STORM:" + evidence.Identity, 1500, new DiagnosticAlert
                    {
                        TimeUtc = evidence.TimeUtc,
                        Severity = DiagnosticSeverity.Warning,
                        Code = "REPEAT_STORM",
                        VirtualKey = evidence.VirtualKey,
                        KeyName = KeyNames.Get(evidence.VirtualKey),
                        Message = "Phím phát lặp dày; có thể đang kẹt hoặc chạm mạch.",
                        Evidence = times.Count + " lần lặp/" + Settings.RepeatStormWindowMs + " ms; " + evidence.Detail
                    });
                }
                return;
            }

            ObserveInitialDown(evidence);

            if (state.LastUpMs >= 0)
            {
                long gap = evidence.MonotonicMs - state.LastUpMs;
                if (gap >= 0 && gap <= Settings.ChatterWindowMs)
                {
                    state.ChatterCount++;
                    RaiseOnce(evidence.MonotonicMs, "CHATTER:" + evidence.Identity, Settings.ChatterWindowMs, new DiagnosticAlert
                    {
                        TimeUtc = evidence.TimeUtc,
                        Severity = DiagnosticSeverity.Warning,
                        Code = "CHATTER",
                        VirtualKey = evidence.VirtualKey,
                        KeyName = KeyNames.Get(evidence.VirtualKey),
                        Message = "Nghi tiếp điểm dội/double key ngoài ý muốn.",
                        Evidence = "Down mới sau Up " + gap + " ms; ngưỡng " + Settings.ChatterWindowMs + " ms"
                    });
                }
            }

            state.IsDown = true;
            state.DownAtMs = evidence.MonotonicMs;
            state.LastDownMs = evidence.MonotonicMs;
            state.DownCount++;
            state.StuckAlerted = false;

            if (Mode == TestMode.IdleContact && evidence.MonotonicMs - _modeStartedMs >= Settings.IdleGraceMs)
            {
                RaiseOnce(evidence.MonotonicMs, "IDLE:" + evidence.Identity, 250, new DiagnosticAlert
                {
                    TimeUtc = evidence.TimeUtc,
                    Severity = DiagnosticSeverity.Critical,
                    Code = "IDLE_CONTACT",
                    VirtualKey = evidence.VirtualKey,
                    KeyName = KeyNames.Get(evidence.VirtualKey),
                    Message = "Có tín hiệu phím trong lúc yêu cầu không chạm bàn phím.",
                    Evidence = KeyNames.ScanLabel(evidence.VirtualKey, evidence.ScanCode, evidence.Extended) + "; device=" + evidence.DeviceId
                });
            }

            if (Mode == TestMode.MatrixPair && _matrixStep != null)
            {
                int key = evidence.VirtualKey;
                if (key != _matrixStep.FirstVirtualKey && key != _matrixStep.SecondVirtualKey)
                {
                    _matrixStep.Failed = true;
                    RaiseOnce(evidence.MonotonicMs, "PHANTOM:" + evidence.Identity, 250, new DiagnosticAlert
                    {
                        TimeUtc = evidence.TimeUtc,
                        Severity = DiagnosticSeverity.Critical,
                        Code = "PHANTOM_KEY",
                        VirtualKey = evidence.VirtualKey,
                        KeyName = KeyNames.Get(evidence.VirtualKey),
                        Message = "Xuất hiện phím thứ ba ngoài cặp đang kiểm tra; nghi chạm ma trận/ghost key.",
                        Evidence = "Cặp yêu cầu: " + _matrixStep.Label + "; nhận thêm " + KeyNames.Get(evidence.VirtualKey)
                    });
                }
                else
                {
                    _matrixKeysDown.Add(key);
                    if (_matrixKeysDown.Contains(_matrixStep.FirstVirtualKey) && _matrixKeysDown.Contains(_matrixStep.SecondVirtualKey))
                    {
                        _matrixStep.Passed = true;
                    }
                }

                Action<MatrixPairStep> matrixChanged = MatrixStepChanged;
                if (matrixChanged != null) matrixChanged(_matrixStep);
            }
        }

        private void HandleUp(KeyEvidence evidence, KeyStateInfo state)
        {
            bool hadDown = state.IsDown;
            if (!state.IsDown)
            {
                RaiseOnce(evidence.MonotonicMs, "ORPHAN_UP:" + evidence.Identity, 1000, new DiagnosticAlert
                {
                    TimeUtc = evidence.TimeUtc,
                    Severity = DiagnosticSeverity.Info,
                    Code = "ORPHAN_KEYUP",
                    VirtualKey = evidence.VirtualKey,
                    KeyName = KeyNames.Get(evidence.VirtualKey),
                    Message = "Nhận KeyUp nhưng không thấy KeyDown tương ứng trong phiên.",
                    Evidence = evidence.Detail
                });
            }
            if (hadDown)
            {
                long dwellMs = Math.Max(0, evidence.MonotonicMs - state.DownAtMs);
                state.LastDwellMs = dwellMs;
                ObserveDwell(evidence, dwellMs);
            }
            state.IsDown = false;
            state.LastUpMs = evidence.MonotonicMs;
            state.UpCount++;
            state.StuckAlerted = false;
            _matrixKeysDown.Remove(evidence.VirtualKey);
        }

        private void ObserveInitialDown(KeyEvidence evidence)
        {
            ObserveBurst(evidence);

            Queue<long> downTimes;
            if (!_initialDownTimes.TryGetValue(evidence.Identity, out downTimes))
            {
                downTimes = new Queue<long>();
                _initialDownTimes[evidence.Identity] = downTimes;
            }
            downTimes.Enqueue(evidence.MonotonicMs);
            while (downTimes.Count > 7) downTimes.Dequeue();
            if (downTimes.Count < 7) return;

            long[] values = downTimes.ToArray();
            List<double> intervals = new List<double>();
            for (int index = 1; index < values.Length; index++) intervals.Add(values[index] - values[index - 1]);
            double mean = intervals.Average();
            double variance = intervals.Select(delegate(double value) { double delta = value - mean; return delta * delta; }).Average();
            double deviation = Math.Sqrt(variance);
            if (mean >= 20 && mean <= 500 && deviation <= 2.0)
            {
                RaiseOnce(evidence.MonotonicMs, "MACHINE_RHYTHM:" + evidence.Identity, 3000, new DiagnosticAlert
                {
                    TimeUtc = evidence.TimeUtc,
                    Severity = DiagnosticSeverity.Warning,
                    Code = "MACHINE_RHYTHM",
                    VirtualKey = evidence.VirtualKey,
                    KeyName = KeyNames.Get(evidence.VirtualKey),
                    Message = "Chu kỳ nhấn lặp đều gần như máy; không giống biến thiên tự nhiên của thao tác người.",
                    Evidence = "Chu kỳ TB=" + mean.ToString("0.0") + " ms; lệch chuẩn=" + deviation.ToString("0.0") + " ms; 6 khoảng liên tiếp"
                });
            }
        }

        private void ObserveBurst(KeyEvidence evidence)
        {
            if (Mode == TestMode.MatrixPair) return;
            if (evidence.VirtualKey == 0xA0 || evidence.VirtualKey == 0xA1 || evidence.VirtualKey == 0xA2 || evidence.VirtualKey == 0xA3 || evidence.VirtualKey == 0xA4 || evidence.VirtualKey == 0xA5) return;

            _recentDistinctDowns.Enqueue(new KeyValuePair<long, int>(evidence.MonotonicMs, evidence.VirtualKey));
            while (_recentDistinctDowns.Count > 0 && evidence.MonotonicMs - _recentDistinctDowns.Peek().Key > Settings.SimultaneousBurstWindowMs) _recentDistinctDowns.Dequeue();
            HashSet<int> distinct = new HashSet<int>(_recentDistinctDowns.Select(delegate(KeyValuePair<long, int> item) { return item.Value; }));
            if (distinct.Count < Settings.SimultaneousDistinctKeyCount) return;

            List<int> ordered = distinct.OrderBy(delegate(int key) { return key; }).ToList();
            string names = string.Join(" + ", ordered.Select(delegate(int key) { return KeyNames.Get(key); }).ToArray());
            RaiseOnce(evidence.MonotonicMs, "NON_HUMAN_BURST:" + names, 1500, new DiagnosticAlert
            {
                TimeUtc = evidence.TimeUtc,
                Severity = DiagnosticSeverity.Warning,
                Code = "NON_HUMAN_BURST",
                VirtualKey = evidence.VirtualKey,
                KeyName = names,
                Message = "Nhiều phím khác nhau phát gần đồng thời; nghi chạm ma trận hoặc tì cả cụm phím.",
                Evidence = distinct.Count + " phím trong " + Settings.SimultaneousBurstWindowMs + " ms"
            });
        }

        private void ObserveDwell(KeyEvidence evidence, long dwellMs)
        {
            if (dwellMs < Settings.MinimumHumanDwellMs)
            {
                Queue<long> rapid;
                if (!_rapidPulseTimes.TryGetValue(evidence.Identity, out rapid))
                {
                    rapid = new Queue<long>();
                    _rapidPulseTimes[evidence.Identity] = rapid;
                }
                rapid.Enqueue(evidence.MonotonicMs);
                while (rapid.Count > 0 && evidence.MonotonicMs - rapid.Peek() > Settings.RapidPulseEvidenceWindowMs) rapid.Dequeue();
                if (rapid.Count >= Settings.RapidPulseEvidenceCount)
                {
                    RaiseOnce(evidence.MonotonicMs, "NON_HUMAN_PULSE:" + evidence.Identity, 1500, new DiagnosticAlert
                    {
                        TimeUtc = evidence.TimeUtc,
                        Severity = DiagnosticSeverity.Critical,
                        Code = "NON_HUMAN_PULSE",
                        VirtualKey = evidence.VirtualKey,
                        KeyName = KeyNames.Get(evidence.VirtualKey),
                        Message = "Phím tạo nhiều xung Down-Up quá ngắn để giống thao tác bấm thông thường.",
                        Evidence = rapid.Count + " xung dưới " + Settings.MinimumHumanDwellMs + " ms trong " + Settings.RapidPulseEvidenceWindowMs + " ms; dwell cuối=" + dwellMs + " ms"
                    });
                }
                PublishHumanProfile();
                return;
            }

            HumanTimingSnapshot before = BuildHumanProfile();
            if (before.SampleCount >= Settings.AdaptiveSampleMinimum && dwellMs < before.AdaptiveLowerBoundMs)
            {
                Queue<long> outliers;
                if (!_adaptiveOutlierTimes.TryGetValue(evidence.Identity, out outliers))
                {
                    outliers = new Queue<long>();
                    _adaptiveOutlierTimes[evidence.Identity] = outliers;
                }
                outliers.Enqueue(evidence.MonotonicMs);
                while (outliers.Count > 0 && evidence.MonotonicMs - outliers.Peek() > 3000) outliers.Dequeue();
                if (outliers.Count >= 3)
                {
                    RaiseOnce(evidence.MonotonicMs, "HUMAN_TIMING_OUTLIER:" + evidence.Identity, 3000, new DiagnosticAlert
                    {
                        TimeUtc = evidence.TimeUtc,
                        Severity = DiagnosticSeverity.Warning,
                        Code = "HUMAN_TIMING_OUTLIER",
                        VirtualKey = evidence.VirtualKey,
                        KeyName = KeyNames.Get(evidence.VirtualKey),
                        Message = "Dwell time lặp lại thấp bất thường so với nhịp gõ đã học của người đang test.",
                        Evidence = "dwell=" + dwellMs + " ms; ngưỡng thích nghi=" + before.AdaptiveLowerBoundMs.ToString("0.0") + " ms; median=" + before.MedianDwellMs.ToString("0.0") + " ms"
                    });
                }
            }

            if (dwellMs <= 1000)
            {
                _dwellSamples.Add(dwellMs);
                if (_dwellSamples.Count > 500) _dwellSamples.RemoveAt(0);
            }
            PublishHumanProfile();
        }

        private HumanTimingSnapshot BuildHumanProfile()
        {
            HumanTimingSnapshot snapshot = new HumanTimingSnapshot();
            snapshot.SampleCount = _dwellSamples.Count;
            if (_dwellSamples.Count == 0)
            {
                snapshot.AdaptiveLowerBoundMs = Settings.MinimumHumanDwellMs;
                snapshot.Status = "Chưa có mẫu";
                return snapshot;
            }

            double median = Median(_dwellSamples.Select(delegate(long value) { return (double)value; }).ToList());
            double mad = Median(_dwellSamples.Select(delegate(long value) { return Math.Abs(value - median); }).ToList());
            snapshot.MedianDwellMs = median;
            snapshot.MedianAbsoluteDeviationMs = mad;
            snapshot.AdaptiveLowerBoundMs = Math.Max(Settings.MinimumHumanDwellMs, median - 4.0 * Math.Max(5.0, mad));
            snapshot.Status = snapshot.SampleCount < Settings.AdaptiveSampleMinimum ? "Đang học " + snapshot.SampleCount + "/" + Settings.AdaptiveSampleMinimum : "Đã hiệu chuẩn";
            return snapshot;
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0) return 0;
            values.Sort();
            int middle = values.Count / 2;
            if ((values.Count & 1) == 1) return values[middle];
            return (values[middle - 1] + values[middle]) / 2.0;
        }

        private void PublishHumanProfile()
        {
            Action<HumanTimingSnapshot> changed = HumanProfileChanged;
            if (changed != null) changed(BuildHumanProfile());
        }

        public void ObservePollState(int virtualKey, bool isDown, long nowMs)
        {
            bool previous;
            if (!_pollStates.TryGetValue(virtualKey, out previous)) previous = false;
            if (previous == isDown) return;
            _pollStates[virtualKey] = isDown;
            _pollChangedMs[virtualKey] = nowMs;
        }

        public void Tick(long nowMs)
        {
            CheckPollMismatches(nowMs);
            foreach (KeyValuePair<string, KeyStateInfo> pair in _states)
            {
                KeyStateInfo state = pair.Value;
                if (state.IsDown && !state.StuckAlerted && nowMs - state.DownAtMs >= Settings.StuckThresholdMs)
                {
                    state.StuckAlerted = true;
                    RaiseOnce(nowMs, "STUCK:" + pair.Key, Settings.StuckThresholdMs, new DiagnosticAlert
                    {
                        TimeUtc = DateTime.UtcNow,
                        Severity = DiagnosticSeverity.Critical,
                        Code = "STUCK_KEY",
                        VirtualKey = state.VirtualKey,
                        KeyName = KeyNames.Get(state.VirtualKey),
                        Message = "Phím giữ trạng thái Down quá lâu; kiểm tra kẹt cơ hoặc chạm mạch.",
                        Evidence = "Giữ > " + Settings.StuckThresholdMs + " ms; repeat=" + state.RepeatCount + "; device=" + state.DeviceId
                    });
                    Action<KeyStateInfo> changed = StateChanged;
                    if (changed != null) changed(state);
                }
            }
        }

        private void CheckPollMismatches(long nowMs)
        {
            foreach (KeyValuePair<int, bool> poll in _pollStates)
            {
                long changedAt;
                if (!_pollChangedMs.TryGetValue(poll.Key, out changedAt) || nowMs - changedAt < Settings.PollMismatchMs) continue;

                long latestPhysical = -1;
                bool anyPhysicalDown = false;
                foreach (KeyStateInfo state in _states.Values)
                {
                    if (state.VirtualKey != poll.Key) continue;
                    latestPhysical = Math.Max(latestPhysical, state.LastPhysicalMs);
                    anyPhysicalDown = anyPhysicalDown || state.IsDown;
                }

                if (poll.Value == anyPhysicalDown) continue;
                if (latestPhysical >= 0 && nowMs - latestPhysical < Settings.PollMismatchMs) continue;

                if (poll.Value)
                {
                    RaiseOnce(nowMs, "POLL_ONLY:" + poll.Key, 1000, new DiagnosticAlert
                    {
                        TimeUtc = DateTime.UtcNow,
                        Severity = DiagnosticSeverity.Warning,
                        Code = "POLL_ONLY_DOWN",
                        VirtualKey = poll.Key,
                        KeyName = KeyNames.Get(poll.Key),
                        Message = "Windows báo phím đang Down nhưng luồng Raw Input chưa ghi nhận tương ứng.",
                        Evidence = "VK=0x" + poll.Key.ToString("X2") + "; sai lệch > " + Settings.PollMismatchMs + " ms"
                    });
                }
                else
                {
                    RaiseOnce(nowMs, "MISSING_UP:" + poll.Key, 1000, new DiagnosticAlert
                    {
                        TimeUtc = DateTime.UtcNow,
                        Severity = DiagnosticSeverity.Warning,
                        Code = "RAW_MISSING_KEYUP",
                        VirtualKey = poll.Key,
                        KeyName = KeyNames.Get(poll.Key),
                        Message = "Trạng thái Windows đã nhả nhưng Raw Input chưa có KeyUp; có thể mất gói/sự kiện.",
                        Evidence = "VK=0x" + poll.Key.ToString("X2")
                    });
                }
            }
        }

        private void ReleaseAllStates()
        {
            foreach (KeyStateInfo state in _states.Values)
            {
                state.IsDown = false;
                state.StuckAlerted = false;
            }
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

    internal static class DetectorSelfTests
    {
        private static int _passed;

        public static string RunAll()
        {
            _passed = 0;
            TestNormalPress();
            TestChatter();
            TestStuck();
            TestIdleContact();
            TestMatrixPhantom();
            TestMatrixPass();
            TestNonHumanPulse();
            TestEarlyRepeat();
            TestHumanProfileCalibration();
            TestPointerClick();
            TestPointerChatter();
            TestPointerStuck();
            return "SELF TEST: PASS\r\nTests: " + _passed + "/12\r\n";
        }

        private static KeyEvidence Raw(int vk, int scan, bool down, long ms)
        {
            return new KeyEvidence
            {
                TimeUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms),
                MonotonicMs = ms,
                Source = InputSourceKind.RawKeyboard,
                VirtualKey = vk,
                ScanCode = scan,
                IsDown = down,
                IsUp = !down,
                DeviceId = "TEST_DEVICE",
                Detail = "self-test"
            };
        }

        private static KeyEvidence Mouse(ushort flags, long ms)
        {
            return new KeyEvidence
            {
                TimeUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms),
                MonotonicMs = ms,
                Source = InputSourceKind.RawMouse,
                MouseButtonFlags = flags,
                DeviceId = "TEST_TOUCHPAD",
                Detail = "pointer self-test"
            };
        }

        private static void TestNormalPress()
        {
            KeyboardAnalyzer analyzer = new KeyboardAnalyzer();
            List<DiagnosticAlert> alerts = new List<DiagnosticAlert>();
            analyzer.AlertRaised += alerts.Add;
            analyzer.ProcessPrimary(Raw(0x41, 0x1E, true, 100));
            analyzer.ProcessPrimary(Raw(0x41, 0x1E, false, 200));
            Assert(alerts.Count == 0, "normal press generated an alert");
            Pass();
        }

        private static void TestChatter()
        {
            KeyboardAnalyzer analyzer = new KeyboardAnalyzer();
            List<DiagnosticAlert> alerts = new List<DiagnosticAlert>();
            analyzer.AlertRaised += alerts.Add;
            analyzer.ProcessPrimary(Raw(0x42, 0x30, true, 100));
            analyzer.ProcessPrimary(Raw(0x42, 0x30, false, 120));
            analyzer.ProcessPrimary(Raw(0x42, 0x30, true, 140));
            Assert(alerts.Exists(delegate(DiagnosticAlert a) { return a.Code == "CHATTER"; }), "chatter was not detected");
            Pass();
        }

        private static void TestStuck()
        {
            KeyboardAnalyzer analyzer = new KeyboardAnalyzer();
            List<DiagnosticAlert> alerts = new List<DiagnosticAlert>();
            analyzer.AlertRaised += alerts.Add;
            analyzer.Settings.StuckThresholdMs = 1000;
            analyzer.ProcessPrimary(Raw(0x43, 0x2E, true, 100));
            analyzer.Tick(1200);
            Assert(alerts.Exists(delegate(DiagnosticAlert a) { return a.Code == "STUCK_KEY"; }), "stuck key was not detected");
            Pass();
        }

        private static void TestIdleContact()
        {
            KeyboardAnalyzer analyzer = new KeyboardAnalyzer();
            List<DiagnosticAlert> alerts = new List<DiagnosticAlert>();
            analyzer.AlertRaised += alerts.Add;
            analyzer.Settings.IdleGraceMs = 100;
            analyzer.StartIdleContactTest(0);
            analyzer.ProcessPrimary(Raw(0x44, 0x20, true, 200));
            Assert(alerts.Exists(delegate(DiagnosticAlert a) { return a.Code == "IDLE_CONTACT"; }), "idle contact was not detected");
            Pass();
        }

        private static void TestMatrixPhantom()
        {
            KeyboardAnalyzer analyzer = new KeyboardAnalyzer();
            List<DiagnosticAlert> alerts = new List<DiagnosticAlert>();
            analyzer.AlertRaised += alerts.Add;
            analyzer.StartMatrixPairTest(new MatrixPairStep { FirstVirtualKey = 0x51, SecondVirtualKey = 0x41, Label = "Q + A" }, 0);
            analyzer.ProcessPrimary(Raw(0x51, 0x10, true, 100));
            analyzer.ProcessPrimary(Raw(0x41, 0x1E, true, 110));
            analyzer.ProcessPrimary(Raw(0x5A, 0x2C, true, 120));
            Assert(alerts.Exists(delegate(DiagnosticAlert a) { return a.Code == "PHANTOM_KEY"; }), "phantom key was not detected");
            Pass();
        }

        private static void TestMatrixPass()
        {
            KeyboardAnalyzer analyzer = new KeyboardAnalyzer();
            MatrixPairStep step = new MatrixPairStep { FirstVirtualKey = 0x57, SecondVirtualKey = 0x53, Label = "W + S" };
            analyzer.StartMatrixPairTest(step, 0);
            analyzer.ProcessPrimary(Raw(0x57, 0x11, true, 100));
            analyzer.ProcessPrimary(Raw(0x53, 0x1F, true, 110));
            Assert(step.Passed && !step.Failed, "valid matrix pair did not pass");
            Pass();
        }

        private static void TestNonHumanPulse()
        {
            KeyboardAnalyzer analyzer = new KeyboardAnalyzer();
            List<DiagnosticAlert> alerts = new List<DiagnosticAlert>();
            analyzer.AlertRaised += alerts.Add;
            analyzer.ProcessPrimary(Raw(0x45, 0x12, true, 100));
            analyzer.ProcessPrimary(Raw(0x45, 0x12, false, 105));
            analyzer.ProcessPrimary(Raw(0x45, 0x12, true, 205));
            analyzer.ProcessPrimary(Raw(0x45, 0x12, false, 210));
            Assert(alerts.Exists(delegate(DiagnosticAlert a) { return a.Code == "NON_HUMAN_PULSE"; }), "non-human pulse was not detected");
            Pass();
        }

        private static void TestEarlyRepeat()
        {
            KeyboardAnalyzer analyzer = new KeyboardAnalyzer();
            List<DiagnosticAlert> alerts = new List<DiagnosticAlert>();
            analyzer.AlertRaised += alerts.Add;
            analyzer.Settings.RepeatDelayMs = 500;
            analyzer.ProcessPrimary(Raw(0x46, 0x21, true, 100));
            analyzer.ProcessPrimary(Raw(0x46, 0x21, true, 200));
            Assert(alerts.Exists(delegate(DiagnosticAlert a) { return a.Code == "EARLY_REPEAT"; }), "early repeat was not detected");
            Pass();
        }

        private static void TestHumanProfileCalibration()
        {
            KeyboardAnalyzer analyzer = new KeyboardAnalyzer();
            long now = 100;
            for (int index = 0; index < 20; index++)
            {
                analyzer.ProcessPrimary(Raw(0x47, 0x22, true, now));
                analyzer.ProcessPrimary(Raw(0x47, 0x22, false, now + 80));
                now += 200;
            }
            HumanTimingSnapshot profile = analyzer.HumanProfile;
            Assert(profile.SampleCount == 20 && profile.Status == "Đã hiệu chuẩn", "human profile did not calibrate");
            Pass();
        }

        private static void TestPointerClick()
        {
            PointerAnalyzer analyzer = new PointerAnalyzer();
            List<DiagnosticAlert> alerts = new List<DiagnosticAlert>();
            analyzer.AlertRaised += alerts.Add;
            analyzer.Process(Mouse(NativeMethods.RI_MOUSE_LEFT_BUTTON_DOWN, 100));
            analyzer.Process(Mouse(NativeMethods.RI_MOUSE_LEFT_BUTTON_UP, 180));
            Assert(alerts.Count == 0 && analyzer.Snapshot.LeftClicks == 1 && !analyzer.Snapshot.LeftDown, "normal pointer click failed");
            Pass();
        }

        private static void TestPointerChatter()
        {
            PointerAnalyzer analyzer = new PointerAnalyzer();
            List<DiagnosticAlert> alerts = new List<DiagnosticAlert>();
            analyzer.AlertRaised += alerts.Add;
            analyzer.Process(Mouse(NativeMethods.RI_MOUSE_RIGHT_BUTTON_DOWN, 100));
            analyzer.Process(Mouse(NativeMethods.RI_MOUSE_RIGHT_BUTTON_UP, 130));
            analyzer.Process(Mouse(NativeMethods.RI_MOUSE_RIGHT_BUTTON_DOWN, 150));
            Assert(alerts.Exists(delegate(DiagnosticAlert a) { return a.Code == "TOUCHPAD_CHATTER"; }), "pointer chatter was not detected");
            Pass();
        }

        private static void TestPointerStuck()
        {
            PointerAnalyzer analyzer = new PointerAnalyzer();
            List<DiagnosticAlert> alerts = new List<DiagnosticAlert>();
            analyzer.AlertRaised += alerts.Add;
            analyzer.StuckThresholdMs = 1000;
            analyzer.Process(Mouse(NativeMethods.RI_MOUSE_LEFT_BUTTON_DOWN, 100));
            analyzer.Tick(1200);
            Assert(alerts.Exists(delegate(DiagnosticAlert a) { return a.Code == "TOUCHPAD_STUCK"; }), "stuck pointer button was not detected");
            Pass();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Pass()
        {
            _passed++;
        }
    }
}
