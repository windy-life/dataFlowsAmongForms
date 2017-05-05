using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MethodEvent
{
    public partial class frmDataSource : Form
    {
        public frmDataSource()
        {
            InitializeComponent();
        }

        public delegate void GetMessage(string str);//定义委托
        public event GetMessage GetMsg;

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            GetMsg(textBox1.Text);
        }
    }
}
