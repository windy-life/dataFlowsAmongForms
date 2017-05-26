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
using DevExpress.Data.Filtering.Helpers;
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

        #region 206变量

        private string _filePath206 = ""; //文件夹路径
        private string _path206 = ""; //文件路径
        private bool _collectFlag206; //采集标志位
        private SerialPort _sp206 = new SerialPort(); //S90产品的串口
        const int Bytes206 = 15; //一帧数据字节数
        private int _dataLeftNum206; //存储未解析完的字节个数
        private byte[] _dataLeft206 = new byte[30]; //存储未解析完的字节 

        #endregion

        #region D60变量

        private string _filePathD60 = ""; //文件夹路径
        private string _pathD60 = ""; //文件路径
        private bool _collectFlagD60; //采集标志位
        private SerialPort _spD60 = new SerialPort(); //S90产品的串口
        const int BytesD60 = 8; //一帧数据字节数
        private int _dataLeftNumD60; //存储未解析完的字节个数
        private byte[] _dataLeftD60 = new byte[30]; //存储未解析完的字节 

        #endregion
        private bool _listening = false; //是否没有执行完invoke相关操作  
        private bool _commClosing = false; //是否正在关闭串口，执行Application.DoEvents，并阻止再次invoke  

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
            sp.ReceivedBytesThreshold = 1;
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
        /// 从两个字节解算出角度数据
        /// </summary>
        /// <param name="angleRange">角度量程:15;30;60</param>
        /// <param name="byteH">高字节</param>
        /// <param name="byteL">低字节</param>
        /// <returns></returns>
        private double GetAngleFromByte(int angleRange, byte byteH, byte byteL)
        {
            double angle= byteH * 256 + byteL;
            if (angle >= 32768) angle -= 65536; 
            switch (angleRange)
            {
                case 15:
                    angle *= 0.0005;
                    break;
                case 30:
                    angle *= 0.001;
                    break;
                case 60:
                    angle *= 0.002;
                    break;
                default:
                    angle = 100;
                    break;
            }
            return angle;
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
                        if (rtbS90.TextLength > 100000)
                        {
                            rtbS90.Clear();
                        }
                        rtbS90.Select(rtbS90.TextLength, 0); //贯标移到最后
                        rtbS90.ScrollToCaret(); //移动滚动条到光标处
                        rtbS90.AppendText(showData); //追加信息
                        tbxAngleS90.Text = angle;
                        tbxTempreatureS90.Text = tempreature;
                    }));
                }
            }
        }

        /// <summary>
        /// 对D15产品数据进行处理
        /// </summary>
        /// <param name="buffer">串口接收的字节数组</param>
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
                    if (_collectFlagD15) //是否采集
                    {
                        WriteData2TxtD15(_pathD15, angleX, angleY, tempreature, showData);
                    }
                    Invoke(new MethodInvoker(delegate
                    {
                        if (rtbD15.TextLength > 100000)
                        {
                            rtbD15.Clear();
                        }
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
        /// 对206产品数据进行处理
        /// </summary>
        /// <param name="buffer">串口接收的字节数组</param>
        private void Process206(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == 0xAA) //字头正确
                {
                    #region 判断剩余数据是否完全

                    if (i + Bytes206 - 1 >= buffer.Length) //这组数据不完整
                    {
                        //存储剩下的数据和个数
                        _dataLeftNum206 = buffer.Length - i; //个数
                        for (int j = 0; j < _dataLeftNum206; j++)
                        {
                            _dataLeft206[j] = buffer[j + i];
                        }
                        return; //退出
                    }

                    #endregion

                    #region 判断是否符合格式（通过校验）

                    if (buffer[i+1] != 0xAA) //第二个字节正确
                        continue;
                    if (buffer[i + 2] == 0x0D && buffer[i + 3] == 0x02 && buffer[i + 4] == 0x00 && buffer[i + 5] == 0x00 && buffer[i + 6] == 0x00 && buffer[i + 7] == 0x00 && buffer[i + 8] == 0x00)//判断固定字符
                    {
                        byte xorCheck = 0x02^0x0D;//异或校验
                        for (int j = 9; j < 14; j++)
                        {
                            xorCheck ^= buffer[i + j];
                        }
                        if(xorCheck!=buffer[i+14])//校验未通过
                            continue;
                    }
                    else
                    {
                        continue;
                    }
                    #endregion
                    #region 解析数据

                    double angleX = GetAngleFromByte(30, buffer[i + 9], buffer[i + 10]);//X轴
                    double angleY = GetAngleFromByte(30, buffer[i + 11], buffer[i + 12]);//Y轴
                    double tempreature = buffer[i + 13];//温度
                    StringBuilder showData = new StringBuilder();
                    showData.Clear();
                    for (int j = i; j < i+15; j++)//获取完整帧字符串
                    {
                        showData.Append(buffer[j].ToString("X").PadLeft(2,'0').PadLeft(3, ' '));
                    }
                    #endregion

                    if (_collectFlag206) //是否采集
                    {
                        WriteData2Txt206(_path206, angleX.ToString("0.000"), angleY.ToString("0.000"), tempreature.ToString(), showData.ToString());
                    }
                    Invoke(new MethodInvoker(delegate
                    {
                        if (rtb206.TextLength > 100000)
                        {
                            rtb206.Clear();
                        }
                        rtb206.Select(rtb206.TextLength, 0); //贯标移到最后
                        rtb206.ScrollToCaret(); //移动滚动条到光标处
                        rtb206.AppendText(showData.ToString()); //追加信息
                        tbxXAngle206.Text = angleX.ToString("0.000");
                        tbxYAngle206.Text = angleY.ToString("0.000");
                        tbxTempreature206.Text = tempreature.ToString();
                    }));
                }
            }
        }
        /// <summary>
        /// 对D60产品数据进行处理
        /// </summary>
        /// <param name="buffer">串口接收的字节数组</param>
        private void ProcessD60(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == 0xAA) //字头正确
                {
                    #region 判断剩余数据是否完全

                    if (i + BytesD60 - 1 >= buffer.Length) //这组数据不完整
                    {
                        //存储剩下的数据和个数
                        _dataLeftNumD60 = buffer.Length - i; //个数
                        for (int j = 0; j < _dataLeftNumD60; j++)
                        {
                            _dataLeftD60[j] = buffer[j + i];
                        }
                        return; //退出
                    }

                    #endregion

                    #region 判断是否符合格式（通过校验）

                    byte sumCheck = 0;//和校验
                    for (int j = i+1; j < i+7; j++)
                    {
                        sumCheck += buffer[j];
                    }
                    if (sumCheck != buffer[i+7])//校验未通过
                        continue;
                    #endregion
                    #region 解析数据

                    double angleX = GetAngleFromByte(60, buffer[i + 1], buffer[i + 2]);//X轴
                    double angleY = GetAngleFromByte(60, buffer[i + 3], buffer[i + 4]);//Y轴
                    double compassAngle = GetAngleFromByte(60, buffer[i + 5], buffer[i + 6]);//罗盘
                    StringBuilder showData = new StringBuilder();
                    showData.Clear();
                    for (int j = i; j < i + BytesD60; j++)//获取完整帧字符串
                    {
                        showData.Append(buffer[j].ToString("X").PadLeft(2, '0').PadLeft(3, ' '));
                    }
                    #endregion

                    if (_collectFlagD60) //是否采集
                    {
                        WriteData2TxtD60(_pathD60, angleX.ToString("0.000"), angleY.ToString("0.000"), compassAngle.ToString("0.0"), showData.ToString());
                    }
                    Invoke(new MethodInvoker(delegate
                    {
                        if (rtbD60.TextLength > 100000)
                        {
                            rtbD60.Clear();
                        }
                        rtbD60.Select(rtbD60.TextLength, 0); //贯标移到最后
                        rtbD60.ScrollToCaret(); //移动滚动条到光标处
                        rtbD60.AppendText(showData.ToString()); //追加信息
                        tbxXAngleD60.Text = angleX.ToString("0.000");
                        tbxYAngleD60.Text = angleY.ToString("0.000");
                        tbxCompassAngleD60.Text = compassAngle.ToString();
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
        /// 将D15数据写入txt文件
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

        /// <summary>
        /// 将206数据写入txt文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="angleX">X轴角度</param>
        /// /// <param name="angleY">Y轴角度</param>
        /// <param name="tempreature">温度</param>
        /// <param name="originalData">原始数据</param>
        private void WriteData2Txt206(string path, string angleX, string angleY, string tempreature, string originalData)
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

        /// <summary>
        /// 将D60数据写入txt文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="angleX">X轴角度</param>
        /// /// <param name="angleY">Y轴角度</param>
        /// <param name="compassAngle">罗盘方位角</param>
        /// <param name="originalData">原始数据</param>
        private void WriteData2TxtD60(string path, string angleX, string angleY, string compassAngle, string originalData)
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
                fileWrite.WriteLine(string.Format(@"{0}   {1}    {2}    {3}    {4}", DateTime.Now, originalData, angleX, angleY, compassAngle));
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
        /// 刷新206串口号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRefresh206_Click(object sender, EventArgs e)
        {
            RefreshPorts(cbxCommNum206);
        }

        /// <summary>
        /// 刷新D60串口号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRefreshD60_Click(object sender, EventArgs e)
        {
            RefreshPorts(cbxCommNumD60);
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
                    if (_spS90.IsOpen) //成功打开
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
                    _commClosing = true; //置位标志位：我要关闭串口了
                    while (_listening) Application.DoEvents(); //等待接收函数处理完毕
                    //打开时点击，则关闭串口  
                    _spS90.Close(); //关闭串口
                    _commClosing = false; //置位标志位：已经关闭

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
                    _commClosing = true; //置位标志位：我要关闭串口了
                    while (_listening) Application.DoEvents(); //等待接收函数处理完毕
                    //打开时点击，则关闭串口  
                    _spD15.Close(); //关闭串口
                    _commClosing = false; //置位标志位：已经关闭

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
        /// 打开/关闭206串口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenComm206_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_sp206.IsOpen) //串口未打开
                {
                    SetCommPortsParam(cbxCommNum206.Text.Trim(), Convert.ToInt32(cbxBaud206.Text.Trim()), _sp206);
                    //配置串口参数
                    _sp206.DataReceived += _sp206_DataReceived;
                    _sp206.Open(); //打开
                    if (_sp206.IsOpen == true) //成功打开
                    {
                        btnOpenComm206.Text = "关闭串口";
                        cbxCommNum206.Enabled = false;
                        cbxBaud206.Enabled = false;
                        btnRefresh206.Enabled = false;
                    }
                    else
                    {
                        MessageBox.Show("打开串口失败！");
                    }
                }
                else //串口已打开
                {
                    _commClosing = true; //置位标志位：我要关闭串口了
                    while (_listening) Application.DoEvents(); //等待接收函数处理完毕
                    //打开时点击，则关闭串口  
                    _sp206.Close(); //关闭串口
                    _commClosing = false; //置位标志位：已经关闭

                    if (_sp206.IsOpen == false)
                    {
                        btnOpenComm206.Text = "打开串口";
                        cbxCommNum206.Enabled = true;
                        cbxBaud206.Enabled = true;
                        btnRefresh206.Enabled = true;
                    }
                }
            }
            catch (Exception a)
            {
                MessageBox.Show(a.Message);
            }
        }

        /// <summary>
        /// 打开/关闭D60串口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenCommD60_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_spD60.IsOpen) //串口未打开
                {
                    SetCommPortsParam(cbxCommNumD60.Text.Trim(), Convert.ToInt32(cbxBaudD60.Text.Trim()), _spD60);
                    //配置串口参数
                    _spD60.DataReceived += _spD60_DataReceived;
                    _spD60.Open(); //打开
                    if (_spD60.IsOpen == true) //成功打开
                    {
                        btnOpenCommD60.Text = "关闭串口";
                        cbxCommNumD60.Enabled = false;
                        cbxBaudD60.Enabled = false;
                        btnRefreshD60.Enabled = false;
                    }
                    else
                    {
                        MessageBox.Show("打开串口失败！");
                    }
                }
                else //串口已打开
                {
                    _commClosing = true; //置位标志位：我要关闭串口了
                    while (_listening) Application.DoEvents(); //等待接收函数处理完毕
                    //打开时点击，则关闭串口  
                    _spD60.Close(); //关闭串口
                    _commClosing = false; //置位标志位：已经关闭

                    if (_spD60.IsOpen == false)
                    {
                        btnOpenCommD60.Text = "打开串口";
                        cbxCommNumD60.Enabled = true;
                        cbxBaudD60.Enabled = true;
                        btnRefreshD60.Enabled = true;
                    }
                }
            }
            catch (Exception a)
            {
                MessageBox.Show(a.Message);
            }
        }

        /// <summary>
        /// 串口D15接收数据函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _spD15_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_commClosing)
                return;
            try
            {
                _listening = true; //设置标记，说明我已经开始处理数据，一会儿要使用系统UI的。
                byte[] buffer = GetCommData(_spD15, _dataLeftD15, _dataLeftNumD15); //获取字节数组
                ProcessD15(buffer);
            }
            finally
            {
                _listening = false; //我用完了，ui可以关闭串口了。
            }
        }

        /// <summary>
        /// 串口S90接收数据函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _spS90_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_commClosing)
                return;
            try
            {
                _listening = true; //设置标记，说明我已经开始处理数据，一会儿要使用系统UI的。
                byte[] buffer = GetCommData(_spS90, _dataLeftS90, _dataLeftNumS90); //获取字节数组
                ProcessS90(buffer);
            }
            finally
            {
                _listening = false; //我用完了，ui可以关闭串口了。
            }
        }

        /// <summary>
        /// 串口206接收数据函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _sp206_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_commClosing)
                return;
            try
            {
                _listening = true; //设置标记，说明我已经开始处理数据，一会儿要使用系统UI的。
                byte[] buffer = GetCommData(_sp206, _dataLeft206, _dataLeftNum206); //获取字节数组
                Process206(buffer);
            }
            finally
            {
                _listening = false; //我用完了，ui可以关闭串口了。
            }
        }

        /// <summary>
        /// 串口D60接收数据函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _spD60_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_commClosing)
                return;
            try
            {
                _listening = true; //设置标记，说明我已经开始处理数据，一会儿要使用系统UI的。
                byte[] buffer = GetCommData(_spD60, _dataLeftD60, _dataLeftNumD60); //获取字节数组
                ProcessD60(buffer);
            }
            finally
            {
                _listening = false; //我用完了，ui可以关闭串口了。
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

        /// <summary>
        /// 选择206存储文件路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSelectPath206_Click(object sender, EventArgs e)
        {
            using (fbd = new FolderBrowserDialog())
            {
                if (_filePath206 != string.Empty) //不是第一次选择路径，赋值之前的路径
                {
                    fbd.SelectedPath = _filePath206;
                }
                if (fbd.ShowDialog() == DialogResult.OK) //选择了一个路径
                {
                    _filePath206 = fbd.SelectedPath; //获取路径
                    tbxFilePath206.Text = _filePath206; //显示路径
                    _path206 = _filePath206 + "\\" + tbxFileName206.Text.Trim() + ".txt"; //全路径
                }
            }
        }

        /// <summary>
        /// 选择D60存储文件路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSelectPathD60_Click(object sender, EventArgs e)
        {
            using (fbd = new FolderBrowserDialog())
            {
                if (_filePathD60 != string.Empty) //不是第一次选择路径，赋值之前的路径
                {
                    fbd.SelectedPath = _filePathD60;
                }
                if (fbd.ShowDialog() == DialogResult.OK) //选择了一个路径
                {
                    _filePathD60 = fbd.SelectedPath; //获取路径
                    tbxFilePathD60.Text = _filePathD60; //显示路径
                    _pathD60 = _filePathD60 + "\\" + tbxFileNameD60.Text.Trim() + ".txt"; //全路径
                }
            }
        }

        /// <summary>
        /// S90文件名变化，更新路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbxFileNameS90_TextChanged(object sender, EventArgs e)
        {
            _pathS90 = _filePathS90 + "\\" + tbxFileNameS90.Text.Trim() + ".txt"; //全路径
        }

        /// <summary>
        /// D15文件名变化，更新路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbxFileNameD15_TextChanged(object sender, EventArgs e)
        {
            _pathD15 = _filePathD15 + "\\" + tbxFileNameD15.Text.Trim() + ".txt"; //全路径
        }

        /// <summary>
        /// 206文件名变化，更新路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbxFileName206_TextChanged(object sender, EventArgs e)
        {
            _path206 = _filePath206 + "\\" + tbxFileName206.Text.Trim() + ".txt"; //全路径
        }

        /// <summary>
        /// D60文件名变化，更新路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbxFileNameD60_TextChanged(object sender, EventArgs e)
        {
            _pathD60 = _filePathD60 + "\\" + tbxFileNameD60.Text.Trim() + ".txt"; //全路径
        }

        /// <summary>
        /// S90采集
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCollectS90_Click(object sender, EventArgs e)
        {
            if (btnCollectS90.Text == "开始采集")
            {
                if (_pathS90 == string.Empty || _filePathS90 == string.Empty)
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

        /// <summary>
        /// D15采集
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCollectD15_Click(object sender, EventArgs e)
        {
            if (btnCollectD15.Text == "开始采集")
            {
                if (_pathD15 == string.Empty || _filePathD15 == string.Empty)
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

        /// <summary>
        /// 206采集
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCollect206_Click(object sender, EventArgs e)
        {
            if (btnCollect206.Text == "开始采集")
            {
                if (_path206 == string.Empty || _filePath206 == string.Empty)
                {
                    MessageBox.Show("请选择文件存储路径!");
                    return;
                }
                tbxFileName206.Enabled = false;
                btnSelectPath206.Enabled = false;
                _collectFlag206 = true; //开始采集
                btnCollect206.Text = "停止采集";
            }
            else
            {
                tbxFileName206.Enabled = true;
                btnSelectPath206.Enabled = true;
                _collectFlag206 = false; //停止采集
                btnCollect206.Text = "开始采集";
            }
        }
        /// <summary>
        /// D60采集
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCollectD60_Click(object sender, EventArgs e)
        {
            if (btnCollectD60.Text == "开始采集")
            {
                if (_pathD60 == string.Empty || _filePathD60 == string.Empty)
                {
                    MessageBox.Show("请选择文件存储路径!");
                    return;
                }
                tbxFileNameD60.Enabled = false;
                btnSelectPathD60.Enabled = false;
                _collectFlagD60 = true; //开始采集
                btnCollectD60.Text = "停止采集";
            }
            else
            {
                tbxFileNameD60.Enabled = true;
                btnSelectPathD60.Enabled = true;
                _collectFlagD60 = false; //停止采集
                btnCollectD60.Text = "开始采集";
            }
        }

        private void tileBar_Click(object sender, EventArgs e)
        {

        }
    }
}