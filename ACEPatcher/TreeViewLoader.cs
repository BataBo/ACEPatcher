using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using System.Windows.Forms;
using System.Drawing;

namespace ACEPatcher
{
    class TreeViewLoader
    {
        public static Dictionary<string, int> ResourceMap;
        public static ImageList Images;
        private static TreeNode[] ListNamespaces(ModuleDef module)
        {
            List<TreeNode> namespaces = new List<TreeNode>();
            namespaces.Add(new TreeNode("", ResourceMap["Namespace"], ResourceMap["Namespace"]) { ForeColor = Color.FromArgb(255, 215, 15) });
            foreach (TypeDef def in module.Types)
            {
                string ns = def.Namespace.String;
                if (namespaces.Where(x => x.Text == ns).Count() == 0)
                    namespaces.Add(new TreeNode(ns,ResourceMap["Namespace"], ResourceMap["Namespace"]) { ForeColor = Color.FromArgb(255, 215, 15) });
            }
            return namespaces.ToArray();
        }

        public static void FillTreeView(ModuleDefMD module,ModuleDefMD[] dependancies,TreeView treeView,bool AddDependancies = false) 
        {
            AddThisAssemblyToTree(module, treeView);
            if (AddDependancies)
            {
                foreach(ModuleDefMD dependancy in dependancies) 
                {
                    AddThisAssemblyToTree(dependancy, treeView);
                }
            }
        }


        private static void AddThisAssemblyToTree(ModuleDefMD module, TreeView treeView) 
        {
            TreeNode root = new TreeNode(module.Assembly.Name, ResourceMap["Assembly"], ResourceMap["Assembly"]) { ForeColor = Color.FromArgb(219, 179, 126), Tag = module.Assembly };
            foreach (ModuleDef AssemblyModule in module.Assembly.Modules)
            {
                TreeNode moduleNode = new TreeNode(AssemblyModule.Name, ResourceMap["Module"], ResourceMap["Module"]) { ForeColor = Color.FromArgb(177, 127, 216), Tag = AssemblyModule };
                TreeNode[] namespaces = ListNamespaces(AssemblyModule);
                namespaces = namespaces.OrderBy(x => x.Text).ToArray();
                List<TypeDef> types = AssemblyModule.Types.ToList();
                types.Sort((x, y) => { return x.Name.CompareTo(y.Name); });
                foreach (TypeDef type in types)
                {
                    ProcessType(namespaces.Where(x => x.Text == type.Namespace.String).First(), type);
                }
                namespaces[0].Name = "-";
                foreach (TreeNode ns in namespaces)
                    moduleNode.Nodes.Add(ns);
                root.Nodes.Add(moduleNode);
            }
            treeView.Nodes.Add(root);
        }

        private static void ProcessType(TreeNode ParentNode,TypeDef type) 
        {
            if (type.HasGenericParameters || type.IsInterface || type.IsEnum || type.IsDelegate)
                return;
            TreeNode node = new TreeNode(type.Name) {Tag = type };
            if (type.IsNestedPrivate)
            {
                node.ImageIndex = ResourceMap["ClassPrivate"];
                node.SelectedImageIndex = ResourceMap["ClassPrivate"];
            }
            else if (type.IsNestedFamily) 
            {
                node.ImageIndex = ResourceMap["ClassProtected"];
                node.SelectedImageIndex = ResourceMap["ClassProtected"];
            }
            else if (type.Visibility == TypeAttributes.NotPublic || type.IsNestedAssembly || type.IsNestedFamilyAndAssembly || type.IsNestedFamilyOrAssembly) 
            {
                node.ImageIndex = ResourceMap["ClassFriend"];
                node.SelectedImageIndex = ResourceMap["ClassFriend"];
            }
            else 
            {
                node.ImageIndex = ResourceMap["Class"];
                node.SelectedImageIndex = ResourceMap["Class"];
            }
            if (type.IsAbstract)
                node.ForeColor = Color.FromArgb(55, 141, 108);
            else
                node.ForeColor = Color.FromArgb(78,201,176);
            
            foreach(MethodDef m in type.FindConstructors()) 
            {
                ProccessMethod(node, m);
            }
            List<MethodDef> methods = type.Methods.ToList();
            methods.Sort((x,y) => { return FormatMethod(x).CompareTo(FormatMethod(y)); });
            foreach(MethodDef method in methods) 
            {
                if(!(method.IsGetter || method.IsSetter) && !method.IsConstructor)
                ProccessMethod(node, method);
            }
            List<PropertyDef> properties = type.Properties.ToList();
            properties.Sort((x, y) => { return x.Name.CompareTo(y.Name); });
            foreach (PropertyDef property in properties)
            {
                ProccessProperty(node, property);
            }
            List<TypeDef> NestedTypes = type.NestedTypes.ToList();
            NestedTypes.Sort((x, y) => { return x.Name.CompareTo(y.Name); });
            foreach (TypeDef NestedType in NestedTypes)
            {
                ProcessType(node, NestedType);
            }
            ParentNode.Nodes.Add(node);
        }
        
        private static void ProccessProperty(TreeNode ParentNode,PropertyDef property) 
        {
            TreeNode node = new TreeNode(property.Name, ResourceMap["Property"], ResourceMap["Property"]) {Tag = property };
            node.ForeColor = Color.FromArgb(36,143,143);
            if (property.GetMethod != null)
                ProccessMethod(node, property.GetMethod);
            if(property.SetMethod != null)
                ProccessMethod(node, property.SetMethod);
            ParentNode.Nodes.Add(node);
        }

        private static void ProccessMethod(TreeNode ParentNode,MethodDef method) 
        {
            if (method.IsAbstract || method.HasGenericParameters)
                return;
            TreeNode node = new TreeNode(FormatMethod(method)) {Tag = method };
            if (method.IsInternalCall) 
            {
                node.ImageIndex = ResourceMap["MethodFriend"];
                node.SelectedImageIndex = ResourceMap["MethodFriend"];
            }
            else if (method.IsPrivate) 
            {
                node.ImageIndex = ResourceMap["MethodPrivate"];
                node.SelectedImageIndex = ResourceMap["MethodPrivate"];
            }
            else if (method.IsPublic) 
            {
                node.ImageIndex = ResourceMap["Method"];
                node.SelectedImageIndex = ResourceMap["Method"];
            }
            if (method.IsConstructor) 
            {
                node.ForeColor = ParentNode.ForeColor;
            }
            else 
            {
                node.ForeColor = Color.FromArgb(230,155,0);
            }
            ParentNode.Nodes.Add(node);
        }

        private static string FormatMethod(MethodDef method)
        {
            string name = (method.IsInstanceConstructor ? method.DeclaringType.Name : method.Name) + "(";
            List<string> parameters = new List<string>();
            List<Parameter> parameters1 = method.Parameters.ToList();
            if (method.HasThis) 
            {
                parameters1.RemoveAt(0);
            }
            foreach(Parameter param in parameters1) 
            {
                parameters.Add(param.Type.TypeName);
            }
            name += string.Join(",", parameters) + ") : " + method.ReturnType.TypeName;
            return name;
        }

        private static string FormatMethod(MemberRef method)
        {
            string name = (method.Name == ".ctor" ? method.DeclaringType.Name : method.Name) + "(";
            List<string> parameters = new List<string>();
            foreach (TypeSig param in method.MethodSig.GetParams())
            {
                parameters.Add(param.TypeName);
            }
            name += string.Join(",", parameters) + ") : " + method.ReturnType.TypeName;
            return name;
        }
    }
}
