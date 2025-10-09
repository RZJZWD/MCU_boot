using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using MCUBoot.DateModels;
using MCUBoot.Services;
using System.Security.AccessControl;
using System.Threading.Tasks;
namespace MCUBoot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //服务实例
        private SerialPortService _serialPortService;
        private DataProcessService _dataProcessService;
        //配置实例
        private SerialPortConfig _SerialPortConfig;
        private DisplayConfig _DisplayConfig;
        private FrameConfig _FrameConfig;
        //UI状态
        private StringBuilder _receivedTextBuilder;
        public MainWindow()
        {
            // 在程序启动时注册GB2312编码，只需一次
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            InitConfig();
            InitializeComponent();
            InitServices();
            InitEventHandlers();

            //加载一次串口列表，用于串口连接后开启上位机的情况
            LoadAvailablePorts();

        }


        /// <summary>
        /// 初始化所有服务
        /// </summary>
        private void InitServices()
        {
            _serialPortService = new SerialPortService();
            _dataProcessService = new DataProcessService();

            _receivedTextBuilder = new StringBuilder();
        }

        /// <summary>
        /// 初始化配置参数
        /// </summary>
        private void InitConfig()
        {
            _SerialPortConfig = new SerialPortConfig();
            _DisplayConfig = new DisplayConfig();
            _FrameConfig = new FrameConfig();
            
        }

        /// <summary>
        /// 设置事件处理器
        /// </summary>
        private void InitEventHandlers()
        {
            //串口服务事件
            _serialPortService.DataReceived += OnDataReceived;
            _serialPortService.ConnectionStateChanged += OnConnectionStateChanged;
            _serialPortService.AvailablePortsChanged += OnAvailablePortsChanged;
            _serialPortService.ErrorOccurred += OnSerialError;

            //数据处理服务事件
            _dataProcessService.ReceivedDaraProcessed += OnDataProcessed;
            _dataProcessService.ReceivedFrameProcessed += OnReceivedFrameProcessed;

            //自动发送服务事件

        }



        #region 事件处理方法

        //串口事件处理方法
        /// <summary>
        /// 处理可用串口列表发生变化
        /// </summary>
        private void OnAvailablePortsChanged(object sender, string[] ports)
        {
            // Use BeginInvoke to avoid blocking the caller thread
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                LoadAvailablePortsUI(ports);
            }));
        }
        /// <summary>
        /// 处理连接状态变化事件
        /// </summary>
        private void OnConnectionStateChanged(object sender, bool isConnected)
        {
            // Use BeginInvoke to avoid blocking the caller thread
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                UpdateConnectionUI(isConnected);
            }));
        }
        /// <summary>
        /// 串口数据接收处理
        /// </summary>
        private void OnDataReceived(object sender, DataReceivedEventArgs e)
        {
            // Use BeginInvoke so the serial port thread is not blocked waiting for UI thread
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                _dataProcessService.ProcessReceivedData(e.Data,_DisplayConfig);
            }));
        }
        /// <summary>
        /// 处理串口错误事件
        /// </summary>
        /// <remarks>This method is invoked on the UI thread to ensure that the error message is displayed
        /// in a message box.</remarks>
        /// <param name="sender">The source of the event. Typically the serial port instance that encountered the error.</param>
        /// <param name="errorMessage">A string containing the error message to display. Cannot be null.</param>
        private void OnSerialError(object sender, string errorMessage)
        {
            // Use BeginInvoke to avoid blocking caller thread
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                MessageBox.Show(errorMessage,"串口错误", MessageBoxButton.OK,MessageBoxImage.Error);
            }));
        }

        //数据处理事件处理方法
        /// <summary>
        /// 无帧处理数据处理完成事件
        /// </summary>
        private void OnDataProcessed(object sendr,string processedText)
        {
            AppendToReceivedText(processedText);
        }
        /// <summary>
        /// 有帧处理数据处理完成事件
        /// </summary>
        private void OnReceivedFrameProcessed(object sendr, ReceivedFrameProcessedEventArgs e)
        {
            //AppendToReceivedText(e.DisplayText);
            string frameText = $"[帧数据] {e.DisplayText}\r\n";
            AppendToReceivedText(frameText);
        }

        #endregion


        #region UI控制
        /// <summary>
        /// 加载可用串口列表
        /// </summary>
        private void LoadAvailablePortsUI(string[] ports)
        {
            cmbPortName.Items.Clear();
            foreach (string port in ports)
            {
                cmbPortName.Items.Add(port);
            }
            if (cmbPortName.Items.Count > 0)
            {
                cmbPortName.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 连接状态变化事件处理
        /// </summary>
        private void UpdateConnectionUI(bool isConnected)
        {
            BtnCOMSwitch.Content = isConnected ? "关闭串口" : "开启串口";
            COMSwitchLight.Fill = isConnected ? Brushes.Green : Brushes.Red;

            // 更新控件状态
            cmbPortName.IsEnabled = !isConnected;
            cmbBaudRate.IsEnabled = !isConnected;
            cmbDateBits.IsEnabled = !isConnected;
            cmbStopBits.IsEnabled = !isConnected;
            cmbParity.IsEnabled = !isConnected;
        }   

        /// <summary>
        /// 串口开关按钮点击事件
        /// </summary>
        private void BtnCOMSwitch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_serialPortService.IsOpen)
                {
                    //更新串口配置
                    UpdateSerialConfigFromUI();
                    //打开串口
                    _serialPortService.Open(_SerialPortConfig);
                }
                else
                {
                    //关闭串口
                    _serialPortService.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"串口打开失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 追加文本到接收区
        /// </summary>
        /// <param name="text">文本</param>
        private void AppendToReceivedText(string text)
        {
            // 移除首尾的空白字符
            text = text.Trim();
            // 如果文本不为空，则添加为新的一行
            if (!string.IsNullOrEmpty(text))
            {
                if (_DisplayConfig.PauseShowReceived) return;

                // 在现有内容后添加新行
                if (_receivedTextBuilder.Length > 0)
                {
                    _receivedTextBuilder.AppendLine(); // 添加换行符
                }

                _receivedTextBuilder.Append(text);

                comReceived.Text = _receivedTextBuilder.ToString();
                
                
                //自动滚动
                if (_DisplayConfig.AutoScroll)
                {
                    comReceived.ScrollToEnd();
                }
            }
        }

        private void BtnClearReceivedArea_Click(object sender, RoutedEventArgs e)
        {
            _receivedTextBuilder.Clear();
            comReceived.Text = "";
            _dataProcessService.ClearFrameBuffer();
        }

        private void BtnPauseShowReceived_Click(object sender, RoutedEventArgs e)
        {

        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            SendData();
        }

        private void BtnClearSendArea_Click(object sender, RoutedEventArgs e)
        {
            comSend.Text = "";
        }

        private void receDeCode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_DisplayConfig == null) return;
            _DisplayConfig.ReceiveEncoding = GetReceiveEncodingTypeFromUI();
        }

        private void sendEnCode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_DisplayConfig == null) return;
            _DisplayConfig.SendEncoding = GetSendEncodingTypeFromUI();

        }
        
        private void chkRecvAutoWrap_Checked(object sender, RoutedEventArgs e)
        {

        }
        private void chkAutoScroll_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void chkShowSend_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void chkRecvShowRowNum_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void chkRecvShowTxRx_Checked(object sender, RoutedEventArgs e)
        {

        }
        #endregion

        #region 从前端获取数据

        /// <summary>
        /// 从UI更新串口配置
        /// </summary>
        private void UpdateSerialConfigFromUI()
        {
            //获取com名
            _SerialPortConfig.PortName = cmbPortName.SelectedItem?.ToString();
            //获取波特率
            _SerialPortConfig.BaudRate = int.Parse((cmbBaudRate.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString());
            _SerialPortConfig.DataBits = int.Parse((cmbDateBits.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString());
            _SerialPortConfig.StopBits = (cmbStopBits.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString();
            _SerialPortConfig.Parity = (cmbParity.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString();
            _SerialPortConfig.EnableDTR = chkEnableDTR.IsChecked ?? false;
        }

        /// <summary>
        /// 获取发送编码类型
        /// </summary>
        /// <returns></returns>
        private EncodingType GetSendEncodingTypeFromUI()
        {
            return sendEnCode.SelectedIndex switch
            {
               0 => EncodingType.ASCII,
               1 => EncodingType.UTF8,
               2 => EncodingType.GB2312,
               3 => EncodingType.Hex,
               4 => EncodingType.Decimal,
               5 => EncodingType.Octal,
               6 => EncodingType.Binary,
               _=>EncodingType.UTF8
            };
        }

        /// <summary>
        /// 获取接收解码格式
        /// </summary>
        /// <returns></returns>
        private EncodingType GetReceiveEncodingTypeFromUI()
        {
            return receDeCode.SelectedIndex switch
            {
                0 => EncodingType.ASCII,
                1 => EncodingType.UTF8,
                2 => EncodingType.GB2312,
                3 => EncodingType.Hex,
                4 => EncodingType.Decimal,
                5 => EncodingType.Octal,
                6 => EncodingType.Binary,
                _ => EncodingType.UTF8
            };
        }

        #endregion


        #region 辅助工具
        private async void SendData()
        {
            if (!_serialPortService.IsOpen)
            {
                MessageBox.Show("请先打开串口", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string sendText = comSend.Text;
                if (string.IsNullOrEmpty(sendText)) return;

                //解码数据，将其变为字节流并添加帧头/帧尾
                byte[] data = _dataProcessService.ProcessSendData(sendText, _DisplayConfig);

                // 在后台线程执行串口写操作，避免 UI 卡死
                await Task.Run(() => _serialPortService.SendData(data));

                //显示发送
                if (_DisplayConfig.ShowSend)
                {
                    //发送区显示TX/RX
                    string displayText = _dataProcessService.FormatWithTxRxPrefix(sendText, _DisplayConfig, false);
                    // 使用 BeginInvoke 保证在 UI 线程更新
                    Dispatcher.BeginInvoke(new System.Action(() => AppendToReceivedText(displayText)));
                }
                    
            }
            catch (Exception ex)
            {
                // 在 UI 线程显示错误
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    MessageBox.Show($"发送失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }));
            }
        }

        /// <summary>
        /// 加载可用串口列表
        /// </summary>
        private void LoadAvailablePorts()
        {
            cmbPortName.Items.Clear();
            foreach (string port in System.IO.Ports.SerialPort.GetPortNames())
            {
                cmbPortName.Items.Add(port);
            }
            if (cmbPortName.Items.Count > 0)
                cmbPortName.SelectedIndex = 0;
        }

        #endregion


        /// <summary>
        /// 窗口关闭时清理资源
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _serialPortService?.Dispose();
            //_autoSendService?.Stop();
            base.OnClosed(e);
        }

        
    }
}