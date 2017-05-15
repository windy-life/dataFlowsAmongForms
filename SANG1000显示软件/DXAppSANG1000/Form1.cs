using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace DXAppSANG1000
{
    public partial class Form1 : DevExpress.XtraEditors.XtraForm
    {
        #region 定义变量

        private FolderBrowserDialog fbd;

        #region S90变量

        private string _filePathS90 = ""; //文件夹路径
        private string _pathS90 = ""; //文件路径
        private bool _collectFlagS90; //采集标志位
        private SerialPort _spS90 = new SerialPort(); //S90产品的串口
        const int BytesS90 = 11; //一帧数据字节数
        private int _dataLeftNumS90; //存储未解析完的字节个数
        private byte[] _dataLeftS90 = new byte[30]; //存储未解析完的字节 

        #endregion

        #region D15变量

        private string _filePathD15 = ""; //文件夹路径
        private string _pathD15 = ""; //文件路径
        private bool _collectFlagD15; //采集标志位
        private SerialPort _spD15 = new SerialPort(); //S90产品的串口
        const int BytesD15 = 17; //一帧数据字节数
        private int _dataLeftNumD15; //存储未解析完的字节个数
        private byte[] _dataLeftD15 = new byte[30]; //存储未解析完的字节 

        #endregion

        private bool Listening = false; //是否没有执行完invoke相关操作  
        private bool Closing = false; //是否正在关闭串口，执行Application.DoEvents，并阻止再次invoke  

        #endregion

        #region 定义功能函数

        /// <summary>
        /// 刷新串口号，获取当前可用的串口号
        /// </summary>
        /// <param name="cbx">要填充串口号的Combobox</param>
        private void RefreshPorts(System.Windows.Forms.ComboBox cbx)
        {
            cbx.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            foreach (string p in ports)
            {
                SerialPort tempSp = new SerialPort(p);
                try
                {
                    tempSp.Open();
                    cbx.Items.Add(p);
                    tempSp.Close();
                }
                catch (Exception)
                {
                }
            }
            if (cbx.Items.Count > 0)
                cbx.SelectedIndex = 0; //显示第一个串口号
        }

        /// <summary>
        /// 设置串口相关参数
        /// </summary>
        /// <param name="commNum">串口号</param>
        /// <param name="baud">波特率</param>
        /// <param name="sp">要配置的串口</param>
        private void SetCommPortsParam(string commNum, int baud, SerialPort sp)
        {
            sp.PortName = commNum;
            sp.BaudRate = baud;
            sp.DataBits = 8;
            sp.StopBits = (StopBits) (1);
            sp.Parity = Parity.None;
            sp.RtsEnable = true;
            sp.DtrEnable = true;
            sp.ReadTimeout = -1;
            sp.ReceivedBytesThreshold = 35;
            sp.WriteTimeout = -1;
        }

        /// <summary>
        /// 获取串口接收的字节数组
        /// </summary>
        /// <param name="sp">接收数据的串口</param>
        /// <param name="savedData">存储上次未处理完的数据的数组</param>
        /// <param name="dataNum">上次未处理完的数据个数</param>
        /// <returns>返回字节数组</returns>
        private byte[] GetCommData(SerialPort sp, byte[] savedData, int dataNum)
        {
            try
            {
                int bytes = sp.BytesToRead; //获取缓存中的字节数
                byte[] buffer = new byte[bytes]; //新建存储字节数组
                sp.Read(buffer, 0, bytes); //接收缓存中的数据
                byte[] newBuffer = new byte[bytes + dataNum]; //新建一个数组存储上次未解析完的数据和这次新来的数据
                for (int i = 0; i < dataNum; i++) //赋值上次的数据
                {
                    newBuffer[i] = savedData[i];
                }
                for (int i = dataNum; i < bytes + dataNum; i++) //赋值新的数据
                {
                    newBuffer[i] = buffer[i - dataNum];
                }
                return newBuffer;
            }
            catch (Exception a)
            {
                MessageBox.Show(a.Message);
                return null;
            }
        }

        /// <summary>
        /// 对S90产品数据进行处理
        /// </summary>
        /// <param name="buffer">串口接收的字节数组</param>
        private void ProcessS90(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == 36) //字头正确
                {
                    #region 判断剩余数据是否完全

                    if (i + BytesS90 - 1 >= buffer.Length) //这组数据不完整
                    {
                        //存储剩下的数据和个数
                        _dataLeftNumS90 = buffer.Length - i; //个数
                        for (int j = 0; j < _dataLeftNumS90; j++)
                        {
                            _dataLeftS90[j] = buffer[j + i];
                        }
                        return; //退出
                    }

                    #endregion

                    byte[] bytesToShow = new byte[BytesS90]; //存储一帧数据
                    for (int j = i; j < i + BytesS90; j++)
                    {
                        bytesToShow[j - i] = buffer[j];
                    }
                    string showData = Encoding.ASCII.GetString(bytesToShow); //转换为ASCII码字符串
                    char[] dataStr = showData.ToCharArray();
                    string angle = dataStr[1].ToString() + dataStr[2].ToString() + dataStr[3].ToString() + "." +
                                   dataStr[4].ToString() +
                                   dataStr[5].ToString(); //获取角度
                    string tempreature = dataStr[6].ToString() + dataStr[7].ToString() + dataStr[8].ToString() + "." +
                                         dataStr[9].ToString();
                    //获取温度
                    if (_collectFlagS90) //是否采集
                    {
                        WriteData2TxtS90(_pathS90, angle, tempreature, showData);
                    }
                    Invoke(new MethodInvoker(delegate
                    {
                        rtbS90.Select(rtbS90.TextLength, 0); //贯标移到最后
                        rtbS90.ScrollToCaret(); //移动滚动条到光标处
                        rtbS90.AppendText(showData); //追加信息
                        tbxAngleS90.Text = angle;
                        tbxTempreatureS90.Text = tempreature;
                    }));
                }
            }
        }

        private void ProcessD15(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == 36) //字头正确
                {
                    #region 判断剩余数据是否完全

                    if (i + BytesD15 - 1 >= buffer.Length) //这组数据不完整
                    {
                        //存储剩下的数据和个数
                        _dataLeftNumD15 = buffer.Length - i; //个数
                        for (int j = 0; j < _dataLeftNumD15; j++)
                        {
                            _dataLeftD15[j] = buffer[j + i];
                        }
                        return; //退出
                    }

                    #endregion

                    byte[] bytesToShow = new byte[BytesD15]; //存储一帧数据
                    for (int j = i; j < i + BytesD15; j++)
                    {
                        bytesToShow[j - i] = buffer[j];
                    }
                    string showData = Encoding.ASCII.GetString(bytesToShow); //转换为ASCII码字符串
                    char[] dataStr = showData.ToCharArray();
                    string angleX = dataStr[3].ToString() + dataStr[4].ToString() + dataStr[5].ToString() + "." +
                                    dataStr[6].ToString() + dataStr[7].ToString(); //获取X轴角度
                    string angleY = dataStr[8].ToString() + dataStr[9].ToString() + dataStr[10].ToString() + "." +
                                    dataStr[11].ToString() + dataStr[12].ToString(); //获取Y轴角度
                    string tempreature = dataStr[13].ToString() + dataStr[14].ToString() + dataStr[15].ToString() + "." +
                                         dataStr[16].ToString();
                    //获取温度
                    if (_collectFlagS90) //是否采集
                    {
                        WriteData2TxtD15(_pathS90, angleX, angleY, tempreature, showData);
                    }
                    Invoke(new MethodInvoker(delegate
                    {
                        rtbD15.Select(rtbD15.TextLength, 0); //贯标移到最后
                        rtbD15.ScrollToCaret(); //移动滚动条到光标处
                        rtbD15.AppendText(showData); //追加信息
                        tbxXAngleD15.Text = angleX;
                        tbxYAngleD15.Text = angleY;
                        tbxTempreatureD15.Text = tempreature;
                    }));
                }
            }
        }

        /// <summary>
        /// 将S90数据写入txt文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="angle">角度</param>
        /// <param name="tempreature">温度</param>
        /// <param name="originalData">原始数据</param>
        private void WriteData2TxtS90(string path, string angle, string tempreature, string originalData)
        {
            if (!File.Exists(path)) //文件不存在则创建一个
            {
                FileStream file_WR = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
                //创建文件 
                file_WR.Close();
            }
            using (FileStream fileW = new FileStream(path, FileMode.Append, FileAccess.Write))
                //写入数据
            {
                StreamWriter fileWrite = new StreamWriter(fileW);
                fileWrite.WriteLine(string.Format(@"{0}   {1}    {2}    {3}", DateTime.Now, originalData, angle,
                    tempreature));
                fileWrite.Close();
            }
        }

        /// <summary>
        /// 将S90数据写入txt文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="angleX">X轴角度</param>
        /// /// <param name="angleY">Y轴角度</param>
        /// <param name="tempreature">温度</param>
        /// <param name="originalData">原始数据</param>
        private void WriteData2TxtD15(string path, string angleX, string angleY, string tempreature, string originalData)
        {
            if (!File.Exists(path)) //文件不存在则创建一个
            {
                FileStream file_WR = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
                //创建文件 
                file_WR.Close();
            }
            using (FileStream fileW = new FileStream(path, FileMode.Append, FileAccess.Write))
                //写入数据
            {
                StreamWriter fileWrite = new StreamWriter(fileW);
                fileWrite.WriteLine(string.Format(@"{0}   {1}    {2}    {3}    {4}", DateTime.Now, originalData, angleX,
                    angleY, tempreature));
                fileWrite.Close();
            }
        }

        #endregion

        public Form1()
        {
            InitializeComponent();
            btnRefreshS90_Click(null, null); //刷新S90串口号
            btnRefreshD15_Click(null, null); //刷新D15串口号
        }

        private void tileBar_SelectedItemChanged(object sender, TileItemEventArgs e)
        {
            navigationFrame.SelectedPageIndex = tileBarGroupTables.Items.IndexOf(e.Item);
        }

        /// <summary>
        /// 刷新S90串口号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRefreshS90_Click(object sender, EventArgs e)
        {
            RefreshPorts(cbxCommNumS90);
        }

        /// <summary>
        /// 刷新D15串口号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRefreshD15_Click(object sender, EventArgs e)
        {
            RefreshPorts(cbxCommNumD15);
        }

        /// <summary>
        /// 打开/关闭S90串口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenCommS90_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_spS90.IsOpen) //串口未打开
                {
                    SetCommPortsParam(cbxCommNumS90.Text.Trim(), Convert.ToInt32(cbxBaudS90.Text.Trim()), _spS90);
                    //配置串口参数
                    _spS90.DataReceived += _spS90_DataReceived;
                    //_spS90_DataReceived;
                    _spS90.Open(); //打开
                    if (_spS90.IsOpen == true) //成功打开
                    {
                        btnOpenCommS90.Text = "关闭串口";
                        cbxCommNumS90.Enabled = false;
                        cbxBaudS90.Enabled = false;
                        btnRefreshS90.Enabled = false;
                    }
                    else
                    {
                        MessageBox.Show("打开串口失败！");
                    }
                }
                else //串口已打开
                {
                    Closing = true; //置位标志位：我要关闭串口了
                    while (Listening) Application.DoEvents(); //等待接收函数处理完毕
                    //打开时点击，则关闭串口  
                    _spS90.Close(); //关闭串口
                    Closing = false; //置位标志位：已经关闭

                    if (_spS90.IsOpen == false)
                    {
                        btnOpenCommS90.Text = "打开串口";
                        cbxCommNumS90.Enabled = true;
                        cbxBaudS90.Enabled = true;
                        btnRefreshS90.Enabled = true;
                    }
                }
            }
            catch (Exception a)
            {
                MessageBox.Show(a.Message);
            }
        }

        /// <summary>
        /// 打开/关闭D15串口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenCommD15_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_spD15.IsOpen) //串口未打开
                {
                    SetCommPortsParam(cbxCommNumD15.Text.Trim(), Convert.ToInt32(cbxBaudD15.Text.Trim()), _spD15);
                    //配置串口参数
                    _spD15.DataReceived += _spD15_DataReceived;
                    _spD15.Open(); //打开
                    if (_spD15.IsOpen == true) //成功打开
                    {
                        btnOpenCommD15.Text = "关闭串口";
                        cbxCommNumD15.Enabled = false;
                        cbxBaudD15.Enabled = false;
                        btnRefreshD15.Enabled = false;
                    }
                    else
                    {
                        MessageBox.Show("打开串口失败！");
                    }
                }
                else //串口已打开
                {
                    Closing = true; //置位标志位：我要关闭串口了
                    while (Listening) Application.DoEvents(); //等待接收函数处理完毕
                    //打开时点击，则关闭串口  
                    _spD15.Close(); //关闭串口
                    Closing = false; //置位标志位：已经关闭

                    if (_spD15.IsOpen == false)
                    {
                        btnOpenCommD15.Text = "打开串口";
                        cbxCommNumD15.Enabled = true;
                        cbxBaudD15.Enabled = true;
                        btnRefreshD15.Enabled = true;
                    }
                }
            }
            catch (Exception a)
            {
                MessageBox.Show(a.Message);
            }
        }

        /// <summary>
        /// 串口S90接收数据函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _spD15_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (Closing)
                return;
            try
            {
                Listening = true; //设置标记，说明我已经开始处理数据，一会儿要使用系统UI的。
                byte[] buffer = GetCommData(_spS90, _dataLeftS90, _dataLeftNumS90); //获取字节数组
                ProcessD15(buffer);
            }
            finally
            {
                Listening = false; //我用完了，ui可以关闭串口了。
            }
        }

        /// <summary>
        /// 串口S90接收数据函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _spS90_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (Closing)
                return;
            try
            {
                Listening = true; //设置标记，说明我已经开始处理数据，一会儿要使用系统UI的。
                byte[] buffer = GetCommData(_spS90, _dataLeftS90, _dataLeftNumS90); //获取字节数组
                ProcessS90(buffer);
            }
            finally
            {
                Listening = false; //我用完了，ui可以关闭串口了。
            }
        }

        /// <summary>
        /// 选择S90存储文件路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSelectPathS90_Click(object sender, EventArgs e)
        {
            using (fbd = new FolderBrowserDialog())
            {
                if (_filePathS90 != string.Empty) //不是第一次选择路径，赋值之前的路径
                {
                    fbd.SelectedPath = _filePathS90;
                }
                if (fbd.ShowDialog() == DialogResult.OK) //选择了一个路径
                {
                    _filePathS90 = fbd.SelectedPath; //获取路径
                    tbxFilePathS90.Text = _filePathS90; //显示路径
                    _pathS90 = _filePathS90 + "\\" + tbxFileNameS90.Text.Trim() + ".txt"; //全路径
                }
            }
        }

        /// <summary>
        /// 选择D15存储文件路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSelectPathD15_Click(object sender, EventArgs e)
        {
            using (fbd = new FolderBrowserDialog())
            {
                if (_filePathD15 != string.Empty) //不是第一次选择路径，赋值之前的路径
                {
                    fbd.SelectedPath = _filePathD15;
                }
                if (fbd.ShowDialog() == DialogResult.OK) //选择了一个路径
                {
                    _filePathD15 = fbd.SelectedPath; //获取路径
                    tbxFilePathD15.Text = _filePathD15; //显示路径
                    _pathD15 = _filePathD15 + "\\" + tbxFileNameD15.Text.Trim() + ".txt"; //全路径
                }
            }
        }

        private void tbxFileNameS90_TextChanged(object sender, EventArgs e)
        {
            _pathS90 = _filePathS90 + "\\" + tbxFileNameS90.Text.Trim() + ".txt"; //全路径
        }

        private void tbxFileNameD15_TextChanged(object sender, EventArgs e)
        {
            _pathD15 = _filePathD15 + "\\" + tbxFileNameD15.Text.Trim() + ".txt"; //全路径
        }

        private void btnCollectS90_Click(object sender, EventArgs e)
        {
            if (btnCollectS90.Text == "开始采集")
            {
                if (_pathS90 == string.Empty)
                {
                    MessageBox.Show("请选择文件存储路径!");
                    return;
                }
                tbxFileNameS90.Enabled = false;
                btnSelectPathS90.Enabled = false;
                _collectFlagS90 = true; //开始采集
                btnCollectS90.Text = "停止采集";
            }
            else
            {
                tbxFileNameS90.Enabled = true;
                btnSelectPathS90.Enabled = true;
                _collectFlagS90 = false; //停止采集
                btnCollectS90.Text = "开始采集";
            }
        }

        private void btnCollectD15_Click(object sender, EventArgs e)
        {
            if (btnCollectD15.Text == "开始采集")
            {
                if (_pathD15 == string.Empty)
                {
                    MessageBox.Show("请选择文件存储路径!");
                    return;
                }
                tbxFileNameD15.Enabled = false;
                btnSelectPathD15.Enabled = false;
                _collectFlagD15 = true; //开始采集
                btnCollectD15.Text = "停止采集";
            }
            else
            {
                tbxFileNameD15.Enabled = true;
                btnSelectPathD15.Enabled = true;
                _collectFlagD15 = false; //停止采集
                btnCollectD15.Text = "开始采集";
            }
        }
    }
}