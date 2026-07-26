using CodeWalker.GameFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CodeWalker.Forms
{
    public partial class FxcForm : Form
    {
        private FxcFile Fxc;
        private AwcShaderFile AwcShader;
        private AwcShader SelectedAwcShader;
        private RpfFileEntry rpfFileEntry;
        private ExploreForm exploreForm;

        private string fileName;
        public string FileName
        {
            get { return fileName; }
            set
            {
                fileName = value;
                UpdateFormTitle();
            }
        }
        public string FilePath { get; set; }


        public FxcForm()
        {
            InitializeComponent();
            UpdateAwcModeUi(awcMode: false);
            this.Activated += (s, e) => RefreshEditModeUi();
        }

        private bool IsEditable => AwcShader != null && (exploreForm?.EditMode ?? false);

        private void RefreshEditModeUi()
        {
            if (AwcShader == null) return;
            bool editable = IsEditable;
            SaveMenuItem.Enabled = editable && rpfFileEntry != null;
            ImportCsoMenuItem.Enabled = editable && SelectedAwcShader != null;
        }


        private void UpdateFormTitle()
        {
            string suffix = AwcShader != null ? "FXDB Shader Library Viewer" : "FXC Viewer";
            Text = fileName + " - " + suffix + " - CodeWalker by dexyfex";
        }

        private bool AwcHasEffects => AwcShader != null && AwcShader.Effects != null && AwcShader.Effects.Length > 0;

        private void UpdateAwcModeUi(bool awcMode)
        {
            // Menu items only meaningful in AWC mode. Save / Import are further
            // gated on RPF Explorer's edit mode in RefreshEditModeUi() and on
            // selection in ShaderContextMenu_Opening.
            SaveMenuItem.Enabled = false;
            SaveAsMenuItem.Enabled = awcMode;
            ExportAllMenuItem.Enabled = awcMode;

            // Search/type filter only for AWC (FXC list is small and unsegmented).
            // SearchPanel is docked Top; toggling visibility lets the docked
            // ShadersListView fill the freed space automatically.
            SearchPanel.Visible = awcMode;

            // Hide Type column in FXC mode
            ShadersTypeColumn.Width = awcMode ? 40 : 0;

            // Tree view replaces the flat list when AWC has a decoded effects
            // database; otherwise (FXC mode or AWC fallback) the flat list is
            // shown. The two controls share the same dock-fill panel.
            bool useTree = awcMode && AwcHasEffects;
            EffectsTreeView.Visible = useTree;
            ShadersListView.Visible = !useTree;

            // Re-enable the Techniques tab in AWC mode now that we decode them.
            // (The tab is informational only — the new effects tree is the
            // primary navigation surface.)
            if (!MainTabControl.TabPages.Contains(TechniquesTabPage))
                MainTabControl.TabPages.Insert(1, TechniquesTabPage);
        }


        public void LoadFxc(FxcFile fxc)
        {
            Fxc = fxc;
            AwcShader = null;
            UpdateAwcModeUi(awcMode: false);

            fileName = fxc?.Name;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = fxc?.FileEntry?.Name;
            }

            UpdateFormTitle();

            DetailsPropertyGrid.SelectedObject = fxc;


            ShadersListView.Items.Clear();
            TechniquesListView.Items.Clear();
            if ((fxc == null) || (fxc.Shaders == null)) return;

            foreach (var shader in fxc.Shaders)
            {
                var item = ShadersListView.Items.Add(string.Empty); // Type col empty in FXC mode
                item.SubItems.Add(shader.Name);
                item.Tag = shader;
            }

            if (fxc.Techniques != null)
            {
                foreach (var technique in fxc.Techniques)
                {
                    var item = TechniquesListView.Items.Add(technique.ToString());
                    item.Tag = technique;
                }
            }


            StatusLabel.Text = (fxc.Shaders?.Length ?? 0) + " shaders, " + (fxc.Techniques?.Length ?? 0) + " techniques";
        }


        public void LoadAwcShader(AwcShaderFile awc, RpfFileEntry entry, ExploreForm owner)
        {
            Fxc = null;
            AwcShader = awc;
            rpfFileEntry = entry;
            exploreForm = owner;
            UpdateAwcModeUi(awcMode: true);

            fileName = entry?.Name ?? awc?.Name;
            UpdateFormTitle();

            DetailsPropertyGrid.SelectedObject = awc;

            if (AwcHasEffects)
            {
                RebuildEffectsTree();
            }
            else
            {
                RebuildShadersList();
            }
            RefreshEditModeUi();

            StatusLabel.Text = BuildAwcStatus();
        }

        private string BuildAwcStatus()
        {
            if (AwcShader == null) return "Ready";
            string shaders = AwcShader.TotalShaderCount + " shaders ("
                + AwcShader.VertexCount + " VS, "
                + AwcShader.PixelCount + " PS, "
                + AwcShader.GeometryCount + " GS, "
                + AwcShader.DomainCount + " DS, "
                + AwcShader.HullCount + " HS, "
                + AwcShader.ComputeCount + " CS)";
            if (AwcHasEffects) shaders = AwcShader.EffectCount + " effects, " + shaders;
            return shaders;
        }

        private void RebuildShadersList()
        {
            ShadersListView.BeginUpdate();
            try
            {
                ShadersListView.Items.Clear();
                if (AwcShader == null) return;

                string filter = SearchTextBox.Text?.Trim();
                bool hasFilter = !string.IsNullOrEmpty(filter);

                foreach (var s in AwcShader.AllShaders())
                {
                    if (hasFilter && (s.Name == null || s.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)) continue;

                    var item = ShadersListView.Items.Add(s.StageName);
                    item.SubItems.Add(s.Name);
                    item.Tag = s;
                }
            }
            finally
            {
                ShadersListView.EndUpdate();
            }
        }

        // ---------- AWC effects tree ----------

        private void RebuildEffectsTree()
        {
            EffectsTreeView.BeginUpdate();
            try
            {
                EffectsTreeView.Nodes.Clear();
                if (!AwcHasEffects) return;

                string filter = SearchTextBox.Text?.Trim();
                bool hasFilter = !string.IsNullOrEmpty(filter);

                // Look up table of global shader index -> AwcShader per stage.
                var vs = AwcShader.VertexShaders ?? new AwcShader[0];
                var ps = AwcShader.PixelShaders ?? new AwcShader[0];
                var gs = AwcShader.GeometryShaders ?? new AwcShader[0];
                var ds = AwcShader.DomainShaders ?? new AwcShader[0];
                var hs = AwcShader.HullShaders ?? new AwcShader[0];
                var cs = AwcShader.ComputeShaders ?? new AwcShader[0];

                foreach (var eff in AwcShader.Effects)
                {
                    if (!EffectMatchesFilter(eff, filter, hasFilter)) continue;

                    var effNode = new TreeNode(BuildEffectNodeText(eff));
                    effNode.Tag = eff;

                    AddStageNodes(effNode, "Vertex Shaders",   eff.VsIndices, vs, AwcShaderStage.Vertex);
                    AddStageNodes(effNode, "Pixel Shaders",    eff.PsIndices, ps, AwcShaderStage.Pixel);
                    AddStageNodes(effNode, "Geometry Shaders", eff.GsIndices, gs, AwcShaderStage.Geometry);
                    AddStageNodes(effNode, "Domain Shaders",   eff.DsIndices, ds, AwcShaderStage.Domain);
                    AddStageNodes(effNode, "Hull Shaders",     eff.HsIndices, hs, AwcShaderStage.Hull);
                    AddStageNodes(effNode, "Compute Shaders",  eff.CsIndices, cs, AwcShaderStage.Compute);

                    if (eff.Techniques != null && eff.Techniques.Length > 0)
                    {
                        var techRoot = effNode.Nodes.Add("Techniques (" + eff.Techniques.Length + ")");
                        techRoot.Tag = null;
                        foreach (var t in eff.Techniques)
                        {
                            var tn = new TreeNode(t.Name + " (" + (t.Passes?.Length ?? 0) + " passes)");
                            tn.Tag = t;
                            if (t.Passes != null)
                            {
                                foreach (var pass in t.Passes)
                                {
                                    var pn = new TreeNode(pass.Name ?? string.Empty);
                                    pn.Tag = pass;
                                    tn.Nodes.Add(pn);
                                }
                            }
                            techRoot.Nodes.Add(tn);
                        }
                    }

                    // Effect States subfolder — lists rasterizer/depth-stencil/
                    // blend/sampler state-block counts plus the render-shader-set
                    // count. Even though the state-block bodies aren't currently
                    // decoded for SGD2, the counts on AwcEffect expose the size
                    // metadata that's available.
                    int rsc = eff.RasterizerStateCount, dssc = eff.DepthStencilStateCount;
                    int bsc = eff.BlendStateCount, sssc = eff.SamplerStateCount, rssc = eff.RenderShaderSetCount;
                    if ((rsc | dssc | bsc | sssc | rssc) > 0)
                    {
                        var statesRoot = effNode.Nodes.Add("Effect States");
                        statesRoot.Tag = null;
                        statesRoot.Nodes.Add("Rasterizer (" + rsc + ")").Tag = null;
                        statesRoot.Nodes.Add("DepthStencil (" + dssc + ")").Tag = null;
                        statesRoot.Nodes.Add("Blend (" + bsc + ")").Tag = null;
                        statesRoot.Nodes.Add("Sampler (" + sssc + ")").Tag = null;
                        statesRoot.Nodes.Add("RenderShaderSets (" + rssc + ")").Tag = null;
                    }

                    if (eff.SamplerNames != null && eff.SamplerNames.Length > 0)
                    {
                        var sampRoot = effNode.Nodes.Add("Samplers: " + string.Join(", ", eff.SamplerNames));
                        sampRoot.Tag = null;
                    }

                    EffectsTreeView.Nodes.Add(effNode);
                }
            }
            finally
            {
                EffectsTreeView.EndUpdate();
            }
        }

        private static string BuildEffectNodeText(AwcEffect e)
        {
            return e.Name + " (" + e.TotalShaderCount + " shaders, " + e.TechniqueCount + " techniques)";
        }

        private static bool EffectMatchesFilter(AwcEffect e, string filter, bool hasFilter)
        {
            if (!hasFilter) return true;
            if (e.Name != null && e.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (e.Techniques != null)
            {
                foreach (var t in e.Techniques)
                {
                    if (t.Name != null && t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            return false;
        }

        private void AddStageNodes(TreeNode parent, string label, uint[] indices, AwcShader[] globalArr, AwcShaderStage stage)
        {
            if (indices == null || indices.Length == 0) return;

            var stageNode = new TreeNode(label + " (" + indices.Length + ")");
            stageNode.Tag = null;
            foreach (var idx in indices)
            {
                AwcShader s = (idx < globalArr.Length) ? globalArr[idx] : null;
                string text = s != null ? s.Name : ("<missing #" + idx + ">");
                var node = new TreeNode(text);
                node.Tag = s;
                stageNode.Nodes.Add(node);
            }
            parent.Nodes.Add(stageNode);
        }

        private void EffectsTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var tag = e.Node?.Tag;
            if (tag is AwcShader s)
            {
                LoadAwcShader(s);
            }
            else if (tag is AwcEffect eff)
            {
                LoadEffectSummary(eff);
            }
            else if (tag is AwcEffectTechnique tech)
            {
                LoadTechniqueSummary(tech);
            }
            else if (tag is AwcEffectPass pass)
            {
                LoadPassSummary(pass);
            }
            else
            {
                // Grouping nodes ("Vertex Shaders", "Techniques", "Samplers")
                LoadAwcShader((AwcShader)null);
            }
        }

        private void LoadEffectSummary(AwcEffect eff)
        {
            SelectedAwcShader = null;
            DetailsPropertyGrid.SelectedObject = eff;
            ShaderPanel.Enabled = true;
            // Also populate the Techniques tab with this effect's techniques.
            TechniquesListView.Items.Clear();
            if (eff.Techniques != null)
            {
                foreach (var t in eff.Techniques)
                {
                    var item = TechniquesListView.Items.Add(t.Name ?? string.Empty);
                    item.Tag = t;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("// Effect: " + eff.Name);
            sb.AppendLine("// DataBufferSize: 0x" + eff.DataBufferSize.ToString("X8") + " (" + eff.DataBufferSize + ")");
            sb.AppendLine("// Shaders: " + eff.TotalShaderCount
                + "  (VS=" + eff.VsCount + " PS=" + eff.PsCount + " GS=" + eff.GsCount
                + " DS=" + eff.DsCount + " HS=" + eff.HsCount + " CS=" + eff.CsCount + ")");
            sb.AppendLine("// Techniques: " + eff.TechniqueCount);
            sb.AppendLine("// Samplers:   " + eff.SamplerCount);
            sb.AppendLine("// PropEntries:" + (eff.PropEntries?.Length ?? 0));
            sb.AppendLine("// States:     Rasterizer=" + eff.RasterizerStateCount
                + " DepthStencil=" + eff.DepthStencilStateCount
                + " Blend=" + eff.BlendStateCount
                + " Sampler=" + eff.SamplerStateCount
                + " RenderShaderSets=" + eff.RenderShaderSetCount);
            sb.AppendLine();
            if (eff.SamplerNames != null && eff.SamplerNames.Length > 0)
            {
                sb.AppendLine("// Sampler names:");
                foreach (var n in eff.SamplerNames) sb.AppendLine("//   " + n);
                sb.AppendLine();
            }
            if (eff.Techniques != null)
            {
                foreach (var t in eff.Techniques)
                {
                    sb.AppendLine("technique " + t.Name);
                    sb.AppendLine("{");
                    if (t.Passes != null)
                    {
                        foreach (var p in t.Passes)
                        {
                            sb.AppendLine("    pass " + p.Name + " { }");
                        }
                    }
                    sb.AppendLine("}");
                }
            }
            ShaderTextBox.Text = sb.ToString();
        }

        private void LoadTechniqueSummary(AwcEffectTechnique tech)
        {
            SelectedAwcShader = null;
            DetailsPropertyGrid.SelectedObject = tech;
            ShaderPanel.Enabled = true;
            var sb = new StringBuilder();
            sb.AppendLine("technique " + tech.Name);
            sb.AppendLine("{");
            if (tech.Passes != null)
            {
                foreach (var p in tech.Passes)
                {
                    sb.AppendLine("    pass " + p.Name + " { }");
                }
            }
            sb.AppendLine("}");
            ShaderTextBox.Text = sb.ToString();
        }

        private void LoadPassSummary(AwcEffectPass pass)
        {
            SelectedAwcShader = null;
            DetailsPropertyGrid.SelectedObject = pass;
            ShaderPanel.Enabled = true;
            ShaderTextBox.Text = "// pass " + (pass.Name ?? string.Empty);
        }

        private void LoadShader(FxcShader s)
        {
            if (s == null)
            {
                ShaderPanel.Enabled = false;
                ShaderTextBox.Text = string.Empty;
            }
            else
            {
                ShaderPanel.Enabled = true;
                FxcParser.ParseShader(s);
                if (!string.IsNullOrEmpty(s.LastError))
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("Error: ");
                    sb.AppendLine(s.LastError);
                    sb.AppendLine();
                    sb.AppendLine(s.Disassembly);
                    ShaderTextBox.Text = sb.ToString();
                }
                else
                {
                    ShaderTextBox.Text = s.Disassembly;
                }
            }
        }

        private void LoadAwcShader(AwcShader s)
        {
            SelectedAwcShader = s;
            DetailsPropertyGrid.SelectedObject = (object)s ?? AwcShader;

            if (s == null)
            {
                ShaderPanel.Enabled = false;
                ShaderTextBox.Text = string.Empty;
                return;
            }
            ShaderPanel.Enabled = true;

            ShaderTextBox.Text = BuildShaderHeader(s);
        }

        private static string BuildShaderHeader(AwcShader s)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// " + s.StageName + " " + s.Name);
            sb.AppendLine("// Hash:    " + s.HashHex);
            sb.AppendLine("// Wave:    " + s.WaveSize);
            sb.AppendLine("// Size:    " + s.Size + " bytes");
            sb.AppendLine("// Block:   " + s.BlockSize + " bytes");
            sb.AppendLine("// Counts:  reg=" + s.RegCount + " cb=" + s.CBufferCount + " tex=" + s.TexCount);

            if (s.Registers != null && s.Registers.Length > 0)
            {
                sb.AppendLine("//");
                sb.AppendLine("// Registers:");
                foreach (var r in s.Registers)
                {
                    sb.Append("//   ").Append(r.Slot.PadRight(10)).Append(' ').Append((r.Name ?? string.Empty).PadRight(32)).Append("  (").Append(r.ResourceType).AppendLine(")");
                    if (r.CBuffers != null && r.CBuffers.Length > 0)
                    {
                        foreach (var cb in r.CBuffers)
                        {
                            sb.Append("//     +0x").Append(cb.PackOffset.ToString("X4")).Append("  ")
                              .Append(cb.Type).Append(cb.ArraySize > 1 ? "[" + cb.ArraySize + "]" : string.Empty)
                              .Append("  ").AppendLine(cb.Name);
                        }
                    }
                }
            }
            return sb.ToString();
        }

        private void LoadTechnique(FxcTechnique t)
        {
            if (t == null)
            {
                TechniquePanel.Enabled = false;
                TechniqueTextBox.Text = string.Empty;
            }
            else
            {
                TechniquePanel.Enabled = true;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("technique " + t.Name);
                sb.AppendLine("{");
                if (t.Passes != null)
                {
                    for (int i = 0; i < t.Passes.Length; i++)
                    {
                        var pass = t.Passes[i];
                        sb.AppendLine(" pass p" + i.ToString());
                        sb.AppendLine(" {");

                        var vs = Fxc?.GetVS(pass.VS);
                        var ps = Fxc?.GetPS(pass.PS);
                        var cs = Fxc?.GetCS(pass.CS);
                        var ds = Fxc?.GetDS(pass.DS);
                        var gs = Fxc?.GetGS(pass.GS);
                        var hs = Fxc?.GetHS(pass.HS);

                        if (vs != null) sb.AppendLine("  vertexShader = " + vs.Name + "();");
                        if (ps != null) sb.AppendLine("  pixelShader = " + ps.Name + "();");
                        if (cs != null) sb.AppendLine("  computeShader = " + cs.Name + "();");
                        if (ds != null) sb.AppendLine("  domainShader = " + ds.Name + "();");
                        if (gs != null) sb.AppendLine("  geometryShader = " + gs.Name + "();");
                        if (hs != null) sb.AppendLine("  hullShader = " + hs.Name + "();");

                        sb.AppendLine(" }");
                    }
                }
                sb.AppendLine("}");
                TechniqueTextBox.Text = sb.ToString();
            }
        }


        private void ShadersListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ShadersListView.SelectedItems.Count != 1) return;
            var tag = ShadersListView.SelectedItems[0].Tag;
            if (tag is FxcShader fs) LoadShader(fs);
            else if (tag is AwcShader awcs) LoadAwcShader(awcs);
            else { LoadShader(null); LoadAwcShader((AwcShader)null); }
        }

        private void TechniquesListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (TechniquesListView.SelectedItems.Count != 1) { LoadTechnique(null); return; }
            var tag = TechniquesListView.SelectedItems[0].Tag;
            if (tag is FxcTechnique ft) { LoadTechnique(ft); }
            else if (tag is AwcEffectTechnique at) { LoadTechniqueSummary(at); }
            else { LoadTechnique(null); }
        }

        // ---------- AWC: search / filter ----------

        private void SearchTextBox_TextChanged(object sender, EventArgs e)
        {
            if (AwcShader == null) return;
            if (AwcHasEffects) RebuildEffectsTree();
            else RebuildShadersList();
        }

        // ---------- AWC: export / import ----------

        private void ShaderContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // When the tree is active, derive the current SelectedAwcShader from
            // the tree selection so the menu items reflect the right object.
            if (EffectsTreeView.Visible && EffectsTreeView.SelectedNode != null)
            {
                SelectedAwcShader = EffectsTreeView.SelectedNode.Tag as AwcShader;
            }
            bool hasSelection = AwcShader != null && SelectedAwcShader != null;
            ExportCsoMenuItem.Enabled = hasSelection;
            ImportCsoMenuItem.Enabled = hasSelection && IsEditable;
            ImportCsoMenuItem.ToolTipText = (hasSelection && !IsEditable)
                ? "Enable Edit Mode in RPF Explorer to import shaders."
                : null;
            if (AwcShader == null) e.Cancel = true; // hide menu entirely in FXC mode
        }

        private void ExportCsoMenuItem_Click(object sender, EventArgs e)
        {
            var s = SelectedAwcShader;
            if (s == null) { MessageBox.Show("Select a shader to export."); return; }
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Compiled Shader (*.cso)|*.cso|All files (*.*)|*.*";
                sfd.FileName = SafeFileName(s.StageName + "_" + s.Name) + ".cso";
                if (sfd.ShowDialog() != DialogResult.OK) return;
                File.WriteAllBytes(sfd.FileName, s.Binary ?? Array.Empty<byte>());
                StatusLabel.Text = "Exported " + s.Name + " (" + (s.Binary?.Length ?? 0) + " bytes)";
            }
        }

        private void ImportCsoMenuItem_Click(object sender, EventArgs e)
        {
            if (!IsEditable)
            {
                MessageBox.Show("Enable Edit Mode in RPF Explorer to modify AWC files.",
                    "Edit Mode required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var s = SelectedAwcShader;
            if (s == null) { MessageBox.Show("Select a shader to replace."); return; }
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Compiled Shader (*.cso)|*.cso|All files (*.*)|*.*";
                if (ofd.ShowDialog() != DialogResult.OK) return;
                byte[] bytes = File.ReadAllBytes(ofd.FileName);
                if (bytes.Length < 4)
                {
                    MessageBox.Show("File too small to be a CSO.");
                    return;
                }
                uint magic = BitConverter.ToUInt32(bytes, 0);
                const uint DXBC = 0x43425844;
                const uint DXIL = 0x4C495844;
                if (magic != DXBC && magic != DXIL)
                {
                    var r = MessageBox.Show("File does not start with DXBC/DXIL magic. Import anyway?",
                        "Unrecognised CSO", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r != DialogResult.Yes) return;
                }

                if (!EnsureRootSignature(s, ref bytes, out string rsNote)) return;

                int oldSize = (int)s.Size;
                s.Binary = bytes;
                s.Size = (uint)bytes.Length;
                s.BinaryDirty = true;
                // Keep original metadata block — game may crash if the new CSO's
                // resource layout differs from the original.

                LoadAwcShader(s);
                StatusLabel.Text = "Imported " + s.Name + " (" + oldSize + " -> " + bytes.Length + " bytes)"
                                 + (string.IsNullOrEmpty(rsNote) ? "" : " — " + rsNote);
            }
        }

        /// <summary>
        /// Makes sure the incoming shader keeps a root signature the game can build a PSO
        /// from, transplanting the original's with dxc when it has to. Returns false when
        /// the import must be abandoned; on true, <paramref name="bytes"/> holds what should
        /// actually be written, which is not always what was on disk.
        ///
        /// Every stock Enhanced shader embeds one, so this fires on essentially every import.
        /// Writing a blob without it produces an archive that looks perfectly valid and a
        /// shader the game cannot create a pipeline for.
        /// </summary>
        private bool EnsureRootSignature(AwcShader s, ref byte[] bytes, out string note)
        {
            note = null;
            var check = ShaderRootSignature.Check(s.Binary, bytes);

            if (check.SafeAsIs)
            {
                note = check.Verdict == RootSigVerdict.AlreadyMatches ? "root signature preserved" : null;
                return true;
            }

            if (check.Verdict == RootSigVerdict.Differs || check.Verdict == RootSigVerdict.NotAContainer)
            {
                var r = MessageBox.Show(
                    check.Message + "\n\nImport anyway?",
                    "Root signature mismatch", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return false;
                note = "root signature NOT verified";
                return true;
            }

            // Missing: the original has one, the replacement doesn't. dxc can splice it on
            // and re-sign; an SM6 container is hash-signed, so this cannot be done in
            // managed code and there is no fallback if dxc is absent.
            string dxc = ShaderRootSignature.FindDxc(Properties.Settings.Default.DxcPath);
            if (dxc == null)
            {
                MessageBox.Show(
                    check.Message + "\n\nThe root signature can be copied across automatically, "
                    + "but that needs dxc.exe, which was not found.\n\nPut dxc.exe (with "
                    + "dxcompiler.dll and dxil.dll) in a \"dxcompilers\" folder next to "
                    + "CodeWalker.exe, or set its path in Settings.\n\nThe import has been "
                    + "cancelled — writing this shader as-is would fail pipeline creation in game.",
                    "Root signature missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            Cursor = Cursors.WaitCursor;
            bool ok;
            byte[] patched;
            string msg;
            try { ok = ShaderRootSignature.TryTransplant(s.Binary, bytes, dxc, out patched, out msg); }
            finally { Cursor = Cursors.Default; }

            if (!ok)
            {
                MessageBox.Show(
                    msg + "\n\nThe import has been cancelled and the shader left unchanged. "
                    + "This is the check working: injecting it would most likely crash the game "
                    + "at pipeline creation.",
                    "Root signature incompatible", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            bytes = patched;
            note = "root signature transplanted";
            return true;
        }

        private void ExportAllMenuItem_Click(object sender, EventArgs e)
        {
            if (AwcShader == null) return;
            using (var fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() != DialogResult.OK) return;
                int count = 0;
                foreach (var s in AwcShader.AllShaders())
                {
                    string sub = s.StageName.ToLowerInvariant();
                    string dir = Path.Combine(fbd.SelectedPath, sub);
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, SafeFileName(s.Name) + ".cso");
                    File.WriteAllBytes(path, s.Binary ?? Array.Empty<byte>());
                    count++;
                }
                StatusLabel.Text = "Exported " + count + " shaders to " + fbd.SelectedPath;
            }
        }

        private static string SafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "shader";
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var c in name) sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString();
        }

        // ---------- AWC: save ----------

        private void SaveMenuItem_Click(object sender, EventArgs e)
        {
            if (AwcShader == null) return;
            if (!IsEditable)
            {
                MessageBox.Show("Enable Edit Mode in RPF Explorer to save AWC files back to the archive.",
                    "Edit Mode required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (rpfFileEntry == null)
            {
                SaveAsMenuItem_Click(sender, e);
                return;
            }

            try
            {
                if (!(exploreForm?.EnsureRpfValidEncryption(rpfFileEntry.File) ?? false)) return;

                byte[] data = AwcShader.Save();
                var newentry = RpfFile.CreateFile(rpfFileEntry.Parent, rpfFileEntry.Name, data);
                rpfFileEntry = newentry;
                AwcShader.FileEntry = newentry;

                exploreForm?.RefreshMainListViewInvoke();
                StatusLabel.Text = "Saved " + rpfFileEntry.Name + " (" + data.Length + " bytes)";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save failed: " + ex.Message);
            }
        }

        private void SaveAsMenuItem_Click(object sender, EventArgs e)
        {
            if (AwcShader == null) return;
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "FXDB Shader Library (*.awc)|*.awc|All files (*.*)|*.*";
                sfd.FileName = fileName ?? "shader.awc";
                if (sfd.ShowDialog() != DialogResult.OK) return;
                byte[] data = AwcShader.Save();
                File.WriteAllBytes(sfd.FileName, data);
                StatusLabel.Text = "Saved " + Path.GetFileName(sfd.FileName) + " (" + data.Length + " bytes)";
            }
        }
    }
}
