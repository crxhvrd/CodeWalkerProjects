using System;
using System.Drawing;
using System.Windows.Forms;

namespace CodeWalker.OIVInstaller
{
    public class DocumentationForm : Form
    {
        public DocumentationForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Documentation & Roadmap";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = false;
            this.MaximizeBox = false;

            TabControl tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;

            // Tab 1: User Guide
            TabPage tabGuide = new TabPage("User Guide");
            RichTextBox rtbGuide = new RichTextBox();
            rtbGuide.Dock = DockStyle.Fill;
            rtbGuide.ReadOnly = true;
            rtbGuide.BackColor = Color.White;
            rtbGuide.BorderStyle = BorderStyle.None;
            rtbGuide.Padding = new Padding(10);
            rtbGuide.Text = GetUserGuideText();
            tabGuide.Controls.Add(rtbGuide);
            tabControl.TabPages.Add(tabGuide);

            // Tab 2: Todo / Roadmap
            TabPage tabTodo = new TabPage("Developer Todo");
            RichTextBox rtbTodo = new RichTextBox();
            rtbTodo.Dock = DockStyle.Fill;
            rtbTodo.ReadOnly = true;
            rtbTodo.BackColor = Color.White;
            rtbTodo.BorderStyle = BorderStyle.None;
            rtbTodo.Padding = new Padding(10);
            rtbTodo.Text = GetTodoText();
            tabTodo.Controls.Add(rtbTodo);

            tabControl.TabPages.Add(tabTodo);

            // Tab 3: Feature Support
            TabPage tabFeatures = new TabPage("Feature Support");
            RichTextBox rtbFeatures = new RichTextBox();
            rtbFeatures.Dock = DockStyle.Fill;
            rtbFeatures.ReadOnly = true;
            rtbFeatures.BackColor = Color.White;
            rtbFeatures.BorderStyle = BorderStyle.None;
            rtbFeatures.Padding = new Padding(10);
            rtbFeatures.Text = GetFeatureSupportText();
            tabFeatures.Controls.Add(rtbFeatures);
            tabControl.TabPages.Add(tabFeatures);

            this.Controls.Add(tabControl);
            
            // Add close button at bottom
            Panel bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Height = 50;
            
            Button btnClose = new Button();
            btnClose.Text = "Close";
            btnClose.DialogResult = DialogResult.OK;
            btnClose.Location = new Point(this.Width - 100, 10);
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            bottomPanel.Controls.Add(btnClose);
            
            this.Controls.Add(bottomPanel);
        }

        private string GetUserGuideText()
        {
            return 
@"OIV Installer - User Guide
==========================

1. Installation
   - Drag and drop an .OIV file onto the window, or use the 'Browse OIV' button.
   - Select your GTA V game folder request.
   - Click 'Install'.

2. Uninstalling & Conflicts
   - If you try to install a package that is already installed, the installer will detect the conflict.
   - You can choose to:
     A) Uninstall & Revert to Backup: 
        - Smart Revert: Attempts to undo specific text/XML changes, preserving other mods' edits.
        - Full Backup: Restores exact file backup if smart revert is not applicable.
     B) Uninstall & Reset to Vanilla: Wipes the files and replaces them with original game versions.
     C) Keep Existing: Installs the new mod on top of the old one (Stacking).

3. Managing Mods
   - Use the 'Manage Mods' button (if available) to see a list of installed packages.
   - You can uninstall packages individually from there.

4. Backups
   - Backups are stored in your game folder under 'OIV_Uninstall_Data'.
   - Do not verify/repair via Steam/Epic/Launcher while mods are installed if you plan to uninstall them via this tool, as it might desync the backup state.

5. Supported Features (OpenIV 2.2 Format)
   - Full metadata support (name, version, author, description, colors).
   - XML editing with XPath (add, replace, remove operations).
   - Text file editing (insert, replace, delete operations).
   - PSO/META editing inside RPF archives (YMT, YMF, YMAP, YTYP).
   - Nested RPF creation (createIfNotExist at any depth).
   - Gen9 (Expanded & Enhanced) support.
   - <gameversion> validation (Warns if package requires Enhanced/Legacy mismatch).

