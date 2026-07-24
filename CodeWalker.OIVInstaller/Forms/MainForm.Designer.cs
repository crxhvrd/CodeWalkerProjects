namespace CodeWalker.OIVInstaller
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.panelHeader = new System.Windows.Forms.Panel();
            this.btnInstall = new System.Windows.Forms.Button();
            this.btnUninstall = new System.Windows.Forms.Button();
            this.btnDocs = new System.Windows.Forms.Button();
            this.lblAuthor = new System.Windows.Forms.Label();
            this.lblPackageName = new System.Windows.Forms.Label();
            this.picIcon = new System.Windows.Forms.PictureBox();
            this.pnlTitleClipping = new System.Windows.Forms.Panel();
            this.tmrMarquee = new System.Windows.Forms.Timer(this.components);
            this.panelContent = new System.Windows.Forms.Panel();
            this.rtbDescription = new System.Windows.Forms.RichTextBox();
            this.panelPaths = new System.Windows.Forms.Panel();
            this.lblOivLabel = new System.Windows.Forms.Label();
            this.txtOivPath = new System.Windows.Forms.TextBox();
            this.btnBrowseOiv = new System.Windows.Forms.Button();
            this.lblGameFolderLabel = new System.Windows.Forms.Label();
            this.txtGameFolder = new System.Windows.Forms.TextBox();
            this.btnBrowseGame = new System.Windows.Forms.Button();
            this.lblGameStatus = new System.Windows.Forms.Label();
            this.lblAsiStatus = new System.Windows.Forms.Label();
            this.chkSkipBackup = new System.Windows.Forms.CheckBox();
            this.panelInfo = new System.Windows.Forms.Panel();
            this.lblInfoTitle = new System.Windows.Forms.Label();
            this.lblCreator = new System.Windows.Forms.Label();
            this.linkAuthor = new System.Windows.Forms.LinkLabel();
            this.lblGameLabel = new System.Windows.Forms.Label();
            this.lblGame = new System.Windows.Forms.Label();
            this.lblVersionLabel = new System.Windows.Forms.Label();
            this.lblVersion = new System.Windows.Forms.Label();
            this.panelAdditional = new System.Windows.Forms.Panel();
            this.lblAdditionalTitle = new System.Windows.Forms.Label();
            this.linkWeb = new System.Windows.Forms.LinkLabel();
            this.linkYoutube = new System.Windows.Forms.LinkLabel();
            this.lblWarning = new System.Windows.Forms.Label();
            this.panelLog = new System.Windows.Forms.Panel();
            this.progressBar = new CodeWalker.OIVInstaller.SmoothProgressBar();
            this.rtbLog = new System.Windows.Forms.RichTextBox();
            this.btnDone = new System.Windows.Forms.Button();
            this.panelEmptyState = new System.Windows.Forms.Panel();
            this.lblEmptyIcon = new System.Windows.Forms.Label();
            this.lblEmptyTitle = new System.Windows.Forms.Label();
            this.lblEmptySubtitle = new System.Windows.Forms.Label();
            this.linkInstructions = new System.Windows.Forms.LinkLabel();
            this.panelHeader.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picIcon)).BeginInit();
            this.panelContent.SuspendLayout();
            this.panelLog.SuspendLayout();
            this.panelPaths.SuspendLayout();
            this.panelInfo.SuspendLayout();
            this.panelAdditional.SuspendLayout();
            this.panelEmptyState.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelHeader
            // 
            this.panelHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.panelHeader.Controls.Add(this.lblWarning);
            this.panelHeader.Controls.Add(this.lblWarning);
            this.panelHeader.Controls.Add(this.btnUninstall);
            this.panelHeader.Controls.Add(this.btnDocs);
            this.panelHeader.Controls.Add(this.btnInstall);
            this.panelHeader.Controls.Add(this.lblAuthor);
            this.panelHeader.Controls.Add(this.pnlTitleClipping);
            this.panelHeader.Controls.Add(this.lblAuthor);
            this.panelHeader.Controls.Add(this.picIcon);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Size = new System.Drawing.Size(700, 70);
            this.panelHeader.TabIndex = 0;
            // 
            // btnInstall
            // 
            this.btnInstall.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnInstall.BackColor = System.Drawing.Color.White;
            this.btnInstall.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnInstall.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.btnInstall.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.btnInstall.Location = new System.Drawing.Point(560, 20);
            this.btnInstall.Name = "btnInstall";
            this.btnInstall.Size = new System.Drawing.Size(120, 32);
            this.btnInstall.TabIndex = 0;
            this.btnInstall.Text = "Install";
            this.btnInstall.UseVisualStyleBackColor = false;
            this.btnInstall.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnInstall.Enabled = false;
            this.btnInstall.Visible = false;
            this.btnInstall.Click += new System.EventHandler(this.btnInstall_Click);
            // 
            // btnUninstall
            // 
            this.btnUninstall.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnUninstall.BackColor = System.Drawing.Color.White;
            this.btnUninstall.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnUninstall.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.btnUninstall.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnUninstall.ForeColor = System.Drawing.Color.Black;
            this.btnUninstall.Location = new System.Drawing.Point(580, 20);
            this.btnUninstall.Name = "btnUninstall";
            this.btnUninstall.Size = new System.Drawing.Size(100, 32);
            this.btnUninstall.TabIndex = 5;
            this.btnUninstall.Text = "Manage Mods";
            this.btnUninstall.UseVisualStyleBackColor = false;
            this.btnUninstall.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnUninstall.Click += new System.EventHandler(this.btnUninstall_Click);
            // 
            // btnDocs
            // 
            this.btnDocs.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDocs.BackColor = System.Drawing.Color.White;
            this.btnDocs.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDocs.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.btnDocs.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnDocs.ForeColor = System.Drawing.Color.Black;
            this.btnDocs.Location = new System.Drawing.Point(470, 20);
            this.btnDocs.Name = "btnDocs";
            this.btnDocs.Size = new System.Drawing.Size(100, 32);
            this.btnDocs.TabIndex = 6;
            this.btnDocs.Text = "Docs";
            this.btnDocs.UseVisualStyleBackColor = false;
            this.btnDocs.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnDocs.Click += new System.EventHandler(this.btnDocs_Click);
            // 
            // lblWarning
            // 
            this.lblWarning.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblWarning.BackColor = System.Drawing.Color.Transparent;
            this.lblWarning.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblWarning.ForeColor = System.Drawing.Color.White;
            this.lblWarning.Location = new System.Drawing.Point(450, 55);
            this.lblWarning.Name = "lblWarning";
            this.lblWarning.Size = new System.Drawing.Size(240, 40);
            this.lblWarning.TabIndex = 4;
            this.lblWarning.Text = "";
            this.lblWarning.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblAuthor
            // 
            this.lblAuthor.AutoSize = true;
            this.lblAuthor.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.lblAuthor.ForeColor = System.Drawing.Color.White;
            this.lblAuthor.Location = new System.Drawing.Point(95, 55);
            this.lblAuthor.Name = "lblAuthor";
            this.lblAuthor.Size = new System.Drawing.Size(50, 20);
            this.lblAuthor.TabIndex = 2;
            this.lblAuthor.Text = "";
            this.lblAuthor.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblAuthor.Visible = false;
            this.lblAuthor.Click += new System.EventHandler(this.lblAuthor_Click);
            // 
            // 
            // pnlTitleClipping
            //
            this.pnlTitleClipping.Controls.Add(this.lblPackageName);
            this.pnlTitleClipping.BackColor = System.Drawing.Color.Transparent;
            this.pnlTitleClipping.Location = new System.Drawing.Point(24, 18);
            this.pnlTitleClipping.Name = "pnlTitleClipping";
            this.pnlTitleClipping.Size = new System.Drawing.Size(235, 35);
            this.pnlTitleClipping.TabIndex = 1;
            // 
            // lblPackageName
            // 
            this.lblPackageName.AutoSize = true;
            this.lblPackageName.Font = new System.Drawing.Font("Segoe UI", 18F);
            this.lblPackageName.ForeColor = System.Drawing.Color.White;
            this.lblPackageName.Location = new System.Drawing.Point(0, 0);
            this.lblPackageName.Name = "lblPackageName";
            this.lblPackageName.Size = new System.Drawing.Size(200, 32);
            this.lblPackageName.TabIndex = 0;
            this.lblPackageName.Text = "Select Package";
            // 
            // tmrMarquee
            // 
            this.tmrMarquee.Interval = 50;
            this.tmrMarquee.Tick += new System.EventHandler(this.tmrMarquee_Tick);
            //  
            // picIcon
            // 
            this.picIcon.BackColor = System.Drawing.Color.Transparent;
            this.picIcon.Location = new System.Drawing.Point(20, 18);
            this.picIcon.Name = "picIcon";
            this.picIcon.Size = new System.Drawing.Size(65, 65);
            this.picIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picIcon.TabIndex = 0;
            this.picIcon.TabStop = false;
            this.picIcon.Visible = false;
            //
            // panelLog
            //
            this.panelLog.Controls.Add(this.btnDone);
            this.panelLog.Controls.Add(this.rtbLog);
            this.panelLog.Controls.Add(this.progressBar);
            this.panelLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLog.Location = new System.Drawing.Point(20, 15);
            this.panelLog.Name = "panelLog";
            this.panelLog.Size = new System.Drawing.Size(660, 330);
            this.panelLog.TabIndex = 10;
            this.panelLog.Visible = false;
            //
            // progressBar
            //
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.BarColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.progressBar.TrackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(232)))), ((int)(((byte)(232)))));
            this.progressBar.Location = new System.Drawing.Point(0, 0);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(660, 4);
            this.progressBar.TabIndex = 2;
            //
            // rtbLog
            //
            this.rtbLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.rtbLog.BackColor = System.Drawing.Color.White;
            this.rtbLog.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rtbLog.Font = new System.Drawing.Font("Consolas", 9F);
            this.rtbLog.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.rtbLog.Location = new System.Drawing.Point(0, 12);
            this.rtbLog.Name = "rtbLog";
            this.rtbLog.ReadOnly = true;
            this.rtbLog.Size = new System.Drawing.Size(660, 278);
            this.rtbLog.TabIndex = 0;
            this.rtbLog.Text = "";
            // 
            // btnDone
            // 
            this.btnDone.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDone.BackColor = System.Drawing.Color.White;
            this.btnDone.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDone.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnDone.Location = new System.Drawing.Point(540, 300);
            this.btnDone.Name = "btnDone";
            this.btnDone.Size = new System.Drawing.Size(120, 30);
            this.btnDone.TabIndex = 1;
            this.btnDone.Text = "Done";
            this.btnDone.UseVisualStyleBackColor = false;
            this.btnDone.Click += new System.EventHandler(this.btnDone_Click);
            // 
            // panelContent
            // 
            this.panelContent.Controls.Add(this.panelLog);
            this.panelContent.Controls.Add(this.panelAdditional);
            this.panelContent.Controls.Add(this.panelInfo);
            this.panelContent.Controls.Add(this.panelPaths);
            this.panelContent.Controls.Add(this.linkInstructions);
            this.panelContent.Controls.Add(this.rtbDescription);
            this.panelContent.Controls.Add(this.panelEmptyState);
            this.panelContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelContent.Location = new System.Drawing.Point(0, 70);
            this.panelContent.Name = "panelContent";
            this.panelContent.Padding = new System.Windows.Forms.Padding(20, 15, 20, 15);
            this.panelContent.Size = new System.Drawing.Size(700, 310);
            this.panelContent.TabIndex = 1;
            // 
            // rtbDescription
            // 
            this.rtbDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.rtbDescription.BackColor = System.Drawing.Color.White;
            this.rtbDescription.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rtbDescription.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.rtbDescription.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.rtbDescription.Location = new System.Drawing.Point(20, 15);
            this.rtbDescription.Name = "rtbDescription";
            this.rtbDescription.ReadOnly = true;
            this.rtbDescription.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.rtbDescription.Size = new System.Drawing.Size(660, 100);
            this.rtbDescription.TabIndex = 0;
            this.rtbDescription.Text = "";
            this.rtbDescription.Visible = false;
            // 
            // panelPaths
            // 
            this.panelPaths.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.panelPaths.Controls.Add(this.chkSkipBackup);
            this.panelPaths.Controls.Add(this.lblAsiStatus);
            this.panelPaths.Controls.Add(this.lblGameStatus);
            this.panelPaths.Controls.Add(this.btnBrowseGame);
            this.panelPaths.Controls.Add(this.txtGameFolder);
            this.panelPaths.Controls.Add(this.lblGameFolderLabel);
            this.panelPaths.Controls.Add(this.btnBrowseOiv);
            this.panelPaths.Controls.Add(this.txtOivPath);
            this.panelPaths.Controls.Add(this.lblOivLabel);
            this.panelPaths.Location = new System.Drawing.Point(20, 160);
            this.panelPaths.Name = "panelPaths";
            this.panelPaths.Size = new System.Drawing.Size(660, 125);
            this.panelPaths.TabIndex = 1;
            // 
            // lblOivLabel
            // 
            this.lblOivLabel.AutoSize = true;
            this.lblOivLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblOivLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.lblOivLabel.Location = new System.Drawing.Point(0, 5);
            this.lblOivLabel.Name = "lblOivLabel";
            this.lblOivLabel.Size = new System.Drawing.Size(80, 15);
            this.lblOivLabel.TabIndex = 0;
            this.lblOivLabel.Text = "Mod Package:";
            // 
            // txtOivPath
            // 
            this.txtOivPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.txtOivPath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtOivPath.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtOivPath.Location = new System.Drawing.Point(85, 3);
            this.txtOivPath.Name = "txtOivPath";
            this.txtOivPath.ReadOnly = true;
            this.txtOivPath.Size = new System.Drawing.Size(495, 23);
            this.txtOivPath.TabIndex = 1;
            // 
            // btnBrowseOiv
            // 
            this.btnBrowseOiv.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseOiv.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnBrowseOiv.Location = new System.Drawing.Point(585, 2);
            this.btnBrowseOiv.Name = "btnBrowseOiv";
            this.btnBrowseOiv.Size = new System.Drawing.Size(75, 25);
            this.btnBrowseOiv.TabIndex = 2;
            this.btnBrowseOiv.Text = "Browse...";
            this.btnBrowseOiv.UseVisualStyleBackColor = true;
            this.btnBrowseOiv.Click += new System.EventHandler(this.btnBrowseOiv_Click);
            // 
            // lblGameFolderLabel
            // 
            this.lblGameFolderLabel.AutoSize = true;
            this.lblGameFolderLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblGameFolderLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.lblGameFolderLabel.Location = new System.Drawing.Point(0, 35);
            this.lblGameFolderLabel.Name = "lblGameFolderLabel";
            this.lblGameFolderLabel.Size = new System.Drawing.Size(90, 15);
            this.lblGameFolderLabel.TabIndex = 3;
            this.lblGameFolderLabel.Text = "GTA V Folder:";
            // 
            // txtGameFolder
            // 
            this.txtGameFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.txtGameFolder.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtGameFolder.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtGameFolder.Location = new System.Drawing.Point(85, 33);
            this.txtGameFolder.Name = "txtGameFolder";
            this.txtGameFolder.ReadOnly = true;
            this.txtGameFolder.Size = new System.Drawing.Size(495, 23);
            this.txtGameFolder.TabIndex = 4;
            // 
            // btnBrowseGame
            // 
            this.btnBrowseGame.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseGame.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnBrowseGame.Location = new System.Drawing.Point(585, 32);
            this.btnBrowseGame.Name = "btnBrowseGame";
            this.btnBrowseGame.Size = new System.Drawing.Size(75, 25);
            this.btnBrowseGame.TabIndex = 5;
            this.btnBrowseGame.Text = "Browse...";
            this.btnBrowseGame.UseVisualStyleBackColor = true;
            this.btnBrowseGame.Click += new System.EventHandler(this.btnBrowseGame_Click);
            // 
            // lblGameStatus
            // 
            this.lblGameStatus.AutoSize = true;
            this.lblGameStatus.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblGameStatus.ForeColor = System.Drawing.Color.Gray;
            this.lblGameStatus.Location = new System.Drawing.Point(85, 60);
            this.lblGameStatus.Name = "lblGameStatus";
            this.lblGameStatus.Size = new System.Drawing.Size(0, 13);
            this.lblGameStatus.TabIndex = 6;
            //
            // lblAsiStatus
            //
            this.lblAsiStatus.AutoSize = true;
            this.lblAsiStatus.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblAsiStatus.ForeColor = System.Drawing.Color.Gray;
            this.lblAsiStatus.Location = new System.Drawing.Point(85, 78);
            this.lblAsiStatus.Name = "lblAsiStatus";
            this.lblAsiStatus.Size = new System.Drawing.Size(0, 13);
            this.lblAsiStatus.TabIndex = 7;
            //
            // chkSkipBackup
            //
            this.chkSkipBackup.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.chkSkipBackup.AutoSize = true;
            this.chkSkipBackup.Cursor = System.Windows.Forms.Cursors.Hand;
            this.chkSkipBackup.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.chkSkipBackup.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.chkSkipBackup.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(95)))), ((int)(((byte)(95)))), ((int)(((byte)(95)))));
            this.chkSkipBackup.Location = new System.Drawing.Point(380, 100);
            this.chkSkipBackup.Name = "chkSkipBackup";
            this.chkSkipBackup.Size = new System.Drawing.Size(280, 17);
            this.chkSkipBackup.TabIndex = 8;
            this.chkSkipBackup.Text = "Skip backup (faster, can’t uninstall later)";
            this.chkSkipBackup.UseVisualStyleBackColor = true;
            // 
            // panelInfo
            // 
            this.panelInfo.Controls.Add(this.lblVersionLabel);
            this.panelInfo.Controls.Add(this.lblVersion);
            this.panelInfo.Controls.Add(this.lblGameLabel);
            this.panelInfo.Controls.Add(this.lblGame);
            this.panelInfo.Controls.Add(this.lblCreator);
            this.panelInfo.Controls.Add(this.linkAuthor);
            this.panelInfo.Controls.Add(this.lblInfoTitle);
            this.panelInfo.Location = new System.Drawing.Point(20, 155);
            this.panelInfo.Name = "panelInfo";
            this.panelInfo.Size = new System.Drawing.Size(300, 130);
            this.panelInfo.TabIndex = 2;
            this.panelInfo.Visible = false;
            // 
            // lblInfoTitle
            // 
            this.lblInfoTitle.AutoSize = true;
            this.lblInfoTitle.Font = new System.Drawing.Font("Segoe UI", 14F);
            this.lblInfoTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.lblInfoTitle.Location = new System.Drawing.Point(0, 0);
            this.lblInfoTitle.Name = "lblInfoTitle";
            this.lblInfoTitle.Size = new System.Drawing.Size(110, 25);
            this.lblInfoTitle.TabIndex = 0;
            this.lblInfoTitle.Text = "Information";
            // 
            // lblCreator
            // 
            this.lblCreator.AutoSize = true;
            this.lblCreator.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.lblCreator.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.lblCreator.Location = new System.Drawing.Point(0, 35);
            this.lblCreator.Name = "lblCreator";
            this.lblCreator.Size = new System.Drawing.Size(56, 17);
            this.lblCreator.TabIndex = 1;
            this.lblCreator.Text = "Creator";
            // 
            // linkAuthor
            // 
            this.linkAuthor.AutoSize = true;
            this.linkAuthor.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.linkAuthor.LinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.linkAuthor.Location = new System.Drawing.Point(120, 35);
            this.linkAuthor.Name = "linkAuthor";
            this.linkAuthor.Size = new System.Drawing.Size(50, 17);
            this.linkAuthor.TabIndex = 2;
            this.linkAuthor.TabStop = true;
            this.linkAuthor.Text = "";
            this.linkAuthor.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            // 
            // lblGameLabel
            // 
            this.lblGameLabel.AutoSize = true;
            this.lblGameLabel.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.lblGameLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.lblGameLabel.Location = new System.Drawing.Point(0, 58);
            this.lblGameLabel.Name = "lblGameLabel";
            this.lblGameLabel.Size = new System.Drawing.Size(96, 17);
            this.lblGameLabel.TabIndex = 3;
            this.lblGameLabel.Text = "Supported game";
            // 
            // lblGame
            // 
            this.lblGame.AutoSize = true;
            this.lblGame.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.lblGame.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.lblGame.Location = new System.Drawing.Point(120, 58);
            this.lblGame.Name = "lblGame";
            this.lblGame.Size = new System.Drawing.Size(50, 17);
            this.lblGame.TabIndex = 4;
            this.lblGame.Text = "GTA V";
            // 
            // lblVersionLabel
            // 
            this.lblVersionLabel.AutoSize = true;
            this.lblVersionLabel.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.lblVersionLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.lblVersionLabel.Location = new System.Drawing.Point(0, 81);
            this.lblVersionLabel.Name = "lblVersionLabel";
            this.lblVersionLabel.Size = new System.Drawing.Size(52, 17);
            this.lblVersionLabel.TabIndex = 5;
            this.lblVersionLabel.Text = "Version";
            // 
            // lblVersion
            // 
            this.lblVersion.AutoSize = true;
            this.lblVersion.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.lblVersion.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.lblVersion.Location = new System.Drawing.Point(120, 81);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(30, 17);
            this.lblVersion.TabIndex = 6;
            this.lblVersion.Text = "1.0";
            // 
            // panelAdditional
            // 
            this.panelAdditional.Controls.Add(this.linkYoutube);
            this.panelAdditional.Controls.Add(this.linkWeb);
            this.panelAdditional.Controls.Add(this.lblAdditionalTitle);
            this.panelAdditional.Location = new System.Drawing.Point(380, 155);
            this.panelAdditional.Name = "panelAdditional";
            this.panelAdditional.Size = new System.Drawing.Size(300, 130);
            this.panelAdditional.TabIndex = 3;
            this.panelAdditional.Visible = false;
            // 
            // lblAdditionalTitle
            // 
            this.lblAdditionalTitle.AutoSize = true;
            this.lblAdditionalTitle.Font = new System.Drawing.Font("Segoe UI", 14F);
            this.lblAdditionalTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.lblAdditionalTitle.Location = new System.Drawing.Point(0, 0);
            this.lblAdditionalTitle.Name = "lblAdditionalTitle";
            this.lblAdditionalTitle.Size = new System.Drawing.Size(100, 25);
            this.lblAdditionalTitle.TabIndex = 0;
            this.lblAdditionalTitle.Text = "Additional";
            // 
            // linkWeb
            // 
            this.linkWeb.AutoSize = true;
            this.linkWeb.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.linkWeb.LinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.linkWeb.Location = new System.Drawing.Point(0, 35);
            this.linkWeb.Name = "linkWeb";
            this.linkWeb.Size = new System.Drawing.Size(80, 17);
            this.linkWeb.TabIndex = 1;
            this.linkWeb.TabStop = true;
            this.linkWeb.Text = "Homepage";
            this.linkWeb.Visible = false;
            this.linkWeb.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            // 
            // linkYoutube
            // 
            this.linkYoutube.AutoSize = true;
            this.linkYoutube.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.linkYoutube.LinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.linkYoutube.Location = new System.Drawing.Point(0, 58);
            this.linkYoutube.Name = "linkYoutube";
            this.linkYoutube.Size = new System.Drawing.Size(60, 17);
            this.linkYoutube.TabIndex = 2;
            this.linkYoutube.TabStop = true;
            this.linkYoutube.Text = "YouTube";
            this.linkYoutube.Visible = false;
            this.linkYoutube.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            //
            // panelEmptyState
            //
            this.panelEmptyState.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.panelEmptyState.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.panelEmptyState.Controls.Add(this.lblEmptyIcon);
            this.panelEmptyState.Controls.Add(this.lblEmptyTitle);
            this.panelEmptyState.Controls.Add(this.lblEmptySubtitle);
            this.panelEmptyState.Location = new System.Drawing.Point(20, 15);
            this.panelEmptyState.Name = "panelEmptyState";
            this.panelEmptyState.Size = new System.Drawing.Size(660, 130);
            this.panelEmptyState.TabIndex = 11;
            //
            // lblEmptyIcon
            //
            this.lblEmptyIcon.AutoSize = true;
            this.lblEmptyIcon.BackColor = System.Drawing.Color.Transparent;
            this.lblEmptyIcon.Font = new System.Drawing.Font("Segoe MDL2 Assets", 28F);
            this.lblEmptyIcon.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(160)))), ((int)(((byte)(175)))));
            this.lblEmptyIcon.Location = new System.Drawing.Point(0, 0);
            this.lblEmptyIcon.Name = "lblEmptyIcon";
            this.lblEmptyIcon.Text = "";
            this.lblEmptyIcon.TabIndex = 0;
            //
            // lblEmptyTitle
            //
            this.lblEmptyTitle.AutoSize = true;
            this.lblEmptyTitle.BackColor = System.Drawing.Color.Transparent;
            this.lblEmptyTitle.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.lblEmptyTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(75)))), ((int)(((byte)(85)))));
            this.lblEmptyTitle.Location = new System.Drawing.Point(0, 0);
            this.lblEmptyTitle.Name = "lblEmptyTitle";
            // GDI+ text rendering: GDI puts an uneven glyph gap in this string at
            // 11pt Segoe UI ("m od") because 11pt maps to a fractional pixel size.
            this.lblEmptyTitle.UseCompatibleTextRendering = true;
            this.lblEmptyTitle.Text = "Drop a mod package here";
            this.lblEmptyTitle.TabIndex = 1;
            //
            // lblEmptySubtitle
            //
            this.lblEmptySubtitle.AutoSize = true;
            this.lblEmptySubtitle.BackColor = System.Drawing.Color.Transparent;
            this.lblEmptySubtitle.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblEmptySubtitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(130)))), ((int)(((byte)(135)))), ((int)(((byte)(145)))));
            this.lblEmptySubtitle.Location = new System.Drawing.Point(0, 0);
            this.lblEmptySubtitle.Name = "lblEmptySubtitle";
            this.lblEmptySubtitle.Text = "or click Browse below to select a file";
            this.lblEmptySubtitle.TabIndex = 2;
            //
            // linkInstructions
            //
            this.linkInstructions.AutoSize = true;
            this.linkInstructions.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.linkInstructions.LinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.linkInstructions.Location = new System.Drawing.Point(20, 120);
            this.linkInstructions.Name = "linkInstructions";
            this.linkInstructions.Size = new System.Drawing.Size(110, 15);
            this.linkInstructions.TabIndex = 12;
            this.linkInstructions.TabStop = true;
            this.linkInstructions.Text = "View install steps →";
            this.linkInstructions.Visible = false;
            this.linkInstructions.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkInstructions_LinkClicked);
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(700, 380);
            this.Controls.Add(this.panelContent);
            this.Controls.Add(this.panelHeader);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = new System.Drawing.Size(716, 419);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CodeWalker - Package Installer";
            this.AllowDrop = true;
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.MainForm_DragEnter);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.MainForm_DragDrop);
            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picIcon)).EndInit();
            this.panelContent.ResumeLayout(false);
            this.panelPaths.ResumeLayout(false);
            this.panelPaths.PerformLayout();
            this.panelInfo.ResumeLayout(false);
            this.panelInfo.PerformLayout();
            this.panelAdditional.ResumeLayout(false);
            this.panelAdditional.PerformLayout();
            this.panelLog.ResumeLayout(false);
            this.panelEmptyState.ResumeLayout(false);
            this.panelEmptyState.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.PictureBox picIcon;
        private System.Windows.Forms.Panel pnlTitleClipping;
        private System.Windows.Forms.Label lblPackageName;
        private System.Windows.Forms.Label lblAuthor;
        private System.Windows.Forms.Timer tmrMarquee;
        private System.Windows.Forms.Button btnInstall;
        private System.Windows.Forms.Button btnUninstall;
        private System.Windows.Forms.Button btnDocs;
        private System.Windows.Forms.Label lblWarning;
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.RichTextBox rtbDescription;
        private System.Windows.Forms.Panel panelPaths;
        private System.Windows.Forms.Label lblOivLabel;
        private System.Windows.Forms.TextBox txtOivPath;
        private System.Windows.Forms.Button btnBrowseOiv;
        private System.Windows.Forms.Label lblGameFolderLabel;
        private System.Windows.Forms.TextBox txtGameFolder;
        private System.Windows.Forms.Button btnBrowseGame;
        private System.Windows.Forms.Label lblGameStatus;
        private System.Windows.Forms.Label lblAsiStatus;
        private System.Windows.Forms.CheckBox chkSkipBackup;
        private System.Windows.Forms.Panel panelInfo;
        private System.Windows.Forms.Label lblInfoTitle;
        private System.Windows.Forms.Label lblCreator;
        private System.Windows.Forms.LinkLabel linkAuthor;
        private System.Windows.Forms.Label lblGameLabel;
        private System.Windows.Forms.Label lblGame;
        private System.Windows.Forms.Label lblVersionLabel;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Panel panelAdditional;
        private System.Windows.Forms.Label lblAdditionalTitle;
        private System.Windows.Forms.LinkLabel linkWeb;
        private System.Windows.Forms.LinkLabel linkYoutube;
        private System.Windows.Forms.Panel panelLog;
        private CodeWalker.OIVInstaller.SmoothProgressBar progressBar;
        private System.Windows.Forms.RichTextBox rtbLog;
        private System.Windows.Forms.Button btnDone;
        private System.Windows.Forms.Panel panelEmptyState;
        private System.Windows.Forms.Label lblEmptyIcon;
        private System.Windows.Forms.Label lblEmptyTitle;
        private System.Windows.Forms.Label lblEmptySubtitle;
        private System.Windows.Forms.LinkLabel linkInstructions;
    }
}
