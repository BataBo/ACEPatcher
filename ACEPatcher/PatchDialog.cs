using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ACEPatcher
{
    public partial class PatchDialog : Form
    {
        public IMethod SelectedMethod;

        public PatchDialog()
        {
            InitializeComponent();
            treeView1.ImageList = TreeViewLoader.Images;
            TreeViewLoader.FillTreeView(Patcher.patchAssembly,null, treeView1);
            this.Icon = Icons.patch;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if(treeView1.SelectedNode == null) 
            {
                MessageBox.Show("Please select a patch method", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else 
            {
                if (treeView1.SelectedNode.Tag.GetType().GetInterfaces().Contains(typeof(IMethodDefOrRef))) 
                {
                    SelectedMethod = (IMethod)treeView1.SelectedNode.Tag;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else 
                {
                    MessageBox.Show("Please select a valid method", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