";
        }

        private string GetTodoText()
        {
            return
@"Developer Todo List / Roadmap
=============================

COMPLETED FEATURES:
[x] PSO file editing inside RPF archives (YMT, YMF, YMAP, YTYP, PSO)
[x] Nested RPF creation (createIfNotExist at any depth)
[x] XML append positions (First, Last, Before, After)
[x] Path normalization fixes (mods\mods duplication bug)
[x] Parent chain header refresh for deeply nested RPFs
[x] Smart Text/XML Revert on uninstall (reverses specific additions/edits instead of full file revert)

KNOWN LIMITATIONS:
- OpenIV may report validation errors on RPFs with 2+ levels of nesting,
  but these files open correctly in CodeWalker.

REMAINING TODO:

1. Archive Management
   [ ] Implement actual 'Defragmentation' logic (currently a placeholder).

2. Multi-Game Support
   [ ] Test GTA IV / EFLC / Max Payne 3 archive formats (IMG3, RPF2-4).

3. UI Improvements
   [ ] Display extended metadata (License, Social Media links) in the main window.
   [ ] Add 'Dark Mode' or theme support based on OIV package colors.

4. Core Features
   [ ] Add full transaction support for safer installations (rollback on crash).
   [ ] Validating 'Condition' attributes for file content more rigorously.

5. Enhanced Installation Features
   [ ] Implement 'Enhanced' mod installation flow (Vortex-style).
       - Allow installing not only a single mod package but also choosing optional components.

";
        }

        private string GetFeatureSupportText()
        {
            return
@"OpenIV 2.2 Feature Support & Uninstall Logic
============================================

The following table details how each OIV 2.2 feature is handled during installation and uninstallation (Manage Mods).

1. FILE OPERATIONS
------------------
- <add> (New File)
  Install: Copies file to game/mods folder.
  Uninstall: Deletes the file.

- <add> (Replace File)
  Install: Backs up original file, then overwrites.
  Uninstall: Restores the original file from backup.

- <delete>
  Install: Backs up target file, then deletes it.
  Uninstall: Restores the deleted file from backup.

2. ARCHIVE OPERATIONS
---------------------
- <archive> (createIfNotExist=""True"")
  Install: Creates new RPF archive.
  Uninstall: Deletes the created archive.

- <archive> (Edit Existing)
  Install: Opens archive to perform inner operations.
  Uninstall: Reverses inner operations (see below).

3. TEXT EDITING (Smart Revert)
------------------------------
- <add> (Append Line)
- <insert> (Before/After)
- <replace>
- <delete>

  Install: Tracks specific line changes.
  Uninstall: SMART REVERT - Attempts to reverse ONLY the specific lines changed by this mod.
             (e.g., removes inserted lines, restores replaced lines).
             If Smart Revert fails, restores the full file backup.

4. XML / PSO EDITING (Smart Revert)
-----------------------------------
- <add> (First/Last/Before/After)
- <replace>
- <remove>

  Install: Tracks specific XPath operations.
  Uninstall: SMART REVERT - Attempts to reverse ONLY the specific node changes.
             (e.g. removes added nodes, restores removed nodes).
             If Smart Revert fails, restores the full file backup.

5. METADATA & COLORS
--------------------
- Handled purely by the installer UI. No game file impact.

6. COMPATIBILITY CHECKS
-----------------------
- <gameversion>
  Validates if the target game folder (Legacy/Enhanced) matches the package requirement.
  Shows a warning if mismatched, but permits installation.

7. ADD-ON CONTENT (DLCs)
------------------------
- Addon RPFs (e.g. dlc.rpf)
  Install: Copied via <content> or created via <archive>. Use backup system to track as 'Added'.
  Uninstall: The added .rpf file is DELETED. Parent folders (e.g. dlcpacks/modname) are removed if empty.

- dlclist.xml Updates
  Install: Typically done via XML <add> command.
  Uninstall: Reversed via Smart XML Revert (the added line is removed), keeping other mods intact.
";
        }
    }
}
