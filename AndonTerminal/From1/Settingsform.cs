using FontAwesome.Sharp;
using SharedLib.Services;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace AndonTerminal.Forms
{
    public class SettingsForm : Form
    {
        private Panel? pnlAuth;
        private Panel? pnlSettings;
        private TextBox? txtAuthPassword;
        private Button? btnLogin;
        private TextBox? txtFactoryCode;
        private TextBox? txtLineName;
        private TextBox? txtNewPassword;
        private TextBox? txtConfirmPassword;
        private TextBox? txtStationName;
        private ListBox? listStations;
        private Button? btnAddStation;
        private Button? btnRemoveStation;
        private Button? btnMoveUp;
        private Button? btnMoveDown;
        private Button? btnSave;

        private TerminalConfig _cfg;
        private const string CFG_PATH = "terminal.cfg";

        static readonly Color C_BG = Color.FromArgb(22, 22, 22);
        static readonly Color C_SURFACE = Color.FromArgb(32, 32, 32);
        static readonly Color C_CARD = Color.FromArgb(42, 42, 42);
        static readonly Color C_BORDER = Color.FromArgb(62, 62, 62);
        static readonly Color C_TXTPRI = Color.FromArgb(240, 240, 240);
        static readonly Color C_TXTSEC = Color.FromArgb(145, 145, 145);
        static readonly Color C_GREEN = Color.FromArgb(0, 180, 75);
        static readonly Color C_BLUE = Color.FromArgb(38, 120, 255);
        static readonly Color C_RED_DIM = Color.FromArgb(90, 20, 30);

        public SettingsForm()
        {
            _cfg = TerminalConfig.Load(CFG_PATH);
            BuildUI();
        }

        private void BuildUI()
        {
            this.Text = "Cài đặt Terminal";
            this.Size = new Size(500, 600);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = C_BG;
            this.Font = new Font("Segoe UI", 9f);

            BuildAuthPanel();
            BuildSettingsPanel();
        }

        // ── Panel xác thực ───────────────────────────────────────────
        private void BuildAuthPanel()
        {
            pnlAuth = new Panel { Dock = DockStyle.Fill, BackColor = C_BG };

            var box = new Panel
            {
                Size = new Size(340, 180),
                Location = new Point(70, 150),
                BackColor = C_SURFACE
            };
            box.Paint += (s, e) =>
            {
                using var p = new System.Drawing.Pen(C_BORDER, 1);
                e.Graphics.DrawRectangle(p, 0, 0, box.Width - 1, box.Height - 1);
            };

            box.Controls.Add(new Label
            {
                Text = "🔒  Nhập mật khẩu Quản lý",
                Font = new Font("Segoe UI", 11f),
                ForeColor = C_TXTSEC,
                Location = new Point(0, 24),
                Size = new Size(340, 28),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                BackColor = System.Drawing.Color.Transparent
            });

            txtAuthPassword = new TextBox
            {
                Location = new Point(20, 68),
                Width = 300,
                PasswordChar = '●',
                Font = new Font("Segoe UI", 11f),
                BackColor = C_CARD,
                ForeColor = C_TXTPRI,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtAuthPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoLogin(); };
            box.Controls.Add(txtAuthPassword);

            btnLogin = new Button
            {
                Text = "XÁC NHẬN →",
                Location = new Point(20, 110),
                Size = new Size(300, 38),
                BackColor = C_BLUE,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Click += (s, e) => DoLogin();
            box.Controls.Add(btnLogin);

            pnlAuth.Controls.Add(box);

            // Hiện đường dẫn file config
            pnlAuth.Controls.Add(new Label
            {
                Text = $"Config: {System.IO.Path.GetFullPath(CFG_PATH)}",
                Font = new Font("Segoe UI", 8f),
                ForeColor = C_TXTSEC,
                Location = new Point(16, this.ClientSize.Height - 32),
                Size = new Size(460, 18),
                BackColor = System.Drawing.Color.Transparent
            });

            this.Controls.Add(pnlAuth);
        }

        // ── Panel cài đặt ────────────────────────────────────────────
        private void BuildSettingsPanel()
        {
            pnlSettings = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = C_BG,
                Padding = new Padding(20, 16, 20, 16)
            };

            int y = 16;

            // Factory + Line
            AddLabel(pnlSettings, "Mã xưởng:", 16, y);
            txtFactoryCode = AddTxt(pnlSettings, 150, y, 310); y += 34;

            AddLabel(pnlSettings, "Tên Line:", 16, y);
            txtLineName = AddTxt(pnlSettings, 150, y, 310); y += 34;

            AddSep(pnlSettings, y); y += 14;

            // Mật khẩu
            AddLabel(pnlSettings, "Mật khẩu mới:", 16, y, C_TXTSEC);
            txtNewPassword = AddTxt(pnlSettings, 150, y, 310, pw: true); y += 32;

            AddLabel(pnlSettings, "Xác nhận:", 16, y, C_TXTSEC);
            txtConfirmPassword = AddTxt(pnlSettings, 150, y, 310, pw: true);
            pnlSettings.Controls.Add(new Label
            {
                Text = "(Để trống nếu không đổi)",
                Font = new Font("Segoe UI", 8f, System.Drawing.FontStyle.Italic),
                ForeColor = C_TXTSEC,
                BackColor = System.Drawing.Color.Transparent,
                Location = new System.Drawing.Point(150, y + 28),
                AutoSize = true
            });
            y += 52;

            AddSep(pnlSettings, y); y += 14;

            // Danh sách trạm
            AddLabel(pnlSettings, "Danh sách Trạm:", 16, y); y += 22;

            listStations = new ListBox
            {
                Location = new System.Drawing.Point(16, y),
                Size = new System.Drawing.Size(300, 148),
                BackColor = C_CARD,
                ForeColor = C_TXTPRI,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f)
            };
            pnlSettings.Controls.Add(listStations);

            // Nút điều khiển bên phải list
            btnMoveUp = AddBtn(pnlSettings, "▲", 326, y, 58, 36, C_CARD);
            btnMoveUp.Click += (s, e) => MoveStation(-1);

            btnMoveDown = AddBtn(pnlSettings, "▼", 326, y + 42, 58, 36, C_CARD);
            btnMoveDown.Click += (s, e) => MoveStation(1);

            btnRemoveStation = AddBtn(pnlSettings, "Xóa", 326, y + 112, 58, 36,
                System.Drawing.Color.FromArgb(80, 15, 25));
            btnRemoveStation.ForeColor = System.Drawing.Color.FromArgb(255, 90, 90);
            btnRemoveStation.Click += (s, e) => RemoveStation();

            y += 156;

            // Thêm trạm
            txtStationName = AddTxt(pnlSettings, 16, y, 300, placeholder: "Tên trạm mới...");
            btnAddStation = AddBtn(pnlSettings, "+ Thêm", 326, y, 58, 30, C_BLUE);
            btnAddStation.Click += (s, e) => AddStation();
            txtStationName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) AddStation(); };
            y += 46;

            // Nút Lưu
            btnSave = new Button
            {
                Text = "💾  LƯU CÀI ĐẶT",
                Location = new System.Drawing.Point(16, y),
                Size = new System.Drawing.Size(460 - 32, 44),
                BackColor = C_GREEN,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, System.Drawing.FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;
            pnlSettings.Controls.Add(btnSave);

            this.Controls.Add(pnlSettings);
        }

        // ─────────────────────────────────────────────────────────────
        // LOGIC
        // ─────────────────────────────────────────────────────────────
        private void DoLogin()
        {
            if (_cfg.VerifyPassword(txtAuthPassword?.Text ?? ""))
            {
                pnlAuth!.Visible = false;
                pnlSettings!.Visible = true;
                LoadToForm();
            }
            else
            {
                MessageBox.Show("Mật khẩu không chính xác!", "Từ chối",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtAuthPassword?.Clear();
                txtAuthPassword?.Focus();
            }
        }

        private void LoadToForm()
        {
            txtFactoryCode!.Text = _cfg.FactoryCode;
            txtLineName!.Text = _cfg.LineName;
            listStations!.Items.Clear();
            foreach (var s in _cfg.Stations)
                listStations.Items.Add(s);
        }

        private void AddStation()
        {
            var name = txtStationName?.Text.Trim() ?? "";
            if (string.IsNullOrEmpty(name)) return;
            if (listStations!.Items.Contains(name))
            {
                MessageBox.Show("Trạm này đã có trong danh sách!",
                    "Trùng tên", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            listStations.Items.Add(name);
            txtStationName!.Clear();
            txtStationName.Focus();
        }

        private void RemoveStation()
        {
            if (listStations?.SelectedIndex < 0) return;
            if (listStations!.Items.Count <= 1)
            {
                MessageBox.Show("Phải giữ ít nhất 1 trạm!",
                    "Không thể xóa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int idx = listStations.SelectedIndex;
            listStations.Items.RemoveAt(idx);
            listStations.SelectedIndex = Math.Min(idx, listStations.Items.Count - 1);
        }

        private void MoveStation(int dir)
        {
            if (listStations?.SelectedIndex < 0) return;
            int idx = listStations!.SelectedIndex;
            int newIdx = idx + dir;
            if (newIdx < 0 || newIdx >= listStations.Items.Count) return;
            var item = listStations.Items[idx];
            listStations.Items.RemoveAt(idx);
            listStations.Items.Insert(newIdx, item);
            listStations.SelectedIndex = newIdx;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFactoryCode?.Text) ||
                string.IsNullOrWhiteSpace(txtLineName?.Text))
            {
                MessageBox.Show("Mã xưởng và Tên Line không được để trống!",
                    "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (listStations?.Items.Count == 0)
            {
                MessageBox.Show("Phải có ít nhất 1 trạm!",
                    "Thiếu trạm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string newPw = txtNewPassword?.Text ?? "";
            string cfmPw = txtConfirmPassword?.Text ?? "";
            if (!string.IsNullOrEmpty(newPw))
            {
                if (newPw != cfmPw)
                {
                    MessageBox.Show("Mật khẩu xác nhận không khớp!",
                        "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (newPw.Length < 4)
                {
                    MessageBox.Show("Mật khẩu tối thiểu 4 ký tự!",
                        "Quá ngắn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            try
            {
                _cfg.FactoryCode = txtFactoryCode!.Text.Trim();
                _cfg.LineName = txtLineName!.Text.Trim();
                if (!string.IsNullOrEmpty(newPw)) _cfg.SetPassword(newPw);

                _cfg.Stations.Clear();
                foreach (var item in listStations!.Items)
                    _cfg.Stations.Add(item?.ToString() ?? "");

                // Ghi vào terminal.cfg — giữ nguyên DbPath và các key khác
                _cfg.Save(CFG_PATH);

                MessageBox.Show("Đã lưu thành công!\n\nKhởi động lại ứng dụng nếu đổi tên Line.",
                    "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu: {ex.Message}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // UI HELPERS
        // ─────────────────────────────────────────────────────────────
        private void AddLabel(Panel p, string text, int x, int y, System.Drawing.Color? clr = null)
            => p.Controls.Add(new Label
            {
                Text = text,
                Location = new System.Drawing.Point(x, y + 7),
                AutoSize = true,
                ForeColor = clr ?? C_TXTPRI,
                BackColor = System.Drawing.Color.Transparent,
                Font = new Font("Segoe UI", 9f)
            });

        private TextBox AddTxt(Panel p, int x, int y, int w,
            bool pw = false, string? placeholder = null)
        {
            var tb = new TextBox
            {
                Location = new System.Drawing.Point(x, y),
                Width = w,
                Height = 28,
                BackColor = C_CARD,
                ForeColor = C_TXTPRI,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f)
            };
            if (pw) tb.PasswordChar = '●';
            p.Controls.Add(tb);
            return tb;
        }

        private Button AddBtn(Panel p, string text, int x, int y, int w, int h, System.Drawing.Color bg)
        {
            var b = new Button
            {
                Text = text,
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(w, h),
                BackColor = bg,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            p.Controls.Add(b);
            return b;
        }

        private void AddSep(Panel p, int y)
            => p.Controls.Add(new Panel
            {
                Location = new System.Drawing.Point(16, y + 4),
                Size = new System.Drawing.Size(460, 1),
                BackColor = C_BORDER
            });
    }
}