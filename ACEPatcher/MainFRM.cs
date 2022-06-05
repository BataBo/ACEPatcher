using ACEPatcher.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using dnlib.DotNet;
using System.IO;
using ACEPatcher.ModuleLoader;
using System.Resources;
using System.Globalization;
using System.Collections;

namespace ACEPatcher
{
    public partial class MainFRM : Form
    {
        ModuleDefMD AssemblyToPatch;
        ModuleDefMD[] Dependancies;
        string AssemblyPath;

        private void PrepareMenuStrip() 
        {
            menuStrip1.Renderer = new DarkMenuRenderer();
            foreach (ToolStripMenuItem menuItem in menuStrip1.Items)
            {
                ((ToolStripDropDownMenu)menuItem.DropDown).ShowImageMargin = false;
            }
            
        }

        private void PrepareTreeListView() 
        {
            ResourceManager resourceManager = Icons.ResourceManager;
            ResourceSet resourceSet = resourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true);
            TreeViewLoader.Images = new ImageList();
            TreeViewLoader.ResourceMap = new Dictionary<string, int>();
            foreach(DictionaryEntry resource in resourceSet) 
            {
                if (resource.Value.GetType() != typeof(Icon))
                {
                    TreeViewLoader.ResourceMap.Add(resource.Key.ToString().Replace("_16x", ""), TreeViewLoader.Images.Images.Count);
                    TreeViewLoader.Images.Images.Add((Bitmap)resource.Value);
                }
            }
            treeView1.ImageList = TreeViewLoader.Images;
        }

        public MainFRM()
        {
            InitializeComponent();
            PrepareMenuStrip();
            PrepareTreeListView();
            this.Icon = Icons.patch;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (File.Exists(files[0])) 
            {
                string[] failed;
                AssemblyToPatch = DotNetModuleLoader.LoadModule(files[0],out Dependancies,out failed);
                if(AssemblyToPatch == null) 
                {
                    MessageBox.Show("Invalid Assembly");
                    return;
                } 
                AssemblyPath = files[0];
                TreeViewLoader.FillTreeView(AssemblyToPatch,Dependancies,treeView1,true);
                if (failed.Length > 0)
                {
                    MessageBox.Show("Failed to load: \r\n" + string.Join("\r\n", failed) + "\r\nSome references might be missing", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else 
            {
                MessageBox.Show("Invalid file");
            }
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog() 
            {
                Title = "Load assembly",
                Filter = "Executable|*.exe|Dynamic Link Library|*.dll",
                CheckFileExists = true
            };
            if(dialog.ShowDialog() == DialogResult.OK) 
            {
                string[] failed;
                AssemblyToPatch = DotNetModuleLoader.LoadModule(dialog.FileName,out Dependancies,out failed);
                if (AssemblyToPatch == null)
                {
                    MessageBox.Show("Invalid Assembly");
                    return;
                }
                AssemblyPath = dialog.FileName;
                TreeViewLoader.FillTreeView(AssemblyToPatch,Dependancies, treeView1,true);
                if (failed.Length > 0) 
                {
                    MessageBox.Show("Failed to load: \r\n" + string.Join("\r\n", failed) + "\r\nSome references might be missing", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if(e.Node.Tag != null && e.Node.Tag.GetType().GetInterfaces().Contains(typeof(IMethodDefOrRef))) 
            {
                Patcher.AddPatchToList((IMethod)e.Node.Tag);
                string[] vs = new string[Patcher.patchList.Count];
                for(int i = 0; i < Patcher.patchList.Count; i++) 
                {
                    vs[i] = Patcher.patchList.ElementAt(i).Key.DeclaringType.Name + "." + Patcher.patchList.ElementAt(i).Key.Name + "->" + Patcher.patchList.ElementAt(i).Value.DeclaringType.Name + "." + Patcher.patchList.ElementAt(i).Value.Name;
                }
                richTextBox1.Lines = vs;
            } 
        }

        

        private void button1_Click(object sender, EventArgs e)
        {
            if(richTextBox1.Text.Length == 0) 
                MessageBox.Show("No patches made", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                Patcher.ApplyPatches(AssemblyPath);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Environment.Exit(0);
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(Patcher.patchAssembly == null || Patcher.patchList.Count == 0) 
            {
                MessageBox.Show("No patches to export", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            SaveFileDialog dialog = new SaveFileDialog()
            {
                Title = "Export patch...",
                Filter = "AcePatcher Patch(*.btb)|*.btb",
            };
            if(dialog.ShowDialog() == DialogResult.OK) 
            {
                ConfigManager.ExportConfig(AssemblyToPatch,dialog.FileName,false);
                MessageBox.Show("File successfully exported", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void secureExportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Patcher.patchAssembly == null || Patcher.patchList.Count == 0)
            {
                MessageBox.Show("No patches to export", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            SaveFileDialog dialog = new SaveFileDialog()
            {
                Title = "Export patch",
                Filter = "AcePatcher Patch(*.btb)|*.btb",
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string password = "";
                if(InputBox("Password","Type in password:",ref password) == DialogResult.OK)
                ConfigManager.ExportConfig(AssemblyToPatch, dialog.FileName, true,password);
                MessageBox.Show("File successfully exported", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public static DialogResult InputBox(string title, string promptText, ref string value)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }

        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(AssemblyToPatch != null) 
            {
                OpenFileDialog dialog = new OpenFileDialog()
                {
                    Title = "Import Patch",
                    Filter = "AcePatcher Patch(*.btb)|*.btb",
                    CheckFileExists = true
                };
                if(dialog.ShowDialog() == DialogResult.OK) 
                {
                    ConfigManager.ImportConfig(Dependancies,dialog.FileName);
                    string[] vs = new string[Patcher.patchList.Count];
                    for (int i = 0; i < Patcher.patchList.Count; i++)
                    {
                        vs[i] = Patcher.patchList.ElementAt(i).Key.DeclaringType.Name + "." + Patcher.patchList.ElementAt(i).Key.Name + "->" + Patcher.patchList.ElementAt(i).Value.DeclaringType.Name + "." + Patcher.patchList.ElementAt(i).Value.Name;
                    }
                    richTextBox1.Lines = vs;
                }
            }
            else 
            {
                MessageBox.Show("Please load the assembly first", "Information", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }
    }
}
