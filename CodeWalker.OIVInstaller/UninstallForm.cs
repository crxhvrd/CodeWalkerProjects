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
        private List<BackupManager> _managers = new List<BackupManager>();
        private Dictionary<BackupLog, BackupManager> _logSource = new Dictionary<BackupLog, BackupManager>();
        private List<BackupLog> _allPackages = new List<BackupLog>();

        public UninstallForm(string gameFolder, string fiveMFolder = null)
        {
            InitializeComponent();
            
            if (!string.IsNullOrEmpty(gameFolder) && Directory.Exists(gameFolder))
            {
                _managers.Add(new BackupManager(gameFolder));
            }

            if (!string.IsNullOrEmpty(fiveMFolder) && Directory.Exists(fiveMFolder))
            {
                // Avoid adding same folder twice if user selected FiveM folder as game folder
                if (string.IsNullOrEmpty(gameFolder) || !string.Equals(Path.GetFullPath(gameFolder), Path.GetFullPath(fiveMFolder), StringComparison.OrdinalIgnoreCase))
                {
                    _managers.Add(new BackupManager(fiveMFolder));
                }
            }
            
            this.Text = "Uninstall/Manage Mods";
            LoadPackages();
        }

        private void LoadPackages()
        {
            lstPackages.Items.Clear();
            _allPackages.Clear();
            _logSource.Clear();

            foreach (var manager in _managers)
            {
                var mgrPackages = manager.GetInstalledPackages();
                string folderName = new DirectoryInfo(manager.GameFolder).Name;
                bool isFiveM = folderName.Equals("mods", StringComparison.OrdinalIgnoreCase) && manager.GameFolder.Contains("FiveM", StringComparison.OrdinalIgnoreCase);

                foreach (var pkg in mgrPackages)
                {
                    _allPackages.Add(pkg);
                    _logSource[pkg] = manager;

                    string platformTag = pkg.IsGen9 ? "[Gen9]" : "[Legacy]";
                    string sourceTag = isFiveM ? "[FiveM]" : "[SP]";
                    
                    lstPackages.Items.Add($"{sourceTag} {platformTag} {pkg.PackageName} ({pkg.InstallDate})");
                }
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
            var log = _allPackages[lstPackages.SelectedIndex];
            var manager = _logSource[log];

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

            bool isFiveM = manager.GameFolder.Contains("FiveM", StringComparison.OrdinalIgnoreCase);

            if (isFiveM)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to uninstall '{log.PackageName}'?\nThis will remove the file from your FiveM mods folder.",
                    "Confirm Uninstall",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No) return;
                
                // For FiveM, "Backup" mode (default) will handle "Added" files by deleting them.
                // "Vanilla" mode would do the same.
                mode = UninstallMode.Backup;
            }
            else
            {
                // Custom Dialog for Uninstall Choice (SP only)
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
            }

            lblStatus.Text = "Uninstalling...";
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            btnUninstall.Enabled = false;
            lstPackages.Enabled = false;

            try
            {
                // Ensure keys are loaded (needed for RPF operations)
                // Ensure keys are loaded (needed for RPF operations)
                // For FiveM mods (RPF files), we might not need keys if we are just deleting the file.
                // But if we ever do deep RPF content revert, we might need them.
                // We try to load keys, but don't fail if we can't find game exe (FiveM mode).
                 if (GTA5Keys.PC_AES_KEY == null)
                {
                    await System.Threading.Tasks.Task.Run(() => 
                    {
                        try
                        {
                            bool isGen9 = File.Exists(Path.Combine(manager.GameFolder, "eboot.bin")) || 
                                         File.Exists(Path.Combine(manager.GameFolder, "GTA5_Enhanced.exe"));
                            
                             GTA5Keys.LoadFromPath(manager.GameFolder, isGen9, null);
                        }
                        catch 
                        {
                            // Ignore key loading failure for FiveM / standalone usage
                        }
                    });
                }

                await System.Threading.Tasks.Task.Run(() => PerformUninstall(manager, log, mode));
                
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

        private void PerformUninstall(BackupManager manager, BackupLog log, UninstallMode mode)
        {
            // Delegate actual work to BackupManager
            // We adapt the IProgress<string> to our UpdateStatus method
            var progress = new Progress<string>(msg => 
            {
                // Simple logging adaptation
                if (msg.StartsWith("Reverting:")) return; // Skip redundant msg if we want, or log it
                UpdateStatus(msg);
            });
            
            manager.Uninstall(log, progress, mode);
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

