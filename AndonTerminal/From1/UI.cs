using FontAwesome.Sharp;
using SharedLib.Model;
using SharedLib.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows.Forms;                    // XÓA using System.Windows.Controls

namespace AndonTerminal.Forms
{
    public class TerminalMainForm : Form
    {
        // ── Services ─────────────────────────────────────────────────
        private IncidentService _dbService;
        private FlowLayoutPanel? _stationContainer;
        private TerminalConfig _cfg;

        // ── Timers & Audio ────────────────────────────────────────────
        private System.Windows.Forms.Timer _blinkTimer;
        private System.Windows.Forms.Timer? _breakTimer;        // ← MỚI
        private SoundPlayer? _sirenPlayer;
        private bool _isBlinkingOn = false;
        private int _redAlarmCount = 0;

        // ── Trạng thái nút vàng toàn chuyền ──────────────────────────
        private IconButton? _btnLineYellow;
        private string _lineYellowTicketId = "";

        // ── Trạng thái nghỉ ca ─────────────────────────────────────── ← MỚI
        private IconButton? _btnBreak;
        private Label? _lblBreakTimer;
        private DateTime _breakStart;
        private bool _isOnBreak = false;

        // ── Theme ─────────────────────────────────────────────────────
        static readonly Color C_BG = Color.FromArgb(18, 18, 18);
        static readonly Color C_HEADER = Color.FromArgb(10, 10, 10);
        static readonly Color C_SURFACE = Color.FromArgb(24, 24, 24);
        static readonly Color C_CARD = Color.FromArgb(32, 32, 32);
        static readonly Color C_CARD_TOP = Color.FromArgb(44, 44, 44);
        static readonly Color C_BORDER = Color.FromArgb(62, 62, 62);
        static readonly Color C_TEXT_PRI = Color.FromArgb(245, 245, 245);
        static readonly Color C_TEXT_SEC = Color.FromArgb(148, 148, 148);
        static readonly Color C_GREEN = Color.FromArgb(0, 200, 83);
        static readonly Color C_GREEN_DIM = Color.FromArgb(0, 140, 58);
        static readonly Color C_RED = Color.FromArgb(255, 23, 68);
        static readonly Color C_RED_DIM = Color.FromArgb(180, 10, 40);
        static readonly Color C_YELLOW = Color.FromArgb(255, 214, 0);
        static readonly Color C_YELLOW_DIM = Color.FromArgb(200, 160, 0);
        static readonly Color C_ORANGE = Color.FromArgb(255, 109, 0);
        static readonly Color C_BLUE = Color.FromArgb(41, 121, 255);
        static readonly Color C_BLUE_BTN = Color.FromArgb(25, 90, 210);

        // ═════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════
        public TerminalMainForm()
        {
            _cfg = TerminalConfig.Load("terminal.cfg");
            _dbService = new IncidentService(_cfg.DbPath);
            _dbService.InitializeStationTable();
            _dbService.InitializeShiftSessionTable();           // ← MỚI

            // Audio
            string audioPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Asset", "alarm.wav");
            if (File.Exists(audioPath))
                _sirenPlayer = new SoundPlayer(audioPath);

            // Blink timer
            _blinkTimer = new System.Windows.Forms.Timer();
            _blinkTimer.Interval = 500;
            _blinkTimer.Tick += BlinkTimer_Tick;
            _blinkTimer.Start();

            // Break timer — đếm giờ nghỉ hiển thị lên header     ← MỚI
            _breakTimer = new System.Windows.Forms.Timer();
            _breakTimer.Interval = 1000;
            _breakTimer.Tick += BreakTimer_Tick;

            // Form
            this.Text = $"preAndon Terminal — {_cfg.FactoryCode} | {_cfg.LineName}";
            this.Size = new Size(1280, 820);
            this.MinimumSize = new Size(1100, 700);
            this.BackColor = C_BG;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            // Bắt đầu session làm việc khi mở app              ← MỚI
            _dbService.StartWorkSession(_cfg.FactoryCode, _cfg.LineName);

            // Đóng session khi tắt app                          ← MỚI
            this.FormClosing += (s, e) =>
                _dbService.EndCurrentWorkSession(_cfg.FactoryCode, _cfg.LineName);

            BuildHeader();

            // Station container
            _stationContainer = new FlowLayoutPanel
            {
                Location = new Point(0, 88),
                Size = new Size(this.ClientSize.Width, this.ClientSize.Height - 88),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                BackColor = C_SURFACE,
                Padding = new Padding(18, 14, 18, 18),
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight
            };
            this.Controls.Add(_stationContainer);
            this.Resize += (s, e) =>
                _stationContainer.Size = new Size(this.ClientSize.Width, this.ClientSize.Height - 88);

            DrawAllStations();
        }

