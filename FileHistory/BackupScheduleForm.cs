using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileHistory
{
    public partial class BackupScheduleForm : Form
    {
        IBackupScheduler _backupScheduler { get; set; }

        public BackupScheduleForm(IBackupScheduler backupScheduler)
        {
            _backupScheduler = backupScheduler;
            InitializeComponent();
            Text = Strings.Get("Schedule_Title");
            label1.Text = Strings.Get("Schedule_Count");
        }

        private void BackupScheduleForm_Load(object sender, EventArgs e)
        {
            listBackupSchedule.Columns.Add(new ColumnHeader { Text = Strings.Get("Schedule_ColFileName"), Width = 600, });
            listBackupSchedule.Columns.Add(new ColumnHeader { Text = Strings.Get("Schedule_ColLastUpdate"), Width = 300, });
            listBackupSchedule.Columns.Add(new ColumnHeader { Text = Strings.Get("Schedule_ColScheduledTime"), Width = 300, });
            Task.Factory.StartNew(() =>
            {
                var count = 0;
                var schedules = _backupScheduler.GetScheduleItems();
                var listViewItems = new List<ListViewItem>();
                foreach (var schedule in schedules)
                {
                    foreach (var item in schedule.Item2)
                    {
                        count++;
                        listViewItems.Add(new ListViewItem(new string[] { item.FileDbEntry.Name, item.AttributeDbEntry.LastUpdate.ToString(), schedule.Item1.ToShortTimeString() }));
                    }
                }
                Invoke(((Action)(() => {
                    listBackupSchedule.Items.AddRange(listViewItems.ToArray());
                })));
                Invoke(((Action)(() => { numberBackupSchedule.Text = count.ToString("#,0"); })));
            });
        }
    }
}
