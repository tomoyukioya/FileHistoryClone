namespace FileHistory
{
    partial class BackupScheduleForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BackupScheduleForm));
            this.label1 = new System.Windows.Forms.Label();
            this.numberBackupSchedule = new System.Windows.Forms.TextBox();
            this.listBackupSchedule = new System.Windows.Forms.ListView();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(165, 25);
            this.label1.TabIndex = 0;
            this.label1.Text = "バックアップ候補数：";
            // 
            // numberBackupSchedule
            // 
            this.numberBackupSchedule.Location = new System.Drawing.Point(183, 9);
            this.numberBackupSchedule.Name = "numberBackupSchedule";
            this.numberBackupSchedule.ReadOnly = true;
            this.numberBackupSchedule.Size = new System.Drawing.Size(190, 31);
            this.numberBackupSchedule.TabIndex = 1;
            this.numberBackupSchedule.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // listBackupSchedule
            // 
            this.listBackupSchedule.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBackupSchedule.HideSelection = false;
            this.listBackupSchedule.Location = new System.Drawing.Point(12, 58);
            this.listBackupSchedule.Name = "listBackupSchedule";
            this.listBackupSchedule.Size = new System.Drawing.Size(1310, 380);
            this.listBackupSchedule.TabIndex = 2;
            this.listBackupSchedule.UseCompatibleStateImageBehavior = false;
            this.listBackupSchedule.View = System.Windows.Forms.View.Details;
            // 
            // BackupScheduleForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1334, 450);
            this.Controls.Add(this.listBackupSchedule);
            this.Controls.Add(this.numberBackupSchedule);
            this.Controls.Add(this.label1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "BackupScheduleForm";
            this.Text = "バックアップ候補";
            this.Load += new System.EventHandler(this.BackupScheduleForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox numberBackupSchedule;
        private System.Windows.Forms.ListView listBackupSchedule;
    }
}