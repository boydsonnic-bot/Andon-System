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
using System.Windows.Forms;

namespace AndonTerminal.Forms
{
    public class TerminalMainForm : Form
    {
        private IncidentService _dbService;
        private FlowLayoutPanel? _stationContainer;
        private TerminalConfig _cfg;

        private System.Windows.Forms.Timer _blinkTimer;
        private SoundPlayer? _sirenPlayer;
        private bool _isBlinkingOn = false;
        private int _redAlarmCount = 0;

        private IconButton? _btnLineYellow;
        private string _lineYellowTicketId = "";

        // ─── MÀU SẮC THEME — INDUSTRIAL CHARCOAL ───────────────────
        // Nền xám than, không xanh — giống HMI Siemens/Rockwell thực tế
        static readonly Color C_BG = Color.FromArgb(18, 18, 18);
        static readonly Color C_HEADER = Color.FromArgb(10, 10, 10);
        static readonly Color C_SURFACE = Color.FromArgb(24, 24, 24);
        static readonly Color C_CARD = Color.FromArgb(32, 32, 32);
        static readonly Color C_CARD_TOP = Color.FromArgb(44, 44, 44);
        static readonly Color C_BORDER = Color.FromArgb(62, 62, 62);
        static readonly Color C_TEXT_PRI = Color.FromArgb(245, 245, 245);
        static readonly Color C_TEXT_SEC = Color.FromArgb(148, 148, 148);

        // Trạng thái — màu thuần bão hòa cao, đọc rõ từ 3m
        static readonly Color C_GREEN = Color.FromArgb(0, 200, 83);
        static readonly Color C_GREEN_DIM = Color.FromArgb(0, 140, 58);
        static readonly Color C_RED = Color.FromArgb(255, 23, 68);
        static readonly Color C_RED_DIM = Color.FromArgb(180, 10, 40);
        static readonly Color C_YELLOW = Color.FromArgb(255, 214, 0);
        static readonly Color C_YELLOW_DIM = Color.FromArgb(200, 160, 0);
        static readonly Color C_ORANGE = Color.FromArgb(255, 109, 0);
        static readonly Color C_BLUE = Color.FromArgb(41, 121, 255);
        static readonly Color C_BLUE_BTN = Color.FromArgb(25, 90, 210);

        public TerminalMainForm()
        {
            _cfg = TerminalConfig.Load("terminal.cfg");
            _dbService = new IncidentService(_cfg.DbPath);
            _dbService.InitializeStationTable();

            string audioPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Asset", "alarm.wav");
            if (File.Exists(audioPath))
                _sirenPlayer = new SoundPlayer(audioPath);

            _blinkTimer = new System.Windows.Forms.Timer();
            _blinkTimer.Interval = 500;
            _blinkTimer.Tick += BlinkTimer_Tick;
            _blinkTimer.Start();

            // ── Form ─────────────────────────────────────────────────
            this.Text = $"preAndon Terminal — {_cfg.FactoryCode} | {_cfg.LineName}";
            this.Size = new Size(1280, 820);
            this.MinimumSize = new Size(1100, 700);
            this.BackColor = C_BG;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            BuildHeader();

            // ── Station container ────────────────────────────────────
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
            this.Resize += (s, e) => {
                _stationContainer.Size = new Size(this.ClientSize.Width, this.ClientSize.Height - 88);
            };

            DrawAllStations();
        }

        // ─────────────────────────────────────────────────────────────
        // HEADER
        // ─────────────────────────────────────────────────────────────
        private void BuildHeader()
        {
            // Panel nền header
            Panel pnlHeader = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(this.ClientSize.Width, 88),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = C_HEADER
            };
            // Gạch accent dưới header
            pnlHeader.Paint += (s, e) => {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(80, 80, 80)), 0, 85, pnlHeader.Width, 3);
            };
            this.Controls.Add(pnlHeader);
            this.Resize += (s, e) => pnlHeader.Width = this.ClientSize.Width;

            // Tiêu đề
            Label lblTitle = new Label
            {
                Text = $"ANDON  ·  {_cfg.FactoryCode}  —  {_cfg.LineName.ToUpper()}",
                Font = new Font("Segoe UI", 17f, FontStyle.Bold),
                ForeColor = C_TEXT_PRI,
                Location = new Point(22, 0),
                Size = new Size(700, 88),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            pnlHeader.Controls.Add(lblTitle);

            // ── Nút THIẾU VẬT TƯ ─────────────────────────────────
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
                Location = new Point(pnlHeader.Width - 390, 22),
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            _btnLineYellow.FlatAppearance.BorderSize = 0;
            _btnLineYellow.FlatAppearance.MouseOverBackColor = C_YELLOW_DIM;
            _btnLineYellow.Click += BtnLineYellow_Click;
            pnlHeader.Controls.Add(_btnLineYellow);

            // ── Nút LỊCH SỬ ─────────────────────────────────────
            IconButton btnHistory = new IconButton
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
                Location = new Point(pnlHeader.Width - 148, 22),
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            btnHistory.FlatAppearance.BorderSize = 0;
            btnHistory.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            btnHistory.Click += (s, e) =>
            {
                Form historyPopup = new Form
                {
                    Text = "Lịch sử sự cố hệ thống",
                    Size = new Size(900, 500),
                    StartPosition = FormStartPosition.CenterParent,
                    BackColor = C_BG
                };
                DataGridView grid = new DataGridView
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
                historyPopup.Controls.Add(grid);
                historyPopup.ShowDialog();
            };
            pnlHeader.Controls.Add(btnHistory);

            // Căn lại nút theo resize
            this.Resize += (s, e) => {
                pnlHeader.Width = this.ClientSize.Width;
                _btnLineYellow!.Left = pnlHeader.Width - 390;
                btnHistory.Left = pnlHeader.Width - 148;
            };
        }

        // ─────────────────────────────────────────────────────────────
        // LOGIC NÚT VÀNG TOÀN CHUYỀN — KHÔNG ĐỔI
        // ─────────────────────────────────────────────────────────────
        private void BtnLineYellow_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_lineYellowTicketId))
            {
                using (var inputForm = new EmployeeInputForm())
                {
                    inputForm.Text = "Nhập mã OP Báo Thiếu Vật Tư";
                    if (inputForm.ShowDialog(this) == DialogResult.OK)
                    {
                        var ticket = new IncidentTicket
                        {
                            TicketId = "TKT-" + DateTime.Now.ToString("HHmmss"),
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

                        foreach (Panel card in _stationContainer!.Controls)
                        {
                            var btn = card.Controls.OfType<IconButton>().FirstOrDefault();
                            var lblHint = card.Controls.OfType<Label>().LastOrDefault();
                            if (btn != null && lblHint != null)
                            {
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
                }
            }
            else
            {
                using (var inputForm = new EmployeeInputForm())
                {
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
        }

        // ─────────────────────────────────────────────────────────────
        // BLINK TIMER — KHÔNG ĐỔI LOGIC, CHỈ ĐỔI MÀU
        // ─────────────────────────────────────────────────────────────
        private void BlinkTimer_Tick(object? sender, EventArgs e)
        {
            _isBlinkingOn = !_isBlinkingOn;

            if (!string.IsNullOrEmpty(_lineYellowTicketId) && _btnLineYellow != null)
                _btnLineYellow.BackColor = _isBlinkingOn ? C_YELLOW : C_YELLOW_DIM;

            if (_stationContainer == null) return;

            foreach (Panel card in _stationContainer.Controls)
            {
                var btnStatus = card.Controls.OfType<IconButton>().FirstOrDefault();
                if (btnStatus != null && btnStatus.Text == "  CHỜ KTV")
                {
                    btnStatus.BackColor = _isBlinkingOn ? C_RED : C_RED_DIM;
                    btnStatus.ForeColor = Color.White;
                    btnStatus.IconColor = Color.White;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // DRAW ALL STATIONS — KHÔNG ĐỔI
        // ─────────────────────────────────────────────────────────────
        private void DrawAllStations()
        {
            _stationContainer!.Controls.Clear();
            List<string> danhSachMay = _dbService.GetActiveStations(_cfg.FactoryCode, _cfg.LineName);
            foreach (var tenMay in danhSachMay)
                _stationContainer.Controls.Add(CreateStationCard(tenMay));
        }

        // ─────────────────────────────────────────────────────────────
        // STATION CARD — CHỈ THAY ĐỔI VISUAL
        // ─────────────────────────────────────────────────────────────
        private Panel CreateStationCard(string stationName)
        {
            // ── Card wrapper ────────────────────────────────────────
            Panel card = new Panel
            {
                Size = new Size(250, 260),
                BackColor = C_CARD,
                Margin = new Padding(8),
                Cursor = Cursors.Default
            };

            // Vẽ border card bằng Paint
            card.Paint += (s, e) => {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(C_BORDER, 1);
                g.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            // ── Header stripe (tên máy) ──────────────────────────
            Panel pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = C_CARD_TOP
            };
            Label lblName = new Label
            {
                Text = stationName.ToUpper(),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = C_TEXT_PRI,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            pnlTop.Controls.Add(lblName);
            card.Controls.Add(pnlTop);

            // ── Nút trạng thái chính ─────────────────────────────
            IconButton btnStatus = new IconButton
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

            // ── Separator line ────────────────────────────────────
            Panel sep = new Panel
            {
                Location = new Point(10, 130),
                Size = new Size(230, 1),
                BackColor = C_BORDER
            };
            card.Controls.Add(sep);

            // ── Label hint ────────────────────────────────────────
            string initialHint = _dbService.GetSmartAnalyticsHint(stationName);
            Label lblHint = new Label
            {
                Text = $"● Sẵn sàng\n{initialHint}",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = C_TEXT_SEC,
                Location = new Point(10, 138),
                Size = new Size(230, 112),
                BackColor = Color.Transparent
            };
            card.Controls.Add(lblHint);

            // ── LOGIC NÚT — KHÔNG ĐỔI GÌ, CHỈ ĐỔI MÀU/TEXT/ICON ─
            btnStatus.Click += (s, e) =>
            {
                if (btnStatus.BackColor == C_GREEN)
                {
                    using (var inputForm = new EmployeeInputForm())
                    {
                        inputForm.Text = "OP Báo Lỗi Máy";
                        if (inputForm.ShowDialog(this) == DialogResult.OK)
                        {
                            btnStatus.BackColor = C_RED;
                            btnStatus.ForeColor = Color.White;
                            btnStatus.IconColor = Color.White;
                            btnStatus.Text = "  CHỜ KTV";
                            btnStatus.IconChar = IconChar.TimesCircle;
                            btnStatus.FlatAppearance.MouseOverBackColor = C_RED_DIM;

                            var ticket = new IncidentTicket
                            {
                                TicketId = "TKT-" + DateTime.Now.ToString("HHmmss"),
                                LineNumber = _cfg.LineName,
                                StationName = stationName,
                                AlarmTypeIndex = 1,
                                Status = TicketStatus.Red,
                                ReportedAt = DateTime.Now,
                                OperatorName = inputForm.EmployeeName
                            };
                            _dbService.OpenIncident(ticket);
                            btnStatus.Tag = ticket.TicketId;

                            _redAlarmCount++;
                            if (_redAlarmCount == 1 && _sirenPlayer != null) _sirenPlayer.PlayLooping();

                            lblHint.Text = $"● OP {inputForm.EmployeeName} báo lỗi\n● Đang hú còi chờ KTV...";
                            lblHint.ForeColor = Color.FromArgb(252, 165, 165);
                        }
                    }
                }
                else if (btnStatus.Text == "  CHỜ KTV")
                {
                    using (var inputForm = new EmployeeInputForm())
                    {
                        inputForm.Text = "KTV Nhận Việc";
                        if (inputForm.ShowDialog(this) == DialogResult.OK)
                        {
                            btnStatus.BackColor = C_ORANGE;
                            btnStatus.ForeColor = Color.White;
                            btnStatus.IconColor = Color.White;
                            btnStatus.Text = "  ĐANG SỬA";
                            btnStatus.IconChar = IconChar.Wrench;
                            btnStatus.FlatAppearance.MouseOverBackColor = Color.FromArgb(194, 65, 8);

                            string currentId = btnStatus.Tag?.ToString() ?? "";
                            _dbService.UpdateTicket(currentId, TicketStatus.Repairing, inputForm.EmployeeName, techCheckinAt: DateTime.Now);

                            lblHint.Text = $"● KTV {inputForm.EmployeeName} đang sửa\n● Chờ sửa xong...";
                            lblHint.ForeColor = Color.FromArgb(253, 186, 116);

                            _redAlarmCount--;
                            if (_redAlarmCount <= 0) { _redAlarmCount = 0; _sirenPlayer?.Stop(); }
                        }
                    }
                }
                else if (btnStatus.Text == "  ĐANG SỬA")
                {
                    using (var fixForm = new FixCompleteForm())
                    {
                        if (fixForm.ShowDialog(this) == DialogResult.OK)
                        {
                            btnStatus.BackColor = C_BLUE;
                            btnStatus.ForeColor = Color.White;
                            btnStatus.IconColor = Color.White;
                            btnStatus.Text = "  CHỜ LEADER";
                            btnStatus.IconChar = IconChar.UserCheck;
                            btnStatus.FlatAppearance.MouseOverBackColor = C_BLUE_BTN;

                            string currentId = btnStatus.Tag?.ToString() ?? "";
                            _dbService.UpdateTicket(currentId, TicketStatus.WaitLeader,
                                errorReason: fixForm.ErrorReason, fixNote: fixForm.FixNote, techFixedAt: DateTime.Now);

                            lblHint.Text = $"● Sửa xong lúc {DateTime.Now:HH:mm}\n● Chờ Leader xác nhận...";
                            lblHint.ForeColor = Color.FromArgb(147, 197, 253);
                        }
                    }
                }
                else if (btnStatus.Text == "  CHỜ LEADER")
                {
                    using (var inputForm = new EmployeeInputForm())
                    {
                        inputForm.Text = "Leader Xác Nhận";
                        if (inputForm.ShowDialog(this) == DialogResult.OK)
                        {
                            string currentId = btnStatus.Tag?.ToString() ?? "";
                            _dbService.UpdateTicket(currentId, TicketStatus.Closed,
                                leaderName: inputForm.EmployeeName, leaderConfirmedAt: DateTime.Now);

                            btnStatus.BackColor = C_GREEN;
                            btnStatus.ForeColor = Color.White;
                            btnStatus.IconColor = Color.White;
                            btnStatus.Text = "  BÌNH THƯỜNG";
                            btnStatus.IconChar = IconChar.CheckCircle;
                            btnStatus.FlatAppearance.MouseOverBackColor = C_GREEN_DIM;
                            btnStatus.Tag = null;

                            string updatedHint = _dbService.GetSmartAnalyticsHint(stationName);
                            lblHint.Text = $"● Đóng phiếu {DateTime.Now:HH:mm}\n{updatedHint}";
                            lblHint.ForeColor = C_TEXT_SEC;
                        }
                    }
                }
            };

            return card;
        }
    }
}