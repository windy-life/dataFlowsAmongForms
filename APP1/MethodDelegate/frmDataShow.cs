using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MethodDelegate
{
    public partial class frmDataShow : Form
    {
        public frmDataShow()
        {
            InitializeComponent();
        }

        public void GetMsg(string str)
        {
            textBox1.Text = str;
        }
    }
}
