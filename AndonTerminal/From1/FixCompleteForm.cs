using System;
using System.Drawing;
using System.Windows.Forms;

namespace AndonTerminal.Forms
{
    public class FixCompleteForm : Form
    {
        // 2 biến này sẽ hứng dữ liệu KTV gõ vào
        public string ErrorReason { get; private set; } = string.Empty;
        public string FixNote { get; private set; } = string.Empty;

        private TextBox _txtReason;
        private TextBox _txtFix;

        public FixCompleteForm()
        {
            this.Text = "Xác nhận Hoàn thành Sửa chữa";
            this.Size = new Size(400, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            // Ô 1: Nguyên nhân lỗi
            this.Controls.Add(new Label { Text = "Nguyên nhân lỗi:", Location = new Point(20, 20), AutoSize = true });
            _txtReason = new TextBox { Location = new Point(20, 45), Width = 340, Height = 60, Multiline = true };
            this.Controls.Add(_txtReason);

            // Ô 2: Cách khắc phục
            this.Controls.Add(new Label { Text = "Cách khắc phục:", Location = new Point(20, 120), AutoSize = true });
            _txtFix = new TextBox { Location = new Point(20, 145), Width = 340, Height = 60, Multiline = true };
            this.Controls.Add(_txtFix);

            // Nút Xác nhận
            var btnOk = new Button { Text = "✓ Đã sửa xong", Location = new Point(130, 230), Size = new Size(120, 40) };
            btnOk.Click += (s, e) => {
                // Ép KTV phải nhập nguyên nhân
                if (string.IsNullOrWhiteSpace(_txtReason.Text))
                {
                    MessageBox.Show("Vui lòng nhập Nguyên nhân lỗi!");
                    return;
                }
                if (string.IsNullOrWhiteSpace(_txtFix.Text))
                {
                    MessageBox.Show("Vui lòng nhập Cách khắc phục!");
                    return;
                }

                ErrorReason = _txtReason.Text.Trim();
                FixNote = _txtFix.Text.Trim();
                this.DialogResult = DialogResult.OK;
            };
            this.Controls.Add(btnOk);
        }
    }
}