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
    public partial class frmDataSource : Form
    {
        public frmDataSource()
        {
            InitializeComponent();
            GetFrmList = new List<Interface>(); //接口方法
        }

        /// <summary>
        /// 接口方法定义的属性
        /// </summary>
        public List<Interface> GetFrmList { get; set; }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            #region 接口方法

            foreach (var a in GetFrmList) //遍历属性中的所有窗体
            {
                a.GetMessage(textBox1.Text);
            }

            #endregion
        }
    }
}