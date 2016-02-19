﻿using System;
using Sm4shCommand.Classes;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using static Sm4shCommand.Runtime;
using Sm4shCommand.Nodes;

namespace Sm4shCommand
{
    public partial class AcmdMain : Form
    {
        public AcmdMain()
        {
            InitializeComponent();
            _manager = new WorkspaceManager();
        }

        internal WorkspaceManager Manager => _manager ?? new WorkspaceManager();
        private readonly WorkspaceManager _manager;

        public void OpenWorkspace(string wrkspce)
        {
            Manager.ReadWRKSPC(wrkspce);
            List<TreeNode> col = new List<TreeNode>();
            FileTree.BeginUpdate();
            foreach (Project p in Manager.Projects)
            {
                _curFighter = Manager.OpenFighter(p.ACMDPath);
                _curFighter.AnimationHashPairs = Manager.getAnimNames(p.AnimationFile);

                string name = $"{p.ProjectName} - [{(p.ProjectType == ProjType.Fighter ? "Fighter" : "Weapon")}]";

                TreeNode pNode = new TreeNode(name);

                TreeNode Actions = new TreeNode("MSCSB (ActionScript)");
                TreeNode ACMD = new TreeNode("ACMD (AnimCmd)");
                TreeNode Weapons = new TreeNode("Weapons");
                TreeNode Parameters = new TreeNode("Parameters");


                foreach (uint u in _curFighter.MotionTable)
                {
                    if (u == 0)
                        continue;

                    CommandListGroup g = new CommandListGroup(_curFighter, u) { ToolTipText = $"[{u:X8}]" };

                    if (AnimHashPairs.ContainsKey(u))
                        g.Text = AnimHashPairs[u];

                    ACMD.Nodes.Add(g);
                }

                pNode.Nodes.AddRange(new[] { Actions, ACMD, Weapons, Parameters });
                col.Add(pNode);
            }
            FileTree.Nodes.AddRange(col.ToArray());
            Runtime.isRoot = true;
            FileTree.EndUpdate();
        }

        private void ACMDMain_Load(object sender, EventArgs e)
        {

            if (File.Exists(Application.StartupPath + "/Events.cfg"))
                GetCommandInfo(Application.StartupPath + "/Events.cfg");
            else
                MessageBox.Show("Could not load Events.cfg");

            if (!String.IsNullOrEmpty(Manager.WorkspaceRoot))
                OpenWorkspace(Manager.WorkspaceRoot);
        }
        private void ACMDMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveCommandInfo(Application.StartupPath + "/Events.cfg");
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isRoot && _curFighter == null)
                return;
            else if (_curFile == null)
                return;

            for (int i = 0; i < tabControl1.TabCount; i++)
            {
                TabPage p = tabControl1.TabPages[i];
                TabControl tmp = (TabControl)p.Controls[0].Controls[0];
                for (int x = 0; x < tmp.TabCount; x++)
                {
                    ITSCodeBox box = (ITSCodeBox)tmp.TabPages[x].Controls[0];

                    if (box.CommandList.Empty)
                        continue;

                    box.ApplyChanges();

                    if (!isRoot)
                        _curFile.EventLists[box.CommandList.AnimationCRC] = box.CommandList;
                    else
                        _curFighter[(ACMDType)x].EventLists[box.CommandList.AnimationCRC] = box.CommandList;
                }
            }

            if (isRoot)
            {

                _curFighter.Main.Export(rootPath + "/game.bin");
                _curFighter.GFX.Export(rootPath + "/effect.bin");
                _curFighter.SFX.Export(rootPath + "/sound.bin");
                _curFighter.Expression.Export(rootPath + "/expression.bin");
                _curFighter.MotionTable.Export(rootPath + "/Motion.mtable");
            }
            else
                _curFile.Export(FileName);
        }
        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < tabControl1.TabCount; i++)
            {
                TabPage p = tabControl1.TabPages[i];
                TabControl tmp = (TabControl)p.Controls[0].Controls[0];
                for (int x = 0; x < tmp.TabCount; x++)
                {
                    ITSCodeBox box = (ITSCodeBox)tmp.TabPages[x].Controls[0];

                    if (box.CommandList.Empty)
                        continue;

                    box.ApplyChanges();

                    if (!isRoot)
                        _curFile.EventLists[box.CommandList.AnimationCRC] = box.CommandList;
                    else
                        _curFighter[(ACMDType)x].EventLists[box.CommandList.AnimationCRC] = box.CommandList;
                }
            }


            if (isRoot)
            {
                FolderSelectDialog dlg = new FolderSelectDialog();
                DialogResult result = dlg.ShowDialog();
                if (result == DialogResult.OK)
                {
                    _curFighter.Main.Export(dlg.SelectedPath + "/game.bin");
                    _curFighter.GFX.Export(dlg.SelectedPath + "/effect.bin");
                    _curFighter.SFX.Export(dlg.SelectedPath + "/sound.bin");
                    _curFighter.Expression.Export(dlg.SelectedPath + "/expression.bin");
                    _curFighter.MotionTable.Export(dlg.SelectedPath + "/Motion.mtable");
                }
                dlg.Dispose();
            }
            else
            {
                SaveFileDialog dlg = new SaveFileDialog { Filter = "ACMD Binary (*.bin)|*.bin|All Files (*.*)|*.*" };
                DialogResult result = dlg.ShowDialog();
                if (result == DialogResult.OK)
                    _curFile.Export(dlg.FileName);
                dlg.Dispose();
            }
        }

        private void workspaceToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            DialogResult result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                FileName = rootPath = String.Empty;
                _curFile = null;
                FileTree.Nodes.Clear();
                tabControl1.TabPages.Clear();
                OpenWorkspace(dlg.FileName);
            }
            dlg.Dispose();
        }
        private void fileToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "ACMD Binary (*.bin)|*.bin| All Files (*.*)|*.*";
                DialogResult result = dlg.ShowDialog();
                if (result == DialogResult.OK)
                {
                    FileName = rootPath = string.Empty;
                    tabControl1.TabPages.Clear();
                    FileTree.Nodes.Clear();
                    isRoot = false;

                    if ((_curFile = Manager.OpenFile(dlg.FileName)) == null)
                        return;

                    FileTree.BeginUpdate();
                    TreeNode root = new TreeNode("AnimCmd");
                    for (int i = 0; i < _curFile.EventLists.Count; i++)
                    {
                        CommandList cml = _curFile.EventLists.Values[i];
                        root.Nodes.Add(new CommandListNode($"{i:X}-[{cml.AnimationCRC:X8}]", cml));
                    }
                    FileTree.Nodes.Add(root);
                    FileTree.EndUpdate();

                    cboMode.Enabled = true;
                    cboMode.SelectedIndex = (int)Runtime.WorkingEndian;

                    FileName = dlg.FileName;
                    Text = $"Main Form - {FileName}";
                }
            }
        }
        private void eventLibraryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EventLibrary dlg = new EventLibrary();
            dlg.Show();
        }
        private void dumpAsTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog { Filter = "Plain Text (.txt) | *.txt" };
            DialogResult result = dlg.ShowDialog();
            if (result != DialogResult.OK) return;
            using (StreamWriter writer = new StreamWriter(dlg.FileName, false, Encoding.UTF8))
            {
                if (isRoot)
                    writer.Write(_curFighter.Serialize());
                else
                    writer.Write(_curFile.Serialize());
            }
        }
        private void tabControl1_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index > tabControl1.TabPages.Count - 1)
                return;

            if (e.Index == tabControl1.SelectedIndex)
                e.Graphics.FillRectangle(Brushes.ForestGreen, e.Bounds);
            else
                e.Graphics.FillRectangle(SystemBrushes.ActiveBorder, e.Bounds);

            e.Graphics.FillEllipse(new SolidBrush(Color.IndianRed), e.Bounds.Right - 18, e.Bounds.Top + 3, e.Graphics.MeasureString("x", Font).Width + 4, Font.Height);
            e.Graphics.DrawEllipse(Pens.Black, e.Bounds.Right - 18, e.Bounds.Top + 3, e.Graphics.MeasureString("X", Font).Width + 3, Font.Height);
            e.Graphics.DrawString("X", new Font(e.Font, FontStyle.Bold), Brushes.Black, e.Bounds.Right - 17, e.Bounds.Top + 3);
            e.Graphics.DrawString(tabControl1.TabPages[e.Index].Text, e.Font, Brushes.Black, e.Bounds.Left + 17, e.Bounds.Top + 3);
            e.DrawFocusRectangle();
        }
        private void tabControl1_MouseClick(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < tabControl1.TabCount; i++)
            {
                TabPage p = tabControl1.TabPages[i];

                TabControl tmp = (TabControl)p.Controls[0].Controls[0];

                Rectangle r = tabControl1.GetTabRect(i);
                Rectangle closeButton = new Rectangle(r.Right - 18, r.Top + 3, 12, 10);
                if (closeButton.Contains(e.Location))
                {
                    for (int x = 0; x < tmp.TabCount; x++)
                    {
                        ITSCodeBox box = (ITSCodeBox)tmp.TabPages[x].Controls[0];
                        if (box.CommandList.Empty)
                            continue;

                        box.ApplyChanges();

                        if (!isRoot)
                            _curFile.EventLists[box.CommandList.AnimationCRC] = box.CommandList;
                        else
                            _curFighter[(ACMDType)x].EventLists[box.CommandList.AnimationCRC] = box.CommandList;

                    }
                    tabControl1.TabPages.Remove(p);
                    return;
                }
            }
        }
        private void parseAnimations(string path)
        {
            TreeView tree = FileTree;
            AnimHashPairs = Manager.getAnimNames(path);
            if (isRoot)
                _curFighter.AnimationHashPairs = AnimHashPairs;
            else
                _curFile.AnimationHashPairs = AnimHashPairs;

            tree.BeginUpdate();
            foreach (TreeNode n in tree.Nodes)
                if (n.Text == "AnimCmd")
                    for (int i = 0; i < n.Nodes.Count; i++)
                        if (n.Nodes[i] is CommandListNode | n.Nodes[i] is CommandListGroup)
                        {
                            var node = ((BaseNode)n.Nodes[i]);
                            string str = "";
                            AnimHashPairs.TryGetValue(node.CRC, out str);
                            if (string.IsNullOrEmpty(str))
                                str = node.Name;
                            n.Nodes[i].Text = str;
                        }
            tree.EndUpdate();
        }
        private void FileTree_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!(FileTree.SelectedNode is CommandListGroup || FileTree.SelectedNode is CommandListNode)) return;

            BaseNode node = (BaseNode)FileTree.SelectedNode;

            if (tabControl1.TabPages.ContainsKey(node.Name + node.Index))
                tabControl1.SelectTab(node.Name + node.Index);
            else
            {
                var p = new TabPage($"{node.Name}") { Name = node.Name + node.Index };
                if (node is CommandListGroup)
                    p.Controls.Add(new CodeEditControl((CommandListGroup)node) { Dock = DockStyle.Fill });
                else if (node is CommandListNode)
                    p.Controls.Add(new CodeEditControl((CommandListNode)node) { Dock = DockStyle.Fill });
                tabControl1.TabPages.Insert(0, p);
                tabControl1.SelectedIndex = 0;
            }
        }

        private void fighterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FolderSelectDialog dlg = new FolderSelectDialog();

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                FileName = rootPath = String.Empty;
                _curFile = null;
                FileTree.Nodes.Clear();
                tabControl1.TabPages.Clear();
                isRoot = true;

                FileTree.BeginUpdate();
                _curFighter = _manager.OpenFighter(dlg.SelectedPath);
                TreeNode nScript = new TreeNode("AnimCmd");
                for (int i = 0; i < _curFighter.MotionTable.Count; i++)
                {
                    uint CRC = _curFighter.MotionTable[i];
                    nScript.Nodes.Add(new CommandListGroup(_curFighter, CRC) { Text = $"{i:X}-[{CRC:X8}]" });
                }
                TreeNode nParams = new TreeNode("Params");
                TreeNode nMscsb = new TreeNode("MSCSB");
                FileTree.Nodes.AddRange(new TreeNode[] { nScript, nParams, nMscsb });
                FileTree.EndUpdate();

                cboMode.SelectedIndex = (int)Runtime.WorkingEndian;
                cboMode.Enabled = true;
                Runtime.isRoot = true;
                Runtime.rootPath = dlg.SelectedPath;
                Runtime.Instance.Text = String.Format("Main Form - {0}", dlg.SelectedPath);
            }
        }
        private void newWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WorkspaceWizard dlg = new WorkspaceWizard();
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _manager.NewWorkspace(dlg.WorkspaceName, dlg.SourceDirectory, dlg.DestinationDirectory);

                FileName = rootPath = String.Empty;
                _curFile = null;
                FileTree.Nodes.Clear();
                tabControl1.TabPages.Clear();
                isRoot = true;

                OpenWorkspace(dlg.DestinationDirectory + Path.DirectorySeparatorChar + dlg.WorkspaceName +
                                Path.DirectorySeparatorChar + dlg.WorkspaceName + ".WRKSPC");

            }
        }

        private void parseAnimationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                    parseAnimations(dlg.FileName);
            }
        }

        private void cboMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            Runtime.WorkingEndian = (Endianness)cboMode.SelectedIndex;
        }
    }

    public enum Endianness
    {
        Big = 0,
        Little = 1
    }
}