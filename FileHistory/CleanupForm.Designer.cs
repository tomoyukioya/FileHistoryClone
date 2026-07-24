namespace FileHistory
{
    partial class CleanupForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CleanupForm));
            buttonClose = new System.Windows.Forms.Button();
            label1 = new System.Windows.Forms.Label();
            ScanCountTextBox = new System.Windows.Forms.TextBox();
            progressBar1 = new System.Windows.Forms.ProgressBar();
            buttonStartStop = new System.Windows.Forms.Button();
            comboBox = new System.Windows.Forms.ComboBox();
            DeleteCountTextBox = new System.Windows.Forms.TextBox();
            label2 = new System.Windows.Forms.Label();
            labelNote = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // buttonClose
            // 
            buttonClose.Location = new System.Drawing.Point(299, 107);
            buttonClose.Margin = new System.Windows.Forms.Padding(2);
            buttonClose.Name = "buttonClose";
            buttonClose.Size = new System.Drawing.Size(78, 24);
            buttonClose.TabIndex = 0;
            buttonClose.Text = "閉じる";
            buttonClose.UseVisualStyleBackColor = true;
            buttonClose.Click += buttonClose_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(12, 13);
            label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(100, 15);
            label1.TabIndex = 1;
            label1.Text = "スキャンファイル数：";
            // 
            // ScanCountTextBox
            // 
            ScanCountTextBox.Location = new System.Drawing.Point(142, 5);
            ScanCountTextBox.Margin = new System.Windows.Forms.Padding(2);
            ScanCountTextBox.Name = "ScanCountTextBox";
            ScanCountTextBox.ReadOnly = true;
            ScanCountTextBox.Size = new System.Drawing.Size(236, 23);
            ScanCountTextBox.TabIndex = 2;
            ScanCountTextBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // progressBar1
            // 
            progressBar1.Location = new System.Drawing.Point(11, 74);
            progressBar1.Margin = new System.Windows.Forms.Padding(2);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new System.Drawing.Size(366, 20);
            progressBar1.TabIndex = 3;
            // 
            // buttonStartStop
            // 
            buttonStartStop.Location = new System.Drawing.Point(219, 107);
            buttonStartStop.Name = "buttonStartStop";
            buttonStartStop.Size = new System.Drawing.Size(75, 23);
            buttonStartStop.TabIndex = 6;
            buttonStartStop.Text = "削除";
            buttonStartStop.UseVisualStyleBackColor = true;
            buttonStartStop.Click += buttonStartStop_Click;
            // 
            // comboBox
            // 
            comboBox.FormattingEnabled = true;
            comboBox.Location = new System.Drawing.Point(11, 108);
            comboBox.Name = "comboBox";
            comboBox.Size = new System.Drawing.Size(202, 23);
            comboBox.TabIndex = 7;
            // 
            // DeleteCountTextBox
            // 
            DeleteCountTextBox.Location = new System.Drawing.Point(141, 32);
            DeleteCountTextBox.Margin = new System.Windows.Forms.Padding(2);
            DeleteCountTextBox.Name = "DeleteCountTextBox";
            DeleteCountTextBox.ReadOnly = true;
            DeleteCountTextBox.Size = new System.Drawing.Size(236, 23);
            DeleteCountTextBox.TabIndex = 9;
            DeleteCountTextBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(11, 40);
            label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(89, 15);
            label2.TabIndex = 8;
            label2.Text = "削除ファイル数：";
            //
            // labelNote
            //
            labelNote.AutoSize = true;
            labelNote.ForeColor = System.Drawing.SystemColors.GrayText;
            labelNote.Location = new System.Drawing.Point(12, 140);
            labelNote.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            labelNote.MaximumSize = new System.Drawing.Size(366, 0);
            labelNote.Name = "labelNote";
            labelNote.Size = new System.Drawing.Size(100, 15);
            labelNote.TabIndex = 10;
            labelNote.Text = "注意書き";
            //
            // CleanupForm
            //
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(393, 192);
            ControlBox = false;
            Controls.Add(labelNote);
            Controls.Add(DeleteCountTextBox);
            Controls.Add(label2);
            Controls.Add(comboBox);
            Controls.Add(buttonStartStop);
            Controls.Add(progressBar1);
            Controls.Add(ScanCountTextBox);
            Controls.Add(label1);
            Controls.Add(buttonClose);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(2);
            Name = "CleanupForm";
            Text = "ファイル削除";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button buttonClose;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox ScanCountTextBox;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Button buttonStartStop;
        private System.Windows.Forms.ComboBox comboBox;
        private System.Windows.Forms.TextBox DeleteCountTextBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label labelNote;
    }
}