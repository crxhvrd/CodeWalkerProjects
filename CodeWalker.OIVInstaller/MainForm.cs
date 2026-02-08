using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace CodeWalker.OIVInstaller
{
    public partial class MainForm : Form
    {
        private OivPackage _package;
        private string _gameFolder = ""; // Current install target
        private string _spGameFolder = ""; // Actual GTA V folder
        private int _marqueeStep = 0;
        private int _marqueeWait = 0;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Check command line args for OIV file
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && File.Exists(args[1]) && (args[1].EndsWith(".oiv", StringComparison.OrdinalIgnoreCase) || args[1].EndsWith(".rpf", StringComparison.OrdinalIgnoreCase)))
            {
                txtOivPath.Text = args[1];
                LoadOivPackage(args[1]);
            }
            
            LoadConfig();
            tmrMarquee.Start();
        }
        
        private void btnBrowseOiv_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Select OIV or RPF Package";
                dlg.Filter = "OIV/RPF Packages (*.oiv;*.rpf)|*.oiv;*.rpf|All Files (*.*)|*.*";
                
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    txtOivPath.Text = dlg.FileName;
                    LoadOivPackage(dlg.FileName);
                }
            }
        }
        
        private void btnBrowseGame_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select GTA V Game Folder";
                dlg.ShowNewFolderButton = false;
                
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    txtGameFolder.Text = dlg.SelectedPath;
                    _gameFolder = dlg.SelectedPath;
                    _spGameFolder = dlg.SelectedPath; // User explicitly selected this
                    ValidateGameFolder();
                    UpdateInstallButton();
                    SaveConfig(); // Save on manual selection
                }
            }
        }
        
        private string GetConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string cwFolder = Path.Combine(appData, "CodeWalker");
            if (!Directory.Exists(cwFolder)) Directory.CreateDirectory(cwFolder);
            return Path.Combine(cwFolder, "OivInstaller.json");
        }
        
        private void LoadConfig()
        {
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var config = System.Text.Json.JsonSerializer.Deserialize<OivConfig>(json);
                    if (config != null && !string.IsNullOrEmpty(config.LastGameFolder) && Directory.Exists(config.LastGameFolder))
                    {
                        _gameFolder = config.LastGameFolder;
                        _spGameFolder = config.LastGameFolder;
                        txtGameFolder.Text = _gameFolder;
                        ValidateGameFolder();
                    }
                }
            }
            catch { /* Ignore config load errors */ }
        }
        
        private void SaveConfig()
        {
            try
            {
                // Save the SP folder if valid, otherwise current if valid
                string saveFolder = !string.IsNullOrEmpty(_spGameFolder) ? _spGameFolder : _gameFolder;
                if (string.IsNullOrEmpty(saveFolder)) return;

                var config = new OivConfig { LastGameFolder = saveFolder };
                string json = System.Text.Json.JsonSerializer.Serialize(config);
                File.WriteAllText(GetConfigPath(), json);
            }
            catch { /* Ignore config save errors */ }
        }
        
        private class OivConfig
        {
            public string LastGameFolder { get; set; }
        }

        private void btnUninstall_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_gameFolder) || !Directory.Exists(_gameFolder))
            {
                MessageBox.Show("Please select a valid GTA V folder first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Pass BOTH the SP folder and the FiveM folder
            string spParam = !string.IsNullOrEmpty(_spGameFolder) && Directory.Exists(_spGameFolder) ? _spGameFolder : null;
            // If current _gameFolder is NOT FiveM, it might be SP, so use that if _spGameFolder is empty
            if (spParam == null && !string.IsNullOrEmpty(_gameFolder) && !_gameFolder.Contains("FiveM.app"))
                spParam = _gameFolder;

            using (var form = new UninstallForm(spParam, FiveMHelper.GetFiveMModsFolder()))
            {
                form.ShowDialog(this);
            }
        }

        private void btnDocs_Click(object sender, EventArgs e)
        {
            using (var form = new DocumentationForm())
            {
                form.ShowDialog(this);
            }
        }

        private void tmrMarquee_Tick(object sender, EventArgs e)
        {
            if (lblPackageName.Width <= pnlTitleClipping.Width)
            {
                lblPackageName.Left = 0;
                return;
            }

            if (_marqueeWait > 0)
            {
                _marqueeWait--;
                return;
            }

            if (_marqueeStep == 0) // Moving Left
            {
                lblPackageName.Left -= 1;
                // If text right edge touches panel right edge
                if (lblPackageName.Right <= pnlTitleClipping.Width)
                {
                    lblPackageName.Left = pnlTitleClipping.Width - lblPackageName.Width;
                    _marqueeStep = 1;
                    _marqueeWait = 40; // 2 seconds
                }
            }
            else // Moving Right
            {
                lblPackageName.Left += 1;
                if (lblPackageName.Left >= 0)
                {
                    lblPackageName.Left = 0;
                    _marqueeStep = 0;
                    _marqueeWait = 40;
                }
            }
        }
        
        private void ValidateGameFolder()
        {
            if (string.IsNullOrEmpty(_gameFolder))
            {
                lblGameStatus.Text = "";
                lblGameStatus.ForeColor = Color.Gray;
                lblAsiStatus.Text = "";
                return;
            }

            bool hasLegacy = File.Exists(Path.Combine(_gameFolder, "GTA5.exe"));
            bool hasEnhanced = File.Exists(Path.Combine(_gameFolder, "GTA5_Enhanced.exe"));
            
            bool hasOpenIV = File.Exists(Path.Combine(_gameFolder, "OpenIV.asi"));
            bool hasOpenRPF = File.Exists(Path.Combine(_gameFolder, "OpenRPF.asi"));
            bool hasDinput8 = File.Exists(Path.Combine(_gameFolder, "dinput8.dll"));
            bool hasXinput = File.Exists(Path.Combine(_gameFolder, "xinput1_4.dll"));
            
            string asiStatus = "";
            
            // Check what version the package requires
            var requiredVersion = _package?.Metadata?.GameVersion ?? GameVersion.Any;
            
            if (_package != null && _package.IsFiveM)
            {
                // FiveM validation
                if (string.IsNullOrEmpty(_gameFolder))
                {
                    lblGameStatus.Text = "⚠ FiveM mods folder not found";
                    lblGameStatus.ForeColor = Color.Orange;
                }
                else
                {
                    lblGameStatus.Text = "✓ FiveM mods folder selected";
                    lblGameStatus.ForeColor = Color.Green;
                }
                lblAsiStatus.Text = "";
                return;
            }
            
            if (hasEnhanced)
            {
                if (requiredVersion == GameVersion.Legacy)
                {
                    lblGameStatus.Text = "⚠ Package requires GTA V Legacy, but this is Enhanced";
                    lblGameStatus.ForeColor = Color.Orange;
                }
                else
                {
                    lblGameStatus.Text = "✓ Valid GTA V folder (Enhanced)";
                    lblGameStatus.ForeColor = Color.Green;
                    
                    if (!hasXinput) 
                        asiStatus = "⚠ ASI Loader (xinput1_4.dll) missing";
                    else if (!hasOpenRPF) 
                        asiStatus = "⚠ OpenRPF.asi missing - mods folder disabled";
                }
            }
            else if (hasLegacy)
            {
                if (requiredVersion == GameVersion.Enhanced)
                {
                    lblGameStatus.Text = "⚠ Package requires GTA V Enhanced, but this is Legacy";
                    lblGameStatus.ForeColor = Color.Orange;
                }
                else
                {
                    lblGameStatus.Text = "✓ Valid GTA V folder (Legacy)";
                    lblGameStatus.ForeColor = Color.Green;
                    
                    if (!hasDinput8) 
                         asiStatus = "⚠ ASI Loader (dinput8.dll) missing";
                    else if (!hasOpenIV) 
                        asiStatus = "⚠ OpenIV.asi missing - mods folder disabled";
                }
            }
            else
            {
                lblGameStatus.Text = "⚠ GTA5.exe or GTA5_Enhanced.exe not found";
                lblGameStatus.ForeColor = Color.Orange;
            }
            
            // Update ASI status label
            if (!string.IsNullOrEmpty(asiStatus))
            {
                lblAsiStatus.Text = asiStatus;
                lblAsiStatus.ForeColor = Color.Red;
            }
            else if (hasEnhanced || hasLegacy)
            {
                 if (hasEnhanced && hasOpenRPF)
                 {
                     lblAsiStatus.Text = "✓ OpenRPF.asi installed";
                     lblAsiStatus.ForeColor = Color.Green;
                 }
                 else if (hasLegacy && hasOpenIV)
                 {
                     lblAsiStatus.Text = "✓ OpenIV.asi installed";
                     lblAsiStatus.ForeColor = Color.Green;
                 }
                 else
                 {
                     lblAsiStatus.Text = ""; 
                 }
            }
            else
            {
                lblAsiStatus.Text = "";
            }

            
            // Enable Manage Mods if game folder is potentially valid (has exe)
            if (btnUninstall != null)
            {
                btnUninstall.Enabled = hasLegacy || hasEnhanced;
            }
        }

        private void LoadOivPackage(string path)
        {
            try
            {
                // Dispose previous package
                _package?.Dispose();
                _package = null;
                
                // Check if it's a folder (extracted OIV for testing)
                if (Directory.Exists(path))
                {
                    _package = OivPackage.LoadFromFolder(path);
                }
                else
                {
                    _package = OivPackage.Load(path);
                }
                
                DisplayPackageInfo();
                
                if (_package.IsFiveM)
                {
                    string fivemMods = FiveMHelper.GetFiveMModsFolder();
                    if (!string.IsNullOrEmpty(fivemMods))
                    {
                        _gameFolder = fivemMods;
                        txtGameFolder.Text = _gameFolder;
                    }
                    else
                    {
                        // Fallback or just show empty? 
                        // If FiveM app exists but mods folder doesn't, we can create it?
                        // FiveMHelper checks existance. 
                        // Let's manually construct it if FiveM is installed.
                        if (FiveMHelper.IsFiveMInstalled())
                        {
                            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                            string fiveMAppPath = Path.Combine(localAppData, "FiveM", "FiveM.app");
                            _gameFolder = Path.Combine(fiveMAppPath, "mods");
                            txtGameFolder.Text = _gameFolder;
                        }
                    }
                    ValidateGameFolder();
                }
                
                UpdateInstallButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load OIV package:\n\n{ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisplayPackageInfo()
        {
            if (_package == null) return;
            
            var meta = _package.Metadata;

            // Update window title
            this.Text = $"{meta.Name} - Package Installer";
            
            // Header
            lblPackageName.Text = meta.Name;
            lblAuthor.Text = meta.AuthorDisplayName;
            
            // Description
            rtbDescription.Text = meta.Description.Trim();
            
            // Calculate height with a cap
            Size textSize = TextRenderer.MeasureText(rtbDescription.Text, rtbDescription.Font, new Size(rtbDescription.Width, 0), TextFormatFlags.WordBreak);
            
            // Cap height at 250px (scroll if larger), min 35px
            int targetHeight = Math.Min(Math.Max(textSize.Height + 20, 35), 250);
            
            rtbDescription.Height = targetHeight;
            
            // Layout adjustment
            int spacer = 20;
            panelPaths.Top = rtbDescription.Bottom + spacer;
            panelInfo.Top = panelPaths.Bottom + spacer;
            panelAdditional.Top = panelPaths.Bottom + spacer;
            
            // Resize form to fit content
            // We need to account for the Header (100px) and padding
            // panelInfo.Bottom is relative to panelContent (which starts after Header)
            int requiredClientHeight = 100 + panelInfo.Bottom + 30;
            
            // Hardcoded minimum height to ensure basic UI usability
            int minHeight = 460;
            
            // Apply the required height, respecting the minimum
            int finalHeight = Math.Max(minHeight, requiredClientHeight);
            
            if (this.ClientSize.Height != finalHeight)
            {
                this.ClientSize = new Size(this.ClientSize.Width, finalHeight);
                // We keep MinimumSize at what resizing allows, or reset it to safe minimum if needed
                // But generally, for a fixed single border style, directly setting ClientSize is best
            }
            
            // Information section
            linkAuthor.Text = meta.AuthorDisplayName;
            linkAuthor.Tag = meta.AuthorActionLink;
            lblVersion.Text = meta.Version;
            
            // Update supported game based on package version
            switch (meta.GameVersion)
            {
                case GameVersion.Enhanced:
                    lblGame.Text = "GTA V Enhanced";
                    break;
                case GameVersion.Legacy:
                    lblGame.Text = "GTA V Legacy";
                    break;
                default:
                    lblGame.Text = "GTA V";
                    break;
            }

            if (_package.IsFiveM)
            {
                lblGame.Text = "FiveM";
                lblGame.ForeColor = Color.OrangeRed;
            }
            else
            {
                lblGame.ForeColor = Color.Black; 
            }
            
            // Re-validate game folder if already set
            if (!string.IsNullOrEmpty(_gameFolder))
            {
                ValidateGameFolder();
            }
            
            // Set icon if available
            if (_package.IconData != null && _package.IconData.Length > 0)
            {
                try
                {
                    using (var ms = new MemoryStream(_package.IconData))
                    {
                        picIcon.Image = Image.FromStream(ms);
                    }
                }
                catch { }
            }

            // Reset default theme (Standard Blue)
            Color defaultBlue = Color.FromArgb(0, 120, 215);
            linkAuthor.LinkColor = defaultBlue;
            linkWeb.LinkColor = defaultBlue;
            linkYoutube.LinkColor = defaultBlue;
            btnInstall.ForeColor = Color.Black; 
            btnInstall.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            panelHeader.BackColor = defaultBlue;
            lblPackageName.ForeColor = Color.White;
            lblAuthor.ForeColor = Color.White;
            lblWarning.ForeColor = Color.White;

            // Apply header color if specified
            if (!string.IsNullOrEmpty(meta.HeaderBackground))
            {
                try
                {
                    string colorStr = meta.HeaderBackground.TrimStart('$');
                    if (colorStr.Length == 8)
                    {
                        int argb = Convert.ToInt32(colorStr, 16);
                        Color headerColor = Color.FromArgb(argb);
                        panelHeader.BackColor = headerColor;
                        
                        Color textColor = meta.UseBlackTextColor ? Color.Black : Color.White;
                        lblPackageName.ForeColor = textColor;
                        lblAuthor.ForeColor = textColor;
                        lblWarning.ForeColor = Color.FromArgb(textColor.A, 
                            (int)(textColor.R * 0.8), (int)(textColor.G * 0.8), (int)(textColor.B * 0.8));
                            
                        // Apply dynamic theme to content controls (if Header is dark enough to be visible on white)
                        // If UseBlackTextColor is FALSE, it means Header is Dark (White text used). 
                        // Dark colors work well as text/accents on White backgrounds.
                        if (!meta.UseBlackTextColor)
                        {
                            linkAuthor.LinkColor = headerColor;
                            linkWeb.LinkColor = headerColor;
                            linkYoutube.LinkColor = headerColor;
                            
                            btnInstall.ForeColor = headerColor;
                            btnInstall.FlatAppearance.BorderColor = headerColor;
                        }
                    }
                }
                catch { }
            }
            
            // Additional links
            DisplayAuthorLinks();
        }
        
        private void DisplayAuthorLinks()
        {
            if (_package == null) return;
            
            var meta = _package.Metadata;
            
            // Web Link
            if (!string.IsNullOrEmpty(meta.AuthorWeb))
            {
                linkWeb.Tag = meta.AuthorWeb;
                linkWeb.Visible = true;
            }
            else
            {
                linkWeb.Visible = false;
            }
            
            // YouTube
            if (!string.IsNullOrEmpty(meta.AuthorYoutube))
            {
                string youtubeUrl = meta.AuthorYoutube;
                if (!youtubeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    youtubeUrl = $"https://www.youtube.com/c/{meta.AuthorYoutube}";
                }
                linkYoutube.Tag = youtubeUrl;
                linkYoutube.Visible = true;
            }
            else
            {
                linkYoutube.Visible = false;
            }
        }
        
        private void UpdateInstallButton()
        {
            btnInstall.Enabled = _package != null && !string.IsNullOrEmpty(_gameFolder);
        }
        
        private void lblAuthor_Click(object sender, EventArgs e)
        {
            if (_package?.Metadata?.AuthorActionLink != null)
            {
                OpenUrl(_package.Metadata.AuthorActionLink);
            }
        }
        
        private void linkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (sender is LinkLabel link && link.Tag is string url && !string.IsNullOrEmpty(url))
            {
                OpenUrl(url);
            }
        }
        
        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open link: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnInstall_Click(object sender, EventArgs e)
        {
            if (_package == null || string.IsNullOrEmpty(_gameFolder)) return;

            // Check for running game process
            while (ProcessHelper.IsGameRunning(out string processName))
            {
                var result = MessageBox.Show(
                    $"The game process '{processName}' is currently running.\n\n" +
                    "Please close the game before installing mods to prevent file locking errors.",
                    "Game is Running",
                    MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Cancel) return;
            }

            if (_package.IsFiveM)
            {
                // FiveM Installation Logic
                if (!Directory.Exists(_gameFolder))
                {
                    try 
                    {
                        Directory.CreateDirectory(_gameFolder);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to create mods folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                string sourcePath = _package.SourceRpf.FilePath;
                string destPath = Path.Combine(_gameFolder, Path.GetFileName(sourcePath));

                if (File.Exists(destPath))
                {
                    var result = MessageBox.Show(
                        $"File '{Path.GetFileName(destPath)}' already exists in FiveM mods folder.\nOverwrite?", 
                        "Confirm Overwrite", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.No) return;
                }

                try
                {
                    // Create uninstaller log
                    var manager = new BackupManager(_gameFolder);
                    var session = manager.CreateSession(
                        _package.Metadata.Name, 
                        _package.Metadata.Description, 
                        _package.Metadata.Version, 
                        false // FiveM RPFs are generally not "Gen9" in the console sense, or we don't care about encryption here
                    );

                    string fileName = Path.GetFileName(sourcePath);
                    session.TrackFileAdded(fileName);
                    
                    File.Copy(sourcePath, destPath, true);
                    
                    session.Save();
                    
                    MessageBox.Show("FiveM mod installed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to install mod: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            // Check for existing installations with the same name
            var backupManager = new BackupManager(_gameFolder);
            var existingPackages = backupManager.GetInstalledPackages()
                .Where(x => x.PackageName == _package.Metadata.Name)
                .ToList();

            List<BackupLog> packagesToUninstall = new List<BackupLog>();
            UninstallMode uninstallMode = UninstallMode.Backup;

            if (existingPackages.Count > 0)
            {
                // We need a custom dialog for 3 choices: Uninstall (Backup), Uninstall (Vanilla), Keep
                using (var prompt = new Form())
                {
                    prompt.Width = 500;
                    prompt.Height = 280;
                    prompt.Text = "Conflicting Installation Found";
                    prompt.StartPosition = FormStartPosition.CenterParent;
                    prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                    prompt.MaximizeBox = false;
                    prompt.MinimizeBox = false;

                    var iconBox = new PictureBox { Image = SystemIcons.Warning.ToBitmap(), Size = new Size(32, 32), Location = new Point(20, 20) };
                    prompt.Controls.Add(iconBox);

                    var lbl = new Label 
                    { 
                        Text = $"Found {existingPackages.Count} existing installation(s) of '{_package.Metadata.Name}'.\n\n" +
                               "How would you like to proceed?",
                        Location = new Point(70, 20),
                        Size = new Size(400, 60)
                    };
                    prompt.Controls.Add(lbl);

                    // Option 1: Revert to Backup (Standard)
                    var btnBackup = new Button 
                    { 
                        Text = "Uninstall & Revert to Backups\n(Standard Update)", 
                        Location = new Point(70, 90), 
                        Size = new Size(380, 40),
                        DialogResult = DialogResult.Yes
                    };
                    btnBackup.Click += (s, ev) => { uninstallMode = UninstallMode.Backup; prompt.Close(); };
                    prompt.Controls.Add(btnBackup);

                    // Option 2: Revert to Vanilla
                    var btnVanilla = new Button 
                    { 
                        Text = "Uninstall & Reset to Vanilla\n(Clean Reinstall)", 
                        Location = new Point(70, 135), 
                        Size = new Size(380, 40),
                        DialogResult = DialogResult.OK
                    };
                    btnVanilla.Click += (s, ev) => { uninstallMode = UninstallMode.Vanilla; prompt.Close(); };
                    prompt.Controls.Add(btnVanilla);

                    // Option 3: Keep (Stacking)
                    var btnKeep = new Button 
                    { 
                        Text = "Keep Existing Files\n(Install on top / Stacking)", 
                        Location = new Point(70, 180), 
                        Size = new Size(380, 40),
                        DialogResult = DialogResult.No
                    };
                    btnKeep.Click += (s, ev) => { prompt.Close(); };
                    prompt.Controls.Add(btnKeep);

                    var result = prompt.ShowDialog(this);
                    
                    if (result == DialogResult.Cancel) return; // Closed window
                    
                    if (result == DialogResult.Yes || result == DialogResult.OK)
                    {
                        packagesToUninstall = existingPackages;
                    }
                    else if (result == DialogResult.No)
                    {
                        // Proceed without uninstalling
                    }
                }
            }

            // Switch to log view
            ShowInstallLog();
            
            // Run installation in background to keep UI responsive
            Task.Run(() =>
            {
                try
                {
                    Log("Initializing installation...");
                    Log($"Package: {_package.Metadata.Name}");
                    Log($"Target: {_gameFolder}");
                    
                    var installer = new OivInstaller(_gameFolder, _package, message => Log(message));
                    installer.Install(null, packagesToUninstall, uninstallMode);
                    
                    Log(""); // Spacer
                    Log("Installation completed successfully.");
                    Log("----------------------------------------");
                }
                catch (Exception ex)
                {
                    Log("");
                    Log("ERROR: Installation failed!");
                    Log(ex.Message);
                    Log(ex.StackTrace);
                }
                finally
                {
                    // Enable Done button on UI thread
                    this.Invoke((MethodInvoker)delegate {
                        btnDone.Enabled = true;
                        btnDone.Visible = true;
                    });
                }
            });
        }
        
        private void btnDone_Click(object sender, EventArgs e)
        {
            ShowMainView();
        }
        
        private void ShowInstallLog()
        {
            // main threaded UI updates
            panelPaths.Visible = false;
            panelInfo.Visible = false;
            panelAdditional.Visible = false;
            rtbDescription.Visible = false;
            
            panelLog.Visible = true;
            panelLog.BringToFront();
            
            rtbLog.Clear();
            btnDone.Enabled = false;
            btnDone.Visible = false; 
            
            // Disable main buttons
            btnInstall.Enabled = false;
            btnUninstall.Enabled = false;
        }
        
        private void ShowMainView()
        {
            panelLog.Visible = false;
            
            panelPaths.Visible = true;
            panelInfo.Visible = true;
            panelAdditional.Visible = true;
            rtbDescription.Visible = true;
            
            // Re-enable main buttons
            btnInstall.Enabled = true;
            UpdateInstallButton(); 
            ValidateGameFolder(); // Re-validate to enable Manage Mods if applicable
        }
        
        private void Log(string message)
        {
            if (rtbLog.InvokeRequired)
            {
                rtbLog.Invoke(new Action<string>(Log), message);
                return;
            }
            rtbLog.AppendText(message + Environment.NewLine);
            rtbLog.ScrollToCaret();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _package?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
