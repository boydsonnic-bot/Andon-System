using System;
using System.Drawing;
using System.Windows.Forms;

namespace AndonTerminal.Forms
{
    public class EmployeeInputForm : Form
    {
        // Khai báo 2 biến để Form chính lấy dữ liệu
        public string EmployeeId { get; private set; } = string.Empty;
        public string EmployeeName { get; private set; } = string.Empty;

        private TextBox _txtId;
        private TextBox _txtName;

        public EmployeeInputForm()
        {
            this.Text = "Xác nhận danh tính";
            this.Size = new Size(350, 250);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // --- VẼ Ô NHẬP MÃ NHÂN VIÊN ---
            var lblId = new Label { Text = "Mã nhân viên:", Location = new Point(20, 20), AutoSize = true };
            _txtId = new TextBox { Location = new Point(20, 45), Width = 290, Font = new Font("Arial", 12) };
            this.Controls.Add(lblId);
            this.Controls.Add(_txtId);

            // --- VẼ Ô NHẬP TÊN NHÂN VIÊN ---
            var lblName = new Label { Text = "Tên nhân viên:", Location = new Point(20, 90), AutoSize = true };
            _txtName = new TextBox { Location = new Point(20, 115), Width = 290, Font = new Font("Arial", 12) };
            this.Controls.Add(lblName);
            this.Controls.Add(_txtName);

            // --- NÚT XÁC NHẬN ---
            var btnOk = new Button { Text = "Xác nhận", Location = new Point(120, 160), Size = new Size(100, 35) };
            this.Controls.Add(btnOk);

            btnOk.Click += (s, e) =>
            {
                string inputId = _txtId.Text.Trim();
                string inputName = _txtName.Text.Trim();

                // LOGIC CHẶN: Phải điền CẢ 2 ô
                if (string.IsNullOrEmpty(inputId) || string.IsNullOrEmpty(inputName))
                {
                    MessageBox.Show("Vui lòng nhập ĐẦY ĐỦ cả Mã và Tên nhân viên!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return; // Chặn lại, không cho tắt Form
                }

                // Nếu ok thì lưu vào biến và đóng Form
                EmployeeId = inputId;
                EmployeeName = inputName;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            this.AcceptButton = btnOk; // Bấm Enter là xác nhận
        }
    }
}