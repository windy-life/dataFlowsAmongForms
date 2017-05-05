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
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            frmDataSource frmDSource = new frmDataSource();
            frmDSource.Show();
            frmDataShow frmDShow = new frmDataShow();
            frmDSource.GetMsg += frmDShow.GetMEvent;//注册事件
            frmDShow.Show();
        }
    }
}
