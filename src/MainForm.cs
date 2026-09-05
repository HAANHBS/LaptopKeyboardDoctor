using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LaptopKeyboardDoctor
{
    internal sealed class MainForm : Form
    {
        private sealed class DeviceChoice
        {
            public string Id;
            public string Label;
            public override string ToString() { return Label; }
        }

        private readonly RawInputReader _rawReader = new RawInputReader();
        private readonly KeyboardHook _hook = new KeyboardHook();
        private readonly KeyboardAnalyzer _analyzer = new KeyboardAnalyzer();
        private readonly PointerAnalyzer _pointerAnalyzer = new PointerAnalyzer();
        private readonly SessionSnapshot _session = new SessionSnapshot();
        private readonly Stopwatch _sessionClock = Stopwatch.StartNew();
        private readonly Timer _pollTimer = new Timer();
        private readonly Timer _guidedTimer = new Timer();
        private readonly List<MatrixPairStep> _matrixSteps = new List<MatrixPairStep>();
        private readonly Dictionary<int, long> _lastInjectedAlert = new Dictionary<int, long>();

        private bool _capturing = true;
        private bool _nativeReady;
        private int _idleSecondsRemaining;
        private int _matrixIndex = -1;
        private long _rawKeyboardCount;
        private long _hookCount;
        private long _hidCount;
        private long _rawMouseCount;

        private ToolStripButton _captureButton;
        private ToolStripLabel _captureStatus;
        private ToolStripComboBox _deviceCombo;
        private ToolStripStatusLabel _lastEventLabel;
        private TabControl _tabs;
        private TabPage _keyboardPage;
        private TabPage _guidedPage;
        private KeyboardMapControl _keyboardMap;
        private TouchpadMapControl _touchpadMap;
        private ComboBox _pointerDeviceCombo;
        private DataGridView _eventsGrid;
        private DataGridView _alertsGrid;
        private Label _eventCountLabel;
        private Label _alertCountLabel;
        private Label _deviceCountLabel;
        private Label _modeLabel;
        private Label _humanModelLabel;
        private Label _pointerMoveLabel;
        private Label _pointerLeftLabel;
        private Label _pointerRightLabel;
        private Label _pointerScrollLabel;
        private Label _pointerSourceLabel;
        private Label _idleInstruction;
        private Button _idleButton;
        private Label _matrixInstruction;
        private Label _matrixProgress;
        private Button _matrixButton;
        private NumericUpDown _chatterSetting;
        private NumericUpDown _stuckSetting;
        private NumericUpDown _humanDwellSetting;
        private Label _repeatInfoLabel;

        public MainForm()
        {
            Text = "Laptop Keyboard Doctor 2026";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 680);
            Size = new Size(1280, 820);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(239, 243, 248);
            try
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                Icon = SystemIcons.Shield;
            }

            BuildMatrixSteps();
            BuildInterface();
            WireEvents();

            _pollTimer.Interval = 20;
            _pollTimer.Tick += PollTimerTick;
            _guidedTimer.Interval = 1000;
            _guidedTimer.Tick += GuidedTimerTick;

            Shown += OnShown;
            FormClosing += OnFormClosing;
        }

        private long NowMs
        {
            get { return _sessionClock.ElapsedMilliseconds; }
        }

        private void BuildInterface()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(8),
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            Controls.Add(root);

            ToolStrip tools = new ToolStrip
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                BackColor = Color.White,
                Padding = new Padding(6, 4, 6, 4),
                RenderMode = ToolStripRenderMode.System
            };

            _captureButton = new ToolStripButton("Tạm dừng") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _captureStatus = new ToolStripLabel("● Đang bắt phím") { ForeColor = Color.FromArgb(31, 139, 78) };
            _deviceCombo = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 310 };
            _deviceCombo.Items.Add(new DeviceChoice { Id = string.Empty, Label = "Tất cả thiết bị bàn phím" });
            _deviceCombo.SelectedIndex = 0;
            ToolStripButton resetButton = new ToolStripButton("⟳ RESET / ĐO LẠI") { Font = new Font("Segoe UI Semibold", 9F), ForeColor = Color.FromArgb(180, 55, 35) };
            ToolStripButton exportButton = new ToolStripButton("Xuất báo cáo");
            ToolStripButton helpButton = new ToolStripButton("Giới hạn đo");

            tools.Items.Add(_captureButton);
            tools.Items.Add(_captureStatus);
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(new ToolStripLabel("Nguồn:"));
            tools.Items.Add(_deviceCombo);
            tools.Items.Add(resetButton);
            tools.Items.Add(exportButton);
            tools.Items.Add(helpButton);
            root.Controls.Add(tools, 0, 0);

            _captureButton.Click += ToggleCapture;
            resetButton.Click += ResetSession;
            exportButton.Click += ExportReport;
            helpButton.Click += ShowMeasurementLimits;
            _deviceCombo.SelectedIndexChanged += DeviceSelectionChanged;

            _tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 5) };
            _keyboardPage = BuildOverviewTab();
            _guidedPage = BuildGuidedTab();
            _tabs.TabPages.Add(_keyboardPage);
            _tabs.TabPages.Add(BuildTouchpadTab());
            _tabs.TabPages.Add(BuildEventsTab());
            _tabs.TabPages.Add(BuildAlertsTab());
            _tabs.TabPages.Add(_guidedPage);
            _tabs.TabPages.Add(BuildSettingsTab());
            _tabs.TabPages.Add(BuildAboutTab());
            root.Controls.Add(_tabs, 0, 1);

            StatusStrip status = new StatusStrip { Dock = DockStyle.Fill, SizingGrip = false };
            _lastEventLabel = new ToolStripStatusLabel("Khởi tạo bộ thu...") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            status.Items.Add(_lastEventLabel);
            status.Items.Add(new ToolStripStatusLabel("MÁY TÍNH HÀ ANH · Như Thanh, Thanh Hoá"));
            status.Items.Add(new ToolStripStatusLabel("Raw Input + Hook + Poll + HID Consumer"));
            root.Controls.Add(status, 0, 2);
        }

        private TabPage BuildOverviewTab()
        {
            TabPage page = new TabPage("Bàn phím") { BackColor = Color.White, Padding = new Padding(12) };
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));

            Label title = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Nhấn và nhả từng phím. Xanh lá: đã test · Xanh dương: đang Down · Đỏ/cam: có cảnh báo",
                Font = new Font("Segoe UI Semibold", 11F),
                ForeColor = Color.FromArgb(32, 48, 68),
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(title, 0, 0);

            _keyboardMap = new KeyboardMapControl { Dock = DockStyle.Fill };
            layout.Controls.Add(_keyboardMap, 0, 1);

            FlowLayoutPanel cards = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 10, 0, 0) };
            _eventCountLabel = AddCard(cards, "SỰ KIỆN", "0");
            _alertCountLabel = AddCard(cards, "CẢNH BÁO", "0");
            _deviceCountLabel = AddCard(cards, "THIẾT BỊ", "0");
            _modeLabel = AddCard(cards, "CHẾ ĐỘ", "Bình thường");
            _humanModelLabel = AddCard(cards, "MÔ HÌNH NGƯỜI", "Đang học 0/20");
            _humanModelLabel.Font = new Font("Segoe UI Semibold", 10F);
            layout.Controls.Add(cards, 0, 2);
            page.Controls.Add(layout);
            return page;
        }

        private static Label AddCard(FlowLayoutPanel parent, string heading, string value)
        {
            Panel card = new Panel { Width = 175, Height = 68, BackColor = Color.FromArgb(245, 248, 252), Margin = new Padding(0, 0, 10, 0) };
            Label head = new Label { Text = heading, AutoSize = true, Location = new Point(12, 8), ForeColor = Color.FromArgb(100, 111, 125), Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            Label number = new Label { Text = value, AutoSize = true, Location = new Point(12, 29), ForeColor = Color.FromArgb(31, 69, 111), Font = new Font("Segoe UI Semibold", 14F) };
            card.Controls.Add(head);
            card.Controls.Add(number);
            parent.Controls.Add(card);
            return number;
        }

        private TabPage BuildTouchpadTab()
        {
            TabPage page = new TabPage("Touchpad + nút chuột") { BackColor = Color.White, Padding = new Padding(12) };
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            Panel header = new Panel { Dock = DockStyle.Fill };
            Label instruction = new Label
            {
                Text = "Di ngón tay khắp bốn góc, cuộn hai chiều rồi bấm riêng nút trái/phải. Nếu có chuột USB, chọn đúng nguồn Raw Mouse ở bên phải.",
                Dock = DockStyle.Top,
                Height = 42,
                Font = new Font("Segoe UI Semibold", 10F),
                ForeColor = Color.FromArgb(32, 48, 68)
            };
            Button reset = new Button
            {
                Text = "⟳ RESET / ĐO LẠI",
                Dock = DockStyle.Right,
                Width = 170,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(245, 231, 225),
                ForeColor = Color.FromArgb(165, 50, 35),
                Font = new Font("Segoe UI Semibold", 9F)
            };
            reset.Click += ResetSession;
            header.Controls.Add(reset);
            header.Controls.Add(instruction);
            root.Controls.Add(header, 0, 0);
            root.SetColumnSpan(header, 2);

            _touchpadMap = new TouchpadMapControl { Dock = DockStyle.Fill, Margin = new Padding(4, 8, 14, 4) };
            root.Controls.Add(_touchpadMap, 0, 1);

            TableLayoutPanel details = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8, ColumnCount = 1, Padding = new Padding(10) };
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            details.Controls.Add(new Label { Text = "Nguồn touchpad/chuột", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 9F) }, 0, 0);
            _pointerDeviceCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _pointerDeviceCombo.Items.Add(new DeviceChoice { Id = string.Empty, Label = "Tất cả nguồn Raw Mouse" });
            _pointerDeviceCombo.SelectedIndex = 0;
            _pointerDeviceCombo.SelectedIndexChanged += PointerDeviceSelectionChanged;
            details.Controls.Add(_pointerDeviceCombo, 0, 1);

            _pointerMoveLabel = MakeMetricLabel("Di chuyển: 0");
            _pointerLeftLabel = MakeMetricLabel("Nút trái: 0 · UP");
            _pointerRightLabel = MakeMetricLabel("Nút phải: 0 · UP");
            _pointerScrollLabel = MakeMetricLabel("Cuộn: 0");
            details.Controls.Add(_pointerMoveLabel, 0, 2);
            details.Controls.Add(_pointerLeftLabel, 0, 3);
            details.Controls.Add(_pointerRightLabel, 0, 4);
            details.Controls.Add(_pointerScrollLabel, 0, 5);
            _pointerSourceLabel = new Label { Text = "Chưa nhận tín hiệu. Hãy chỉ di touchpad để nhận diện đúng device path.", Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Color.FromArgb(95, 105, 120) };
            details.Controls.Add(_pointerSourceLabel, 0, 6);
            details.Controls.Add(new Label
            {
                Text = "Cảnh báo riêng: TOUCHPAD_CHATTER và TOUCHPAD_STUCK. Một số Precision Touchpad chỉ xuất luồng con trỏ hợp nhất từ Windows; khi đó device có thể hiện SYSTEM và không tách được với chuột ngoài.",
                Dock = DockStyle.Fill,
                AutoSize = false,
                ForeColor = Color.FromArgb(90, 70, 45)
            }, 0, 7);

            root.Controls.Add(details, 1, 1);
            page.Controls.Add(root);
            return page;
        }

        private static Label MakeMetricLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(245, 248, 252),
                ForeColor = Color.FromArgb(31, 69, 111),
                Font = new Font("Segoe UI Semibold", 11F),
                Padding = new Padding(10, 0, 0, 0),
                Margin = new Padding(0, 3, 0, 3)
            };
        }

        private TabPage BuildEventsTab()
        {
            TabPage page = new TabPage("Luồng sự kiện") { Padding = new Padding(8) };
            _eventsGrid = CreateGrid();
            _eventsGrid.Columns.Add("Time", "Giờ");
            _eventsGrid.Columns.Add("Source", "Nguồn");
            _eventsGrid.Columns.Add("Device", "Thiết bị");
            _eventsGrid.Columns.Add("Key", "Phím");
            _eventsGrid.Columns.Add("Vk", "VK");
            _eventsGrid.Columns.Add("Scan", "Scan");
            _eventsGrid.Columns.Add("Action", "Trạng thái");
            _eventsGrid.Columns.Add("Flags", "Chi tiết");
            _eventsGrid.Columns[0].Width = 100;
            _eventsGrid.Columns[1].Width = 110;
            _eventsGrid.Columns[2].Width = 230;
            _eventsGrid.Columns[3].Width = 120;
            _eventsGrid.Columns[4].Width = 60;
            _eventsGrid.Columns[5].Width = 70;
            _eventsGrid.Columns[6].Width = 85;
            _eventsGrid.Columns[7].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            page.Controls.Add(_eventsGrid);
            return page;
        }

        private TabPage BuildAlertsTab()
        {
            TabPage page = new TabPage("Cảnh báo") { Padding = new Padding(8) };
            _alertsGrid = CreateGrid();
            _alertsGrid.Columns.Add("Time", "Giờ");
            _alertsGrid.Columns.Add("Level", "Mức");
            _alertsGrid.Columns.Add("Code", "Mã lỗi");
            _alertsGrid.Columns.Add("Key", "Phím");
            _alertsGrid.Columns.Add("Message", "Nhận định");
            _alertsGrid.Columns.Add("Evidence", "Bằng chứng");
            _alertsGrid.Columns[0].Width = 100;
            _alertsGrid.Columns[1].Width = 85;
            _alertsGrid.Columns[2].Width = 145;
            _alertsGrid.Columns[3].Width = 110;
            _alertsGrid.Columns[4].Width = 330;
            _alertsGrid.Columns[5].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            page.Controls.Add(_alertsGrid);
            return page;
        }

        private TabPage BuildGuidedTab()
        {
            TabPage page = new TabPage("Test chuyên sâu") { BackColor = Color.White, Padding = new Padding(16) };
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2, Padding = new Padding(8) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

            GroupBox idleGroup = new GroupBox { Text = "1. Dò phím tự chạm / tự phát", Dock = DockStyle.Fill, Padding = new Padding(14) };
            _idleInstruction = new Label
            {
                Dock = DockStyle.Top,
                Height = 110,
                Text = "Chọn đúng thiết bị ở thanh trên, bấm Bắt đầu rồi KHÔNG chạm bàn phím trong 30 giây. Mọi tín hiệu vật lý sau thời gian ổn định sẽ bị đánh dấu nghi lỗi.",
                AutoEllipsis = true
            };
            _idleButton = MakeActionButton("Bắt đầu 30 giây");
            _idleButton.Click += StartIdleTest;
            idleGroup.Controls.Add(_idleButton);
            idleGroup.Controls.Add(_idleInstruction);
            layout.Controls.Add(idleGroup, 0, 0);

            GroupBox matrixGroup = new GroupBox { Text = "2. Dò chạm ma trận / phím ma", Dock = DockStyle.Fill, Padding = new Padding(14) };
            _matrixInstruction = new Label
            {
                Dock = DockStyle.Top,
                Height = 82,
                Text = "Ứng dụng sẽ yêu cầu giữ đúng hai phím. Nếu Windows nhận thêm phím thứ ba, phiên test ghi PHANTOM_KEY.",
                Font = new Font("Segoe UI", 9.5F)
            };
            _matrixProgress = new Label { Dock = DockStyle.Top, Height = 36, Text = "Chưa bắt đầu", Font = new Font("Segoe UI Semibold", 12F), ForeColor = Color.FromArgb(31, 89, 160) };
            _matrixButton = MakeActionButton("Bắt đầu cặp đầu tiên");
            _matrixButton.Click += NextMatrixStep;
            matrixGroup.Controls.Add(_matrixButton);
            matrixGroup.Controls.Add(_matrixProgress);
            matrixGroup.Controls.Add(_matrixInstruction);
            layout.Controls.Add(matrixGroup, 1, 0);

            GroupBox stressGroup = new GroupBox { Text = "3. Quy trình stress thực tế", Dock = DockStyle.Fill, Padding = new Padding(14) };
            Label stress = new Label
            {
                Dock = DockStyle.Fill,
                Text = "• Gõ nhanh toàn bộ các hàng phím 3 lượt.\r\n• Giữ từng phím 3 giây để dò KeyUp mất và repeat storm.\r\n• Thử Shift/Ctrl/Alt + từng phím chức năng.\r\n• Thử khi máy nóng và lắc nhẹ cụm palmrest/cáp bàn phím.\r\n• Xuất báo cáo ngay khi xuất hiện lỗi để giữ scan code và thiết bị nguồn.",
                Font = new Font("Segoe UI", 9.5F)
            };
            stressGroup.Controls.Add(stress);
            layout.Controls.Add(stressGroup, 0, 1);

            GroupBox modernGroup = new GroupBox { Text = "4. Phím Fn, media và máy đời mới", Dock = DockStyle.Fill, Padding = new Padding(14) };
            Label modern = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Raw HID ghi cả gói Consumer Control chưa ánh xạ; hook ghi Volume/Media/Browser/F13–F24/Copilot khi Windows phát mã. Fn thuần firmware không có mã độc lập: hãy thử Fn cùng tăng/giảm sáng, âm lượng hoặc phím chức năng. Ctrl+Alt+Del thuộc Secure Attention Sequence nên ứng dụng thường không được nhận đầy đủ.",
                Font = new Font("Segoe UI", 9.5F)
            };
            modernGroup.Controls.Add(modern);
            layout.Controls.Add(modernGroup, 1, 1);

            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildSettingsTab()
        {
            TabPage page = new TabPage("Thiết lập") { BackColor = Color.White, Padding = new Padding(22) };
            TableLayoutPanel form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 6 };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));

            _chatterSetting = new NumericUpDown { Minimum = 10, Maximum = 150, Value = _analyzer.Settings.ChatterWindowMs, Width = 120 };
            _stuckSetting = new NumericUpDown { Minimum = 500, Maximum = 10000, Increment = 250, Value = _analyzer.Settings.StuckThresholdMs, Width = 120 };
            _humanDwellSetting = new NumericUpDown { Minimum = 5, Maximum = 30, Value = _analyzer.Settings.MinimumHumanDwellMs, Width = 120 };
            AddSettingRow(form, 0, "Ngưỡng chatter/double key (ms)", _chatterSetting);
            AddSettingRow(form, 1, "Ngưỡng phím giữ/kẹt (ms)", _stuckSetting);
            AddSettingRow(form, 2, "Xung cực ngắn, không giống người (ms)", _humanDwellSetting);

            _repeatInfoLabel = new Label { Text = "Windows typematic: sẽ đọc khi ứng dụng khởi động", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(70, 90, 115) };
            form.Controls.Add(_repeatInfoLabel, 0, 3);
            form.SetColumnSpan(_repeatInfoLabel, 2);

            Label note = new Label
            {
                Dock = DockStyle.Fill,
                Height = 160,
                AutoSize = true,
                MaximumSize = new Size(720, 0),
                Text = "Mô hình thời gian dùng dwell time, nhịp lặp, burst nhiều phím và median/MAD thích nghi sau 20 lần nhấn. Cảnh báo mạnh chỉ phát khi có bằng chứng lặp; một lần gõ nhanh đơn lẻ không đủ kết luận. Ứng dụng không chặn phím và không ghi nội dung văn bản—chỉ lưu tên/mã/trạng thái phím phục vụ chẩn đoán."
            };
            form.Controls.Add(note, 0, 4);
            form.SetColumnSpan(note, 2);
            page.Controls.Add(form);

            _chatterSetting.ValueChanged += delegate { _analyzer.Settings.ChatterWindowMs = (int)_chatterSetting.Value; _pointerAnalyzer.ChatterWindowMs = (int)_chatterSetting.Value; };
            _stuckSetting.ValueChanged += delegate { _analyzer.Settings.StuckThresholdMs = (int)_stuckSetting.Value; _pointerAnalyzer.StuckThresholdMs = (int)_stuckSetting.Value; };
            _humanDwellSetting.ValueChanged += delegate { _analyzer.Settings.MinimumHumanDwellMs = (int)_humanDwellSetting.Value; };
            return page;
        }

        private TabPage BuildAboutTab()
        {
            TabPage page = new TabPage("Giới thiệu") { BackColor = Color.White, Padding = new Padding(28) };
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 6,
                MaximumSize = new Size(900, 0)
            };

            Label title = new Label
            {
                Text = "Laptop Keyboard Doctor 2026",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 22F),
                ForeColor = Color.FromArgb(31, 69, 111),
                Margin = new Padding(0, 0, 0, 8)
            };
            Label version = new Label
            {
                Text = "Phiên bản " + Application.ProductVersion + " · Windows native · Portable một tệp EXE",
                AutoSize = true,
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(85, 100, 118),
                Margin = new Padding(0, 0, 0, 22)
            };
            Label author = new Label
            {
                Text = "Tác giả / đơn vị phát hành: MÁY TÍNH HÀ ANH\r\nĐịa chỉ: xã Như Thanh, tỉnh Thanh Hoá",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 12F),
                ForeColor = Color.FromArgb(32, 48, 68),
                Margin = new Padding(0, 0, 0, 20)
            };
            Label license = new Label
            {
                Text = "Phần mềm nguồn mở theo giấy phép MIT. Có thể sử dụng, sao chép, sửa đổi và phân phối lại theo các điều khoản trong tệp LICENSE đi kèm mã nguồn.",
                AutoSize = true,
                MaximumSize = new Size(820, 0),
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(70, 82, 96),
                Margin = new Padding(0, 0, 0, 14)
            };
            LinkLabel repository = new LinkLabel
            {
                Text = "Mã nguồn: https://github.com/HAANHBS/LaptopKeyboardDoctor",
                AutoSize = true,
                Font = new Font("Segoe UI", 10F),
                LinkColor = Color.FromArgb(31, 89, 160),
                Margin = new Padding(0, 0, 0, 18)
            };
            repository.LinkClicked += delegate
            {
                try { Process.Start("https://github.com/HAANHBS/LaptopKeyboardDoctor"); }
                catch { }
            };
            Label privacy = new Label
            {
                Text = "Ứng dụng hoạt động ngoại tuyến, không gửi dữ liệu và không lưu nội dung văn bản đã gõ.",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(90, 70, 45)
            };

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(version, 0, 1);
            layout.Controls.Add(author, 0, 2);
            layout.Controls.Add(license, 0, 3);
            layout.Controls.Add(repository, 0, 4);
            layout.Controls.Add(privacy, 0, 5);
            page.Controls.Add(layout);
            return page;
        }

        private static void AddSettingRow(TableLayoutPanel panel, int row, string label, Control editor)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            panel.Controls.Add(editor, 1, row);
        }

        private static Button MakeActionButton(string text)
        {
            return new Button
            {
                Text = text,
                Dock = DockStyle.Bottom,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(39, 111, 191),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9.5F),
                Cursor = Cursors.Hand
            };
        }

        private static DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = true,
                RowHeadersVisible = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None
            };
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
            return grid;
        }

        private void WireEvents()
        {
            _hook.Evidence += delegate(KeyEvidence evidence)
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((MethodInvoker)delegate { AddEvidence(evidence); });
            };
            _analyzer.AlertRaised += AddAlert;
            _analyzer.StateChanged += delegate(KeyStateInfo state) { _keyboardMap.UpdateState(state); };
            _analyzer.HumanProfileChanged += UpdateHumanProfile;
            _pointerAnalyzer.AlertRaised += AddAlert;
            _pointerAnalyzer.StateChanged += UpdatePointerState;
            _analyzer.MatrixStepChanged += delegate(MatrixPairStep step)
            {
                if (step.Failed)
                {
                    _matrixProgress.ForeColor = Color.FromArgb(190, 45, 45);
                    _matrixProgress.Text = "PHÁT HIỆN PHÍM THỨ BA — " + step.Label;
                }
                else if (step.Passed)
                {
                    _matrixProgress.ForeColor = Color.FromArgb(31, 139, 78);
                    _matrixProgress.Text = "PASS " + step.Label + " — nhả phím rồi bấm Cặp tiếp theo";
                }
            };
        }

        private void OnShown(object sender, EventArgs e)
        {
            try
            {
                int repeatDelayMs;
                double repeatRateHz;
                RawInputReader.ReadKeyboardRepeatSettings(out repeatDelayMs, out repeatRateHz);
                _analyzer.Settings.RepeatDelayMs = repeatDelayMs;
                _analyzer.Settings.ExpectedRepeatRateHz = repeatRateHz;
                _analyzer.Settings.RepeatStormCount = (int)Math.Ceiling(repeatRateHz * 1.4) + 2;
                _repeatInfoLabel.Text = "Windows typematic: delay≈" + repeatDelayMs + " ms · rate≈" + repeatRateHz.ToString("0.0") + " lần/giây · cảnh báo storm≥" + _analyzer.Settings.RepeatStormCount + "/giây";
                _rawReader.Register(Handle);
                _hook.Start();
                _nativeReady = true;
                _pollTimer.Start();
                _captureStatus.Text = "● Đang bắt phím";
                _captureStatus.ForeColor = Color.FromArgb(31, 139, 78);
                _lastEventLabel.Text = "Sẵn sàng. Nên chọn đúng thiết bị trước khi chạy test chuyên sâu.";
            }
            catch (Exception ex)
            {
                _nativeReady = false;
                _captureStatus.Text = "● Không khởi tạo được";
                _captureStatus.ForeColor = Color.FromArgb(190, 45, 45);
                MessageBox.Show(ex.Message + "\r\n\r\nHãy đóng phần mềm bắt phím khác và chạy lại. Không cần quyền Administrator trong cấu hình bình thường.", "Không mở được bộ thu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WM_INPUT && _capturing)
            {
                KeyEvidence evidence = _rawReader.Read(message.LParam, NowMs);
                if (evidence != null) AddEvidence(evidence);
            }
            else if (message.Msg == NativeMethods.WM_INPUT_DEVICE_CHANGE)
            {
                string name = _rawReader.GetDeviceName(message.LParam);
                bool arrival = message.WParam.ToInt64() == 1;
                AddEvidence(new KeyEvidence
                {
                    TimeUtc = DateTime.UtcNow,
                    MonotonicMs = NowMs,
                    Source = InputSourceKind.DeviceChange,
                    DeviceId = name,
                    Detail = arrival ? "Thiết bị kết nối" : "Thiết bị ngắt kết nối"
                });
                if (!arrival) _rawReader.ForgetDevice(message.LParam);
            }
            else if (message.Msg == NativeMethods.WM_APPCOMMAND && _capturing)
            {
                int command = (int)((message.LParam.ToInt64() >> 16) & 0x7FF);
                AddEvidence(new KeyEvidence
                {
                    TimeUtc = DateTime.UtcNow,
                    MonotonicMs = NowMs,
                    Source = InputSourceKind.AppCommand,
                    DeviceId = "WM_APPCOMMAND",
                    Detail = "Consumer command=" + command
                });
            }

            base.WndProc(ref message);
        }

        private void AddEvidence(KeyEvidence evidence)
        {
            if (!_capturing && evidence.Source != InputSourceKind.DeviceChange) return;
            if (_session.Events.Count >= 100000) _session.Events.RemoveRange(0, 1000);
            _session.Events.Add(evidence);

            if (evidence.Source == InputSourceKind.RawKeyboard)
            {
                _rawKeyboardCount++;
                RegisterDevice(evidence.DeviceId, "Keyboard");
                _analyzer.ProcessPrimary(evidence);
            }
            else if (evidence.Source == InputSourceKind.LowLevelHook)
            {
                _hookCount++;
                if (evidence.Injected && evidence.IsDown) AlertInjected(evidence);
            }
            else if (evidence.Source == InputSourceKind.RawHid)
            {
                _hidCount++;
                RegisterDevice(evidence.DeviceId, "Consumer HID");
                if (_analyzer.IsIdleMonitoring(evidence.MonotonicMs))
                {
                    AddAlert(new DiagnosticAlert
                    {
                        TimeUtc = evidence.TimeUtc,
                        Severity = DiagnosticSeverity.Critical,
                        Code = "IDLE_HID_CONTACT",
                        VirtualKey = 0,
                        KeyName = "HID/Media/Fn",
                        Message = "Có gói Consumer HID trong lúc không chạm bàn phím.",
                        Evidence = evidence.Detail + "; device=" + evidence.DeviceId
                    });
                }
            }
            else if (evidence.Source == InputSourceKind.RawMouse)
            {
                _rawMouseCount++;
                RegisterDevice(evidence.DeviceId, "Touchpad/Mouse");
                if (PointerEvidenceSelected(evidence))
                {
                    _pointerAnalyzer.Process(evidence);
                    _touchpadMap.Apply(evidence, _pointerAnalyzer.Snapshot);
                    _pointerSourceLabel.Text = "Nguồn cuối: " + FriendlyDeviceName(evidence.DeviceId);
                }
            }
            else if (evidence.Source == InputSourceKind.DeviceChange)
            {
                RegisterDevice(evidence.DeviceId, "Device");
            }

            AddEventRow(evidence);
            UpdateCounters();
            string eventName = !string.IsNullOrEmpty(evidence.ControlName) ? evidence.ControlName : evidence.VirtualKey == 0 ? evidence.Detail : KeyNames.ScanLabel(evidence.VirtualKey, evidence.ScanCode, evidence.Extended);
            _lastEventLabel.Text = evidence.TimeUtc.ToLocalTime().ToString("HH:mm:ss.fff") + " · " + SourceText(evidence.Source) + " · " + eventName;
        }

        private void AlertInjected(KeyEvidence evidence)
        {
            long last;
            if (_lastInjectedAlert.TryGetValue(evidence.VirtualKey, out last) && evidence.MonotonicMs - last < 1000) return;
            _lastInjectedAlert[evidence.VirtualKey] = evidence.MonotonicMs;
            AddAlert(new DiagnosticAlert
            {
                TimeUtc = evidence.TimeUtc,
                Severity = DiagnosticSeverity.Info,
                Code = "SOFTWARE_INJECTED",
                VirtualKey = evidence.VirtualKey,
                KeyName = KeyNames.Get(evidence.VirtualKey),
                Message = "Sự kiện được Windows đánh dấu là do phần mềm chèn, không nên kết luận hỏng bàn phím.",
                Evidence = evidence.Detail
            });
        }

        private void RegisterDevice(string id, string type)
        {
            if (string.IsNullOrEmpty(id)) id = "UNKNOWN";
            DeviceRecord record;
            if (!_session.Devices.TryGetValue(id, out record))
            {
                record = new DeviceRecord { Handle = id, Name = FriendlyDeviceName(id), Type = type, LastSeenUtc = DateTime.UtcNow };
                _session.Devices[id] = record;
            }
            else if (record.Type == "Device" && type != "Device")
            {
                record.Type = type;
            }
            record.LastSeenUtc = DateTime.UtcNow;
            record.EventCount++;
            if (type == "Keyboard" || type == "Consumer HID") AddDeviceChoice(_deviceCombo, id, record.Name);
            if (type == "Touchpad/Mouse") AddDeviceChoice(_pointerDeviceCombo, id, record.Name);
        }

        private static void AddDeviceChoice(ComboBox combo, string id, string label)
        {
            if (combo == null) return;
            foreach (object item in combo.Items)
            {
                DeviceChoice choice = item as DeviceChoice;
                if (choice != null && string.Equals(choice.Id, id, StringComparison.OrdinalIgnoreCase)) return;
            }
            combo.Items.Add(new DeviceChoice { Id = id, Label = label });
        }

        private static void AddDeviceChoice(ToolStripComboBox combo, string id, string label)
        {
            if (combo == null) return;
            foreach (object item in combo.Items)
            {
                DeviceChoice choice = item as DeviceChoice;
                if (choice != null && string.Equals(choice.Id, id, StringComparison.OrdinalIgnoreCase)) return;
            }
            combo.Items.Add(new DeviceChoice { Id = id, Label = label });
        }

        private static string FriendlyDeviceName(string id)
        {
            string kind = id.IndexOf("ACPI", StringComparison.OrdinalIgnoreCase) >= 0 ? "Nội bộ/ACPI" :
                id.IndexOf("HID", StringComparison.OrdinalIgnoreCase) >= 0 ? "HID/USB" : "Thiết bị";
            string compact = id.Replace("\\\\?\\", string.Empty);
            if (compact.Length > 52) compact = "..." + compact.Substring(compact.Length - 49);
            return kind + " · " + compact;
        }

        private void AddEventRow(KeyEvidence evidence)
        {
            int index = _eventsGrid.Rows.Add(
                evidence.TimeUtc.ToLocalTime().ToString("HH:mm:ss.fff"),
                SourceText(evidence.Source),
                Shorten(evidence.DeviceId, 42),
                !string.IsNullOrEmpty(evidence.ControlName) ? evidence.ControlName : evidence.VirtualKey == 0 ? "—" : KeyNames.Get(evidence.VirtualKey),
                evidence.VirtualKey == 0 ? "—" : "0x" + evidence.VirtualKey.ToString("X2"),
                evidence.ScanCode == 0 ? "—" : "0x" + evidence.ScanCode.ToString("X3"),
                evidence.IsDown ? "DOWN" : evidence.IsUp ? "UP" : "INFO",
                (evidence.Injected ? "INJECTED; " : string.Empty) + evidence.Detail);
            _eventsGrid.Rows[index].DefaultCellStyle.BackColor = evidence.Source == InputSourceKind.RawKeyboard ? Color.FromArgb(240, 248, 255) :
                evidence.Source == InputSourceKind.RawMouse ? Color.FromArgb(246, 241, 255) : Color.White;
            if (_eventsGrid.Rows.Count > 2000) _eventsGrid.Rows.RemoveAt(0);
            if (_eventsGrid.Rows.Count > 0) _eventsGrid.FirstDisplayedScrollingRowIndex = _eventsGrid.Rows.Count - 1;
        }

        private void AddAlert(DiagnosticAlert alert)
        {
            _session.Alerts.Add(alert);
            int index = _alertsGrid.Rows.Add(
                alert.TimeUtc.ToLocalTime().ToString("HH:mm:ss.fff"),
                SeverityText(alert.Severity),
                alert.Code,
                alert.KeyName,
                alert.Message,
                alert.Evidence);
            Color color = alert.Severity == DiagnosticSeverity.Critical ? Color.FromArgb(255, 224, 224) :
                alert.Severity == DiagnosticSeverity.Warning ? Color.FromArgb(255, 242, 207) : Color.FromArgb(232, 242, 255);
            _alertsGrid.Rows[index].DefaultCellStyle.BackColor = color;
            if (_alertsGrid.Rows.Count > 2000) _alertsGrid.Rows.RemoveAt(0);
            if (_alertsGrid.Rows.Count > 0) _alertsGrid.FirstDisplayedScrollingRowIndex = _alertsGrid.Rows.Count - 1;
            if (alert.VirtualKey != 0) _keyboardMap.MarkAlert(alert.VirtualKey, alert.Severity);
            UpdateCounters();
            System.Media.SystemSounds.Exclamation.Play();
        }

        private void PollTimerTick(object sender, EventArgs e)
        {
            if (!_capturing || !_nativeReady) return;
            long now = NowMs;
            for (int virtualKey = 8; virtualKey <= 254; virtualKey++)
            {
                if (virtualKey == 0x10 || virtualKey == 0x11 || virtualKey == 0x12) continue;
                bool down = (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
                _analyzer.ObservePollState(virtualKey, down, now);
            }
            _analyzer.Tick(now);
            _pointerAnalyzer.Tick(now);
        }

        private void GuidedTimerTick(object sender, EventArgs e)
        {
            if (_idleSecondsRemaining <= 0)
            {
                _guidedTimer.Stop();
                _analyzer.StopGuidedTest();
                _modeLabel.Text = "Bình thường";
                _idleButton.Enabled = true;
                _idleButton.Text = "Chạy lại 30 giây";
                _idleInstruction.Text = "Đã hoàn tất. Xem tab Cảnh báo; nếu không có IDLE_CONTACT/IDLE_HID_CONTACT thì bài test đứng yên đạt.";
                return;
            }

            _idleSecondsRemaining--;
            _idleButton.Text = "Không chạm — còn " + _idleSecondsRemaining + " giây";
        }

        private void StartIdleTest(object sender, EventArgs e)
        {
            if (_session.Devices.Count > 1 && string.IsNullOrEmpty(_analyzer.ActiveDeviceId))
            {
                DialogResult result = MessageBox.Show("Đang chọn Tất cả thiết bị. Bàn phím USB khác cũng có thể tạo cảnh báo. Tiếp tục?", "Chọn nguồn kiểm tra", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }
            _matrixIndex = -1;
            _idleSecondsRemaining = 30;
            _idleButton.Enabled = false;
            _idleButton.Text = "Ổn định 1 giây...";
            _idleInstruction.Text = "Đang đo. KHÔNG chạm bàn phím, không nhấn phím media và không dùng thiết bị nhập đã chọn.";
            _modeLabel.Text = "Dò tự chạm";
            _analyzer.StartIdleContactTest(NowMs);
            _guidedTimer.Start();
        }

        private void NextMatrixStep(object sender, EventArgs e)
        {
            _guidedTimer.Stop();
            _idleSecondsRemaining = 0;
            _idleButton.Enabled = true;
            _matrixIndex++;
            if (_matrixIndex >= _matrixSteps.Count)
            {
                int passed = _matrixSteps.Count(delegate(MatrixPairStep item) { return item.Passed && !item.Failed; });
                int failed = _matrixSteps.Count(delegate(MatrixPairStep item) { return item.Failed; });
                _matrixProgress.Text = "Hoàn tất: " + passed + " PASS · " + failed + " nghi lỗi";
                _matrixProgress.ForeColor = failed == 0 ? Color.FromArgb(31, 139, 78) : Color.FromArgb(190, 45, 45);
                _matrixButton.Text = "Chạy lại từ đầu";
                _matrixIndex = -1;
                _analyzer.StopGuidedTest();
                _modeLabel.Text = "Bình thường";
                return;
            }

            MatrixPairStep step = _matrixSteps[_matrixIndex];
            step.Passed = false;
            step.Failed = false;
            _analyzer.StartMatrixPairTest(step, NowMs);
            _modeLabel.Text = "Ma trận " + (_matrixIndex + 1) + "/" + _matrixSteps.Count;
            _matrixProgress.ForeColor = Color.FromArgb(31, 89, 160);
            _matrixProgress.Text = "GIỮ ĐÚNG: " + step.Label;
            _matrixButton.Text = "Cặp tiếp theo";
        }

        private void BuildMatrixSteps()
        {
            AddPair(0x51, 0x41); AddPair(0x57, 0x53); AddPair(0x45, 0x44); AddPair(0x52, 0x46);
            AddPair(0x54, 0x47); AddPair(0x59, 0x48); AddPair(0x55, 0x4A); AddPair(0x49, 0x4B);
            AddPair(0x4F, 0x4C); AddPair(0x50, 0xBA); AddPair(0x41, 0x5A); AddPair(0x53, 0x58);
            AddPair(0x44, 0x43); AddPair(0x46, 0x56); AddPair(0x47, 0x42); AddPair(0x48, 0x4E);
            AddPair(0x4A, 0x4D); AddPair(0x31, 0x51); AddPair(0x35, 0x54); AddPair(0x39, 0x4F);
        }

        private void AddPair(int first, int second)
        {
            _matrixSteps.Add(new MatrixPairStep { FirstVirtualKey = first, SecondVirtualKey = second, Label = KeyNames.Get(first) + " + " + KeyNames.Get(second) });
        }

        private void ToggleCapture(object sender, EventArgs e)
        {
            _capturing = !_capturing;
            _captureButton.Text = _capturing ? "Tạm dừng" : "Tiếp tục";
            _captureStatus.Text = _capturing ? "● Đang bắt phím" : "● Đã tạm dừng";
            _captureStatus.ForeColor = _capturing ? Color.FromArgb(31, 139, 78) : Color.FromArgb(150, 100, 30);
            if (!_capturing)
            {
                _pollTimer.Stop();
                _guidedTimer.Stop();
                _analyzer.StopGuidedTest();
            }
            else if (_nativeReady)
            {
                _pollTimer.Start();
            }
        }

        private void ResetSession(object sender, EventArgs e)
        {
            _guidedTimer.Stop();
            _idleSecondsRemaining = 0;
            _session.Events.Clear();
            _session.Alerts.Clear();
            _eventsGrid.Rows.Clear();
            _alertsGrid.Rows.Clear();
            _analyzer.Reset();
            _pointerAnalyzer.Reset();
            _keyboardMap.ResetMap();
            _touchpadMap.ResetMap();
            _lastInjectedAlert.Clear();
            _rawKeyboardCount = 0;
            _hookCount = 0;
            _hidCount = 0;
            _rawMouseCount = 0;
            _matrixIndex = -1;
            foreach (MatrixPairStep step in _matrixSteps) { step.Passed = false; step.Failed = false; }
            _modeLabel.Text = "Bình thường";
            _humanModelLabel.Text = "Đang học 0/20";
            _idleButton.Enabled = true;
            _idleButton.Text = "Bắt đầu 30 giây";
            _idleInstruction.Text = "Đã reset. Chọn đúng thiết bị, bấm Bắt đầu rồi không chạm bàn phím trong 30 giây.";
            _matrixProgress.Text = "Chưa bắt đầu";
            _matrixButton.Text = "Bắt đầu cặp đầu tiên";
            foreach (DeviceRecord device in _session.Devices.Values) device.EventCount = 0;
            UpdateCounters();
            _lastEventLabel.Text = "Đã RESET phiên đo; thiết bị và ngưỡng được giữ nguyên.";
        }

        private void DeviceSelectionChanged(object sender, EventArgs e)
        {
            DeviceChoice choice = _deviceCombo.SelectedItem as DeviceChoice;
            _analyzer.ActiveDeviceId = choice == null ? string.Empty : choice.Id;
        }

        private void PointerDeviceSelectionChanged(object sender, EventArgs e)
        {
            DeviceChoice choice = _pointerDeviceCombo.SelectedItem as DeviceChoice;
            _pointerAnalyzer.ActiveDeviceId = choice == null ? string.Empty : choice.Id;
            _pointerAnalyzer.Reset();
            _touchpadMap.ResetMap();
            _pointerSourceLabel.Text = string.IsNullOrEmpty(_pointerAnalyzer.ActiveDeviceId) ? "Đang đo tất cả nguồn Raw Mouse." : "Đang đo: " + FriendlyDeviceName(_pointerAnalyzer.ActiveDeviceId);
        }

        private bool PointerEvidenceSelected(KeyEvidence evidence)
        {
            return string.IsNullOrEmpty(_pointerAnalyzer.ActiveDeviceId) || string.Equals(_pointerAnalyzer.ActiveDeviceId, evidence.DeviceId, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdatePointerState(PointerStateSnapshot snapshot)
        {
            if (_pointerMoveLabel == null) return;
            _pointerMoveLabel.Text = "Di chuyển: " + snapshot.MoveEvents.ToString("N0") + " · quãng=" + snapshot.TotalDistance.ToString("N0");
            _pointerLeftLabel.Text = "Nút trái: " + snapshot.LeftClicks + " · " + (snapshot.LeftDown ? "DOWN" : "UP");
            _pointerRightLabel.Text = "Nút phải: " + snapshot.RightClicks + " · " + (snapshot.RightDown ? "DOWN" : "UP");
            _pointerScrollLabel.Text = "Cuộn: " + snapshot.ScrollEvents.ToString("N0");
            _pointerLeftLabel.BackColor = snapshot.LeftDown ? Color.FromArgb(210, 232, 255) : Color.FromArgb(245, 248, 252);
            _pointerRightLabel.BackColor = snapshot.RightDown ? Color.FromArgb(210, 232, 255) : Color.FromArgb(245, 248, 252);
        }

        private void UpdateHumanProfile(HumanTimingSnapshot profile)
        {
            if (_humanModelLabel == null) return;
            _humanModelLabel.Text = profile.SampleCount < _analyzer.Settings.AdaptiveSampleMinimum ? "Học " + profile.SampleCount + "/" + _analyzer.Settings.AdaptiveSampleMinimum :
                "Đã học · " + profile.MedianDwellMs.ToString("0") + " ms";
        }

        private void UpdateCounters()
        {
            _eventCountLabel.Text = _session.Events.Count.ToString("N0", CultureInfo.CurrentCulture);
            _alertCountLabel.Text = _session.Alerts.Count.ToString("N0", CultureInfo.CurrentCulture);
            _deviceCountLabel.Text = _session.Devices.Count.ToString(CultureInfo.CurrentCulture);
        }

        private void ExportReport(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Chọn tên báo cáo chẩn đoán";
                dialog.Filter = "Báo cáo văn bản (*.txt)|*.txt";
                dialog.FileName = "KeyboardDoctor_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    WriteReportFiles(dialog.FileName);
                    MessageBox.Show("Đã tạo báo cáo và hai tệp CSV cùng thư mục:\r\n" + dialog.FileName, "Xuất báo cáo thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Không xuất được báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void WriteReportFiles(string reportPath)
        {
            string directory = Path.GetDirectoryName(reportPath);
            string stem = Path.GetFileNameWithoutExtension(reportPath);
            string eventsPath = Path.Combine(directory, stem + "_events.csv");
            string alertsPath = Path.Combine(directory, stem + "_alerts.csv");
            UTF8Encoding utf8 = new UTF8Encoding(true);

            using (StreamWriter writer = new StreamWriter(reportPath, false, utf8))
            {
                writer.WriteLine("LAPTOP KEYBOARD DOCTOR 2026 - BÁO CÁO CHẨN ĐOÁN");
                writer.WriteLine("Phiên bản: " + Application.ProductVersion);
                writer.WriteLine("Tác giả / đơn vị phát hành: MÁY TÍNH HÀ ANH");
                writer.WriteLine("Địa chỉ: xã Như Thanh, tỉnh Thanh Hoá");
                writer.WriteLine("Thời điểm: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
                writer.WriteLine("Windows: " + Environment.OSVersion + "; " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"));
                writer.WriteLine("Máy: " + Environment.MachineName + "; CLR: " + Environment.Version);
                writer.WriteLine("Nguồn chọn: " + (string.IsNullOrEmpty(_analyzer.ActiveDeviceId) ? "Tất cả" : _analyzer.ActiveDeviceId));
                writer.WriteLine("Ngưỡng chatter: " + _analyzer.Settings.ChatterWindowMs + " ms; stuck: " + _analyzer.Settings.StuckThresholdMs + " ms");
                writer.WriteLine("Windows typematic: delay~" + _analyzer.Settings.RepeatDelayMs + " ms; rate~" + _analyzer.Settings.ExpectedRepeatRateHz.ToString("0.0") + " Hz; storm threshold=" + _analyzer.Settings.RepeatStormCount + "/s");
                HumanTimingSnapshot human = _analyzer.HumanProfile;
                writer.WriteLine("Mô hình người: " + human.Status + "; mẫu=" + human.SampleCount + "; median dwell=" + human.MedianDwellMs.ToString("0.0") + " ms; MAD=" + human.MedianAbsoluteDeviationMs.ToString("0.0") + " ms; lower bound=" + human.AdaptiveLowerBoundMs.ToString("0.0") + " ms");
                PointerStateSnapshot pointer = _pointerAnalyzer.Snapshot;
                writer.WriteLine("Touchpad/Mouse: move=" + pointer.MoveEvents + "; left=" + pointer.LeftClicks + "; right=" + pointer.RightClicks + "; scroll=" + pointer.ScrollEvents + "; source=" + pointer.LastDeviceId);
                writer.WriteLine("Sự kiện: " + _session.Events.Count + " (Raw keyboard=" + _rawKeyboardCount + ", hook=" + _hookCount + ", HID=" + _hidCount + ", Raw mouse=" + _rawMouseCount + ")");
                writer.WriteLine("Cảnh báo: " + _session.Alerts.Count);
                writer.WriteLine();
                writer.WriteLine("THIẾT BỊ");
                foreach (DeviceRecord device in _session.Devices.Values)
                {
                    writer.WriteLine("- " + device.Type + " | " + device.Name + " | events=" + device.EventCount);
                    writer.WriteLine("  " + device.Handle);
                }
                writer.WriteLine();
                writer.WriteLine("CẢNH BÁO");
                if (_session.Alerts.Count == 0) writer.WriteLine("- Không có cảnh báo trong phiên.");
                foreach (DiagnosticAlert alert in _session.Alerts)
                {
                    writer.WriteLine("- [" + SeverityText(alert.Severity) + "] " + alert.Code + " | " + alert.KeyName + " | " + alert.Message);
                    writer.WriteLine("  " + alert.TimeUtc.ToLocalTime().ToString("HH:mm:ss.fff") + " | " + alert.Evidence);
                }
                writer.WriteLine();
                writer.WriteLine("LƯU Ý: Kết quả là bằng chứng kỹ thuật hỗ trợ chẩn đoán. Fn thuần firmware và Secure Attention Sequence có thể không phát mã cho ứng dụng.");
            }

            using (StreamWriter writer = new StreamWriter(eventsPath, false, utf8))
            {
                writer.WriteLine("time_local,source,device,control,key,vk,scan,extended,action,injected,mouse_flags,delta_x,delta_y,wheel,detail");
                foreach (KeyEvidence item in _session.Events)
                {
                    writer.WriteLine(Csv(item.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff")) + "," +
                        Csv(SourceText(item.Source)) + "," + Csv(item.DeviceId) + "," + Csv(item.ControlName) + "," + Csv(item.VirtualKey == 0 ? string.Empty : KeyNames.Get(item.VirtualKey)) + "," +
                        Csv(item.VirtualKey == 0 ? string.Empty : "0x" + item.VirtualKey.ToString("X2")) + "," + Csv(item.ScanCode == 0 ? string.Empty : "0x" + item.ScanCode.ToString("X3")) + "," +
                        item.Extended + "," + Csv(item.IsDown ? "DOWN" : item.IsUp ? "UP" : "INFO") + "," + item.Injected + "," + Csv("0x" + item.MouseButtonFlags.ToString("X4")) + "," +
                        item.DeltaX + "," + item.DeltaY + "," + item.WheelDelta + "," + Csv(item.Detail));
                }
            }

            using (StreamWriter writer = new StreamWriter(alertsPath, false, utf8))
            {
                writer.WriteLine("time_local,severity,code,key,message,evidence");
                foreach (DiagnosticAlert alert in _session.Alerts)
                {
                    writer.WriteLine(Csv(alert.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff")) + "," + Csv(SeverityText(alert.Severity)) + "," +
                        Csv(alert.Code) + "," + Csv(alert.KeyName) + "," + Csv(alert.Message) + "," + Csv(alert.Evidence));
                }
            }
        }

        private static string Csv(string value)
        {
            if (value == null) value = string.Empty;
            return "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        private void ShowMeasurementLimits(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Bắt được: scan code bàn phím, nguồn thiết bị, Down/Up, dwell time, nhịp lặp, sự kiện do phần mềm chèn, WM_APPCOMMAND, Raw HID Consumer, Raw Mouse, nút trái/phải và cuộn.\r\n\r\n" +
                "Không bảo đảm tách được touchpad với chuột ngoài nếu Precision Touchpad được Windows hợp nhất thành nguồn SYSTEM. Fn thuần firmware, Ctrl+Alt+Del và lỗi điện không tạo report vẫn cần đo phần cứng/cáp hoặc thay thử.",
                "Phạm vi đo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            Keys keyCode = keyData & Keys.KeyCode;
            bool arrow = keyCode == Keys.Left || keyCode == Keys.Right || keyCode == Keys.Up || keyCode == Keys.Down;
            if (_capturing && arrow && _tabs != null && (_tabs.SelectedTab == _keyboardPage || _tabs.SelectedTab == _guidedPage))
            {
                // Raw Input still records the key; consuming the UI command prevents TabControl page navigation.
                return true;
            }
            return base.ProcessCmdKey(ref message, keyData);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            _pollTimer.Stop();
            _guidedTimer.Stop();
            _hook.Dispose();
        }

        private static string SourceText(InputSourceKind source)
        {
            switch (source)
            {
                case InputSourceKind.RawKeyboard: return "RAW KEYBOARD";
                case InputSourceKind.LowLevelHook: return "HOOK";
                case InputSourceKind.Poll: return "POLL";
                case InputSourceKind.AppCommand: return "APP COMMAND";
                case InputSourceKind.RawHid: return "RAW HID";
                case InputSourceKind.RawMouse: return "RAW MOUSE";
                case InputSourceKind.DeviceChange: return "DEVICE";
                default: return source.ToString();
            }
        }

        private static string SeverityText(DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Critical: return "NGHIÊM TRỌNG";
                case DiagnosticSeverity.Warning: return "CẢNH BÁO";
                default: return "THÔNG TIN";
            }
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
            return "..." + value.Substring(value.Length - maxLength + 3);
        }
    }
}