        // ═════════════════════════════════════════════════════════════
        // HEADER (Đã tích hợp FlowLayoutPanel chống vỡ giao diện)
        // ═════════════════════════════════════════════════════════════
        private void BuildHeader()
        {
            var pnlHeader = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 88,
                BackColor = C_HEADER
            };
            pnlHeader.Paint += (s, e) =>
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(80, 80, 80)), 0, 85, pnlHeader.Width, 3);
            this.Controls.Add(pnlHeader);

            // Tiêu đề
            pnlHeader.Controls.Add(new System.Windows.Forms.Label
            {
                Text = $"ANDON  ·  {_cfg.FactoryCode}  —  {_cfg.LineName.ToUpper()}",
                Font = new Font("Segoe UI", 17f, FontStyle.Bold),
                ForeColor = C_TEXT_PRI,
                Location = new Point(22, 26),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            // Label đếm giờ nghỉ
            _lblBreakTimer = new System.Windows.Forms.Label
            {
                Text = "",
                Font = new Font("Consolas", 11f, FontStyle.Bold),
                ForeColor = C_YELLOW,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(22, 60),
                Visible = false
            };
            pnlHeader.Controls.Add(_lblBreakTimer);

            // ────────────────────────────────────────────────────────────────
            // VŨ KHÍ BÍ MẬT: Container tự động căn lề phải cho các nút
            // ────────────────────────────────────────────────────────────────
            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right, // Bám chặt lề phải
                AutoSize = true, // Tự động co giãn theo số lượng nút
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft, // Xếp nút từ phải qua trái
                WrapContents = false, // Không cho phép rớt dòng
                Padding = new Padding(0, 22, 10, 0) // Căn lề top 22px, lề phải 10px
            };
            pnlHeader.Controls.Add(pnlButtons);

            // 1. Nút LỊCH SỬ (Sẽ nằm sát lề phải nhất do FlowDirection = RightToLeft)
            var btnHistory = new IconButton
            {
                Text = "  Lịch Sử",
                IconChar = IconChar.ChartBar,
                IconSize = 20,
                IconColor = C_TEXT_PRI,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = C_BLUE_BTN,
                ForeColor = C_TEXT_PRI,
                Size = new Size(120, 44),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(5, 0, 5, 0) // Khoảng cách đều 2 bên 5px
            };
            btnHistory.FlatAppearance.BorderSize = 0;
            btnHistory.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            btnHistory.Click += (s, e) =>
            {
                var popup = new Form
                {
                    Text = "Lịch sử sự cố hệ thống",
                    Size = new Size(900, 500),
                    StartPosition = FormStartPosition.CenterParent,
                    BackColor = C_BG
                };
                var grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    RowHeadersVisible = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    DataSource = _dbService.GetAllTickets(),
                    BackgroundColor = C_SURFACE,
                    ForeColor = C_TEXT_PRI,
                    GridColor = C_BORDER,
                    BorderStyle = BorderStyle.None,
                    ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                    {
                        BackColor = C_HEADER,
                        ForeColor = C_TEXT_PRI,
                        Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                    },
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        BackColor = C_SURFACE,
                        ForeColor = C_TEXT_PRI,
                        SelectionBackColor = C_BLUE,
                        SelectionForeColor = Color.White
                    }
                };
                popup.Controls.Add(grid);
                popup.ShowDialog(this);
            };
            pnlButtons.Controls.Add(btnHistory); // Thêm vào FlowLayoutPanel

            // 2. Nút CÀI ĐẶT
            var btnSettings = new IconButton
            {
                Text = "  Cài đặt",
                IconChar = IconChar.Cog,
                IconSize = 20,
                IconColor = C_TEXT_PRI,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = C_CARD_TOP,
                ForeColor = C_TEXT_PRI,
                Size = new Size(110, 44),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(5, 0, 5, 0)
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.FlatAppearance.MouseOverBackColor = C_BORDER;
            btnSettings.Click += BtnSettings_Click;
            pnlButtons.Controls.Add(btnSettings); // Thêm vào FlowLayoutPanel

            // 3. Nút THIẾU VẬT TƯ
            _btnLineYellow = new IconButton
            {
                Text = "  THIẾU VẬT TƯ CHUYỀN",
                IconChar = IconChar.ExclamationTriangle,
                IconSize = 22,
                IconColor = Color.FromArgb(15, 23, 42),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = C_YELLOW,
                ForeColor = Color.FromArgb(15, 23, 42),
                Size = new Size(230, 44),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(5, 0, 5, 0)
            };
            _btnLineYellow.FlatAppearance.BorderSize = 0;
            _btnLineYellow.FlatAppearance.MouseOverBackColor = C_YELLOW_DIM;
            _btnLineYellow.Click += BtnLineYellow_Click;
            pnlButtons.Controls.Add(_btnLineYellow); // Thêm vào FlowLayoutPanel

            // 4. Nút NGHỈ CA (Sẽ nằm sát bên trái nhất của vùng chứa)
            _btnBreak = new IconButton
            {
                Text = "  NGHỈ CA",
                IconChar = IconChar.PauseCircle,
                IconSize = 20,
                IconColor = C_TEXT_PRI,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = C_CARD_TOP,
                ForeColor = C_TEXT_PRI,
                Size = new Size(120, 44),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(5, 0, 5, 0)
            };
            _btnBreak.FlatAppearance.BorderSize = 0;
            _btnBreak.FlatAppearance.MouseOverBackColor = C_BORDER;
            _btnBreak.Click += BtnBreak_Click;
            pnlButtons.Controls.Add(_btnBreak); // Thêm vào FlowLayoutPanel
        }

        // ═════════════════════════════════════════════════════════════
        // NGHỈ CA                                                ← MỚI
        // ═════════════════════════════════════════════════════════════
        private void BtnBreak_Click(object? sender, EventArgs e)
        {
            if (!_isOnBreak)
            {
                // 1. THÊM BƯỚC YÊU CẦU LEADER XÁC NHẬN TRƯỚC KHI NGHỈ
                using var inputForm = new EmployeeInputForm { Text = "Leader Xác Nhận Nghỉ Ca" };
                if (inputForm.ShowDialog(this) != DialogResult.OK)
                {
                    // Nếu Leader bấm Hủy hoặc đóng form thì thoát ra, không dừng chuyền
                    return;
                }

                // 2. BẮT ĐẦU NGHỈ (Sau khi đã xác nhận OK)
                _isOnBreak = true;
                _breakStart = DateTime.Now;

                // Lưu ý: Nếu sau này hàm StartBreak có hỗ trợ lưu tên người duyệt, 
                // bạn có thể truyền thêm biến inputForm.EmployeeName vào đây.
                _dbService.StartBreak(_cfg.FactoryCode, _cfg.LineName, "MANUAL");
                _breakTimer?.Start();

                _btnBreak!.Text = "  TIẾP TỤC CA";
                _btnBreak.IconChar = IconChar.PlayCircle;
                _btnBreak.BackColor = C_YELLOW;
                _btnBreak.ForeColor = Color.FromArgb(15, 23, 42);
                _btnBreak.IconColor = Color.FromArgb(15, 23, 42);

                if (_lblBreakTimer != null) _lblBreakTimer.Visible = true;

                // Disable nút trạm đang bình thường — không cho báo lỗi lúc nghỉ
                foreach (System.Windows.Forms.Panel card in _stationContainer!.Controls)
                {
                    var btn = card.Controls.OfType<IconButton>().FirstOrDefault();
                    if (btn != null && btn.BackColor == C_GREEN)
                        btn.Enabled = false;
                }
            }
            else
            {
                // KẾT THÚC NGHỈ (Không cần xác nhận, bấm là chạy luôn)
                _isOnBreak = false;

                _dbService.EndBreak(_cfg.FactoryCode, _cfg.LineName);
                _breakTimer?.Stop();

                _btnBreak!.Text = "  NGHỈ CA";
                _btnBreak.IconChar = IconChar.PauseCircle;
                _btnBreak.BackColor = C_CARD_TOP;
                _btnBreak.ForeColor = C_TEXT_PRI;
                _btnBreak.IconColor = C_TEXT_PRI;

                if (_lblBreakTimer != null)
                {
                    _lblBreakTimer.Visible = false;
                    _lblBreakTimer.Text = "";
                }

                // Enable lại tất cả nút trạm
                foreach (System.Windows.Forms.Panel card in _stationContainer!.Controls)
                {
                    var btn = card.Controls.OfType<IconButton>().FirstOrDefault();
                    if (btn != null) btn.Enabled = true;
                }
            }
        }

        private void BreakTimer_Tick(object? sender, EventArgs e)       // ← MỚI
        {
            if (_lblBreakTimer != null)
            {
                var elapsed = DateTime.Now - _breakStart;
                _lblBreakTimer.Text = $"⏸  ĐANG NGHỈ  {(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
            }
        }

        // ═════════════════════════════════════════════════════════════
        // CÀI ĐẶT
        // ═════════════════════════════════════════════════════════════
        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            try
            {
                using var settingsForm = new SettingsForm();
                if (settingsForm.ShowDialog(this) == DialogResult.OK)
                {
                    _cfg = TerminalConfig.Load("terminal.cfg");
                    this.Text = $"preAndon Terminal — {_cfg.FactoryCode} | {_cfg.LineName}";
                    DrawAllStations();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Lỗi không thể mở Form Cài đặt: {ex.Message}\n\nChi tiết: {ex.StackTrace}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════
        // LOGIC NÚT VÀNG TOÀN CHUYỀN
        // ═════════════════════════════════════════════════════════════
        private void BtnLineYellow_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_lineYellowTicketId))
            {
                using var inputForm = new EmployeeInputForm();
                inputForm.Text = "Nhập mã OP Báo Thiếu Vật Tư";
                if (inputForm.ShowDialog(this) == DialogResult.OK)
                {
                    var ticket = new IncidentTicket
                    {
                        TicketId = "TKT-" + DateTime.Now.ToString("HHmmssff"),
                        LineNumber = _cfg.LineName,
                        StationName = "TOÀN CHUYỀN",
                        AlarmTypeIndex = 2,
                        Status = TicketStatus.WaitLeader,
                        ReportedAt = DateTime.Now,
                        OperatorName = inputForm.EmployeeName
                    };
                    _dbService.OpenIncident(ticket);
                    _lineYellowTicketId = ticket.TicketId;

                    _btnLineYellow!.Text = "  CHỜ CẤP VẬT TƯ (Nhấn chốt)";
                    _btnLineYellow!.IconChar = IconChar.Clock;

                    foreach (System.Windows.Forms.Panel card in _stationContainer!.Controls)
                    {
                        var btn = card.Controls.OfType<IconButton>().FirstOrDefault();
                        var lblHint = card.Controls.OfType<System.Windows.Forms.Label>().LastOrDefault();
                        if (btn == null || lblHint == null) continue;
                        btn.Enabled = false;
                        btn.BackColor = C_YELLOW;
                        btn.ForeColor = Color.FromArgb(15, 23, 42);
                        btn.IconColor = Color.FromArgb(15, 23, 42);
                        btn.Text = "  DỪNG CHỜ VẬT TƯ";
                        btn.IconChar = IconChar.BoxOpen;
                        lblHint.Text = "🚨 Chuyền đang dừng do thiếu vật tư.";
                        lblHint.ForeColor = Color.FromArgb(252, 211, 77);
                    }
                }
            }
            else
            {
                using var inputForm = new EmployeeInputForm();
                inputForm.Text = "Leader Xác Nhận Đã Có Hàng";
                if (inputForm.ShowDialog(this) == DialogResult.OK)
                {
                    _dbService.UpdateTicket(_lineYellowTicketId, TicketStatus.Closed,
                        errorReason: "Thiếu vật tư/hàng hóa",
                        fixNote: "Đã cấp bù vật tư, Line chạy lại",
                        leaderName: inputForm.EmployeeName,
                        leaderConfirmedAt: DateTime.Now);

                    _lineYellowTicketId = "";
                    _btnLineYellow!.Text = "  THIẾU VẬT TƯ CHUYỀN";
                    _btnLineYellow.IconChar = IconChar.ExclamationTriangle;
                    _btnLineYellow.BackColor = C_YELLOW;
                    DrawAllStations();
                }
            }
        }

        // ═════════════════════════════════════════════════════════════
        // BLINK TIMER
        // ═════════════════════════════════════════════════════════════
        private void BlinkTimer_Tick(object? sender, EventArgs e)
        {
            _isBlinkingOn = !_isBlinkingOn;

            if (!string.IsNullOrEmpty(_lineYellowTicketId) && _btnLineYellow != null)
                _btnLineYellow.BackColor = _isBlinkingOn ? C_YELLOW : C_YELLOW_DIM;

            if (_stationContainer == null) return;

            foreach (System.Windows.Forms.Panel card in _stationContainer.Controls)
            {
                var btn = card.Controls.OfType<IconButton>().FirstOrDefault();
                if (btn != null && btn.Text == "  CHỜ KTV")
                {
                    btn.BackColor = _isBlinkingOn ? C_RED : C_RED_DIM;
                    btn.ForeColor = Color.White;
                    btn.IconColor = Color.White;
                }
            }
        }

        // ═════════════════════════════════════════════════════════════
        // DRAW STATIONS
        // ═════════════════════════════════════════════════════════════
        private void DrawAllStations()
        {
            _stationContainer!.Controls.Clear();
            var list = _cfg.Stations.Count > 0
                ? _cfg.Stations
                : _dbService.GetActiveStations(_cfg.FactoryCode, _cfg.LineName);
            foreach (var name in list)
                _stationContainer.Controls.Add(CreateStationCard(name));
        }

        // ═════════════════════════════════════════════════════════════
        // STATION CARD
        // ═════════════════════════════════════════════════════════════
        private System.Windows.Forms.Panel CreateStationCard(string stationName)
        {
            var card = new System.Windows.Forms.Panel
            {
                Size = new Size(250, 260),
                BackColor = C_CARD,
                Margin = new Padding(8),
                Cursor = Cursors.Default
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(C_BORDER, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            var pnlTop = new System.Windows.Forms.Panel
            { Dock = DockStyle.Top, Height = 36, BackColor = C_CARD_TOP };
            pnlTop.Controls.Add(new System.Windows.Forms.Label
            {
                Text = stationName.ToUpper(),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = C_TEXT_PRI,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });
            card.Controls.Add(pnlTop);

            var btnStatus = new IconButton
            {
                Text = "  BÌNH THƯỜNG",
                IconChar = IconChar.CheckCircle,
                IconColor = Color.White,
                IconSize = 36,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                BackColor = C_GREEN,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Size = new Size(230, 76),
                Location = new Point(10, 46),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnStatus.FlatAppearance.BorderSize = 0;
            btnStatus.FlatAppearance.MouseOverBackColor = C_GREEN_DIM;
            card.Controls.Add(btnStatus);

            card.Controls.Add(new System.Windows.Forms.Panel
            {
                Location = new Point(10, 130),
                Size = new Size(230, 1),
                BackColor = C_BORDER
            });

            string hint = _dbService.GetSmartAnalyticsHint(stationName);
            var lblHint = new System.Windows.Forms.Label
            {
                Text = $"● Sẵn sàng\n{hint}",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = C_TEXT_SEC,
                Location = new Point(10, 138),
                Size = new Size(230, 112),
                BackColor = Color.Transparent
            };
            card.Controls.Add(lblHint);

            btnStatus.Click += (s, e) =>
            {
                if (btnStatus.BackColor == C_GREEN)
                {
                    using var f = new EmployeeInputForm { Text = "OP Báo Lỗi Máy" };
                    if (f.ShowDialog(this) != DialogResult.OK) return;

                    btnStatus.BackColor = C_RED; btnStatus.ForeColor = Color.White;
                    btnStatus.IconColor = Color.White; btnStatus.Text = "  CHỜ KTV";
                    btnStatus.IconChar = IconChar.TimesCircle;
                    btnStatus.FlatAppearance.MouseOverBackColor = C_RED_DIM;

                    var t = new IncidentTicket
                    {
                        TicketId = "TKT-" + DateTime.Now.ToString("HHmmssff"),
                        LineNumber = _cfg.LineName,
                        StationName = stationName,
                        AlarmTypeIndex = 1,
                        Status = TicketStatus.Red,
                        ReportedAt = DateTime.Now,
                        OperatorName = f.EmployeeName
                    };
                    _dbService.OpenIncident(t);
                    btnStatus.Tag = t.TicketId;
                    _redAlarmCount++;
                    if (_redAlarmCount == 1) _sirenPlayer?.PlayLooping();

                    lblHint.Text = $"● OP {f.EmployeeName} báo lỗi\n● Đang hú còi chờ KTV...";
                    lblHint.ForeColor = Color.FromArgb(252, 165, 165);
                }
                else if (btnStatus.Text == "  CHỜ KTV")
                {
                    using var f = new EmployeeInputForm { Text = "KTV Nhận Việc" };
                    if (f.ShowDialog(this) != DialogResult.OK) return;

                    btnStatus.BackColor = C_ORANGE; btnStatus.ForeColor = Color.White;
                    btnStatus.IconColor = Color.White; btnStatus.Text = "  ĐANG SỬA";
                    btnStatus.IconChar = IconChar.Wrench;
                    btnStatus.FlatAppearance.MouseOverBackColor = Color.FromArgb(194, 65, 8);

                    _dbService.UpdateTicket(btnStatus.Tag?.ToString() ?? "",
                        TicketStatus.Repairing, f.EmployeeName, techCheckinAt: DateTime.Now);

                    lblHint.Text = $"● KTV {f.EmployeeName} đang sửa\n● Chờ sửa xong...";
                    lblHint.ForeColor = Color.FromArgb(253, 186, 116);

                    _redAlarmCount--;
                    if (_redAlarmCount <= 0) { _redAlarmCount = 0; _sirenPlayer?.Stop(); }
                }
                else if (btnStatus.Text == "  ĐANG SỬA")
                {
                    using var f = new FixCompleteForm();
                    if (f.ShowDialog(this) != DialogResult.OK) return;

                    btnStatus.BackColor = C_BLUE; btnStatus.ForeColor = Color.White;
                    btnStatus.IconColor = Color.White; btnStatus.Text = "  CHỜ LEADER";
                    btnStatus.IconChar = IconChar.UserCheck;
                    btnStatus.FlatAppearance.MouseOverBackColor = C_BLUE_BTN;

                    _dbService.UpdateTicket(btnStatus.Tag?.ToString() ?? "",
                        TicketStatus.WaitLeader,
                        errorReason: f.ErrorReason, fixNote: f.FixNote, techFixedAt: DateTime.Now);

                    lblHint.Text = $"● Sửa xong lúc {DateTime.Now:HH:mm}\n● Chờ Leader xác nhận...";
                    lblHint.ForeColor = Color.FromArgb(147, 197, 253);
                }
                else if (btnStatus.Text == "  CHỜ LEADER")
                {
                    using var f = new EmployeeInputForm { Text = "Leader Xác Nhận" };
                    if (f.ShowDialog(this) != DialogResult.OK) return;

                    _dbService.UpdateTicket(btnStatus.Tag?.ToString() ?? "",
                        TicketStatus.Closed,
                        leaderName: f.EmployeeName, leaderConfirmedAt: DateTime.Now);

                    btnStatus.BackColor = C_GREEN; btnStatus.ForeColor = Color.White;
                    btnStatus.IconColor = Color.White; btnStatus.Text = "  BÌNH THƯỜNG";
                    btnStatus.IconChar = IconChar.CheckCircle;
                    btnStatus.FlatAppearance.MouseOverBackColor = C_GREEN_DIM;
                    btnStatus.Tag = null;

                    string newHint = _dbService.GetSmartAnalyticsHint(stationName);
                    lblHint.Text = $"● Đóng phiếu {DateTime.Now:HH:mm}\n{newHint}";
                    lblHint.ForeColor = C_TEXT_SEC;
                }
            };

            return card;
        }
    }
}