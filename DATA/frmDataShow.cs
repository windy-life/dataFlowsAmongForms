using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DATA
{
    public partial class frmDataShow : Form,Interface
    {
        public frmDataShow()
        {
            InitializeComponent();
        }

        public void GetMessage(string str)
        {
            textBox1.Text = str;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
