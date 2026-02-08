using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CodeWalker.GameFiles;

namespace CodeWalker.OIVInstaller
{
    public partial class UninstallForm : Form
    {
        private BackupManager _manager;
        private List<BackupLog> _packages;

        public UninstallForm(string gameFolder)
        {
            InitializeComponent();
            _manager = new BackupManager(gameFolder);
            LoadPackages();
        }

        private void LoadPackages()
        {
            _packages = _manager.GetInstalledPackages();
            lstPackages.Items.Clear();
            foreach (var pkg in _packages)
            {
                string platformTag = pkg.IsGen9 ? "[Gen9]" : "[Legacy]";
                lstPackages.Items.Add($"{platformTag} {pkg.PackageName} ({pkg.InstallDate})");
            }
        }

        private void lstPackages_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnUninstall.Enabled = lstPackages.SelectedIndex >= 0;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private async void btnUninstall_Click(object sender, EventArgs e)
        {
            if (lstPackages.SelectedIndex < 0) return;
            var log = _packages[lstPackages.SelectedIndex];

            // Check for running game process
            while (ProcessHelper.IsGameRunning(out string processName))
            {
                var result = MessageBox.Show(
                    $"The game process '{processName}' is currently running.\n\n" +
                    "Please close the game before uninstalling mods to prevent file locking errors.",
                    "Game is Running",
                    MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Cancel) return;
            }

            UninstallMode mode = UninstallMode.Backup;

            // Custom Dialog for Uninstall Choice
            using (var prompt = new Form())
            {
                prompt.Width = 450;
                prompt.Height = 220;
                prompt.Text = "Uninstall Options";
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;

                var iconBox = new PictureBox { Image = SystemIcons.Question.ToBitmap(), Size = new Size(32, 32), Location = new Point(20, 20) };
                prompt.Controls.Add(iconBox);

                var lbl = new Label 
                { 
                    Text = $"How do you want to uninstall '{log.PackageName}'?",
                    Location = new Point(70, 25),
                    Size = new Size(350, 40)
                };
                prompt.Controls.Add(lbl);

                var btnBackup = new Button 
                { 
                    Text = "Revert to Previous State\n(Using Backups)", 
                    Location = new Point(70, 70), 
                    Size = new Size(160, 50),
                    DialogResult = DialogResult.Yes
                };
                btnBackup.Click += (s, ev) => { mode = UninstallMode.Backup; prompt.Close(); };
                prompt.Controls.Add(btnBackup);

                var btnVanilla = new Button 
                { 
                    Text = "Reset to Vanilla\n(Ignore Backups)", 
                    Location = new Point(240, 70), 
                    Size = new Size(160, 50),
                    DialogResult = DialogResult.OK
                };
                btnVanilla.Click += (s, ev) => { mode = UninstallMode.Vanilla; prompt.Close(); };
                prompt.Controls.Add(btnVanilla);

                var btnCancel = new Button 
                { 
                    Text = "Cancel", 
                    Location = new Point(320, 140), 
                    Size = new Size(80, 25),
                    DialogResult = DialogResult.Cancel
                };
                btnCancel.Click += (s, ev) => { prompt.Close(); };
                prompt.Controls.Add(btnCancel);
                prompt.CancelButton = btnCancel;

                var result = prompt.ShowDialog(this);
                if (result == DialogResult.Cancel) return;
            }

            lblStatus.Text = "Uninstalling...";
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            btnUninstall.Enabled = false;
            lstPackages.Enabled = false;

            try
            {
                // Ensure keys are loaded (needed for RPF operations)
                if (GTA5Keys.PC_AES_KEY == null)
                {
                    lblStatus.Text = "Initializing keys...";
                    await System.Threading.Tasks.Task.Run(() => 
                    {
                        bool isGen9 = File.Exists(Path.Combine(_manager.GameFolder, "eboot.bin")) || 
                                     File.Exists(Path.Combine(_manager.GameFolder, "GTA5_Enhanced.exe"));
                        GTA5Keys.LoadFromPath(_manager.GameFolder, isGen9, null);
                    });
                }

                await System.Threading.Tasks.Task.Run(() => PerformUninstall(log, mode));
                
                MessageBox.Show("Uninstallation complete!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadPackages(); // Refresh list
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during uninstall: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                lblStatus.Text = "";
                progressBar.Visible = false;
                lstPackages.Enabled = true;
                btnUninstall.Enabled = lstPackages.SelectedIndex >= 0;
            }
        }

        private void PerformUninstall(BackupLog log, UninstallMode mode)
        {
            // Delegate actual work to BackupManager
            // We adapt the IProgress<string> to our UpdateStatus method
            var progress = new Progress<string>(msg => 
            {
                // Simple logging adaptation
                if (msg.StartsWith("Reverting:")) return; // Skip redundant msg if we want, or log it
                UpdateStatus(msg);
            });
            
            _manager.Uninstall(log, progress, mode);
        }

        private void UpdateStatus(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(UpdateStatus), text);
                return;
            }
            lblStatus.Text = text;
        }
    }
}

