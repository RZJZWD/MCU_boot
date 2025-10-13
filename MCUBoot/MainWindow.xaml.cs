using MCUBoot.DateModels;
using MCUBoot.Services;
using System.CodeDom;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Security.AccessControl;
using System.Security.Policy;
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
        private AutoSendService _autoSendService;
        private FileService _fileService;

        //配置实例
        private SerialPortConfig _SerialPortConfig;
        private DisplayConfig _DisplayConfig;
        private FrameConfig _FrameConfig;
        private AutoSendConfig _AutoSendConfig;
        private FileConfig _FileConfig;
        //UI状态
        private StringBuilder _receivedTextBuilder;
        public MainWindow()
        {
            // 在程序启动时注册GB2312编码，只需一次
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            
            InitializeComponent();
            InitConfig();
            InitUI();
            InitServices();
            InitEventHandlers();

            //加载一次串口列表，用于串口连接后开启上位机的情况
            LoadAvailablePorts();
            //设置帧配置
            SetFrameConfig(_FrameConfig);
            SetAutoSendConfig(_AutoSendConfig);
            SetFileConfig(_FileConfig);
        }


        /// <summary>
        /// 初始化所有服务
        /// </summary>
        private void InitServices()
        {
            _serialPortService = new SerialPortService();
            _dataProcessService = new DataProcessService();
            _autoSendService = new AutoSendService();
            _fileService = new FileService();

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
            _AutoSendConfig = new AutoSendConfig();
            _FileConfig = new FileConfig();
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
            _autoSendService.AutoSendTriggered += OnAutoSendTriggered;
            _autoSendService.ErrorOccurred += OnAutoSendErrorProcessed;
        }
        /// <summary>
        /// 根据各服务的配置更新UI
        /// </summary>
        private void InitUI()
        {
            chkRecvAutoWrap.IsChecked = _DisplayConfig.AutoWrap;
            chkAutoScroll.IsChecked = _DisplayConfig.AutoWrap;
            chkShowSend.IsChecked = _DisplayConfig.ShowSend;
            chkEnableDTR.IsChecked = _SerialPortConfig.EnableDTR;
            _DisplayConfig.LineEnding = GetLineEndingFromUI();
        }


        #region 事件处理方法

        //串口事件处理方法
        /// <summary>
        /// 处理可用串口列表发生变化
        /// </summary>
        private void OnAvailablePortsChanged(object sender, string[] ports)
        {
            Dispatcher.Invoke(() =>
            {
                LoadAvailablePortsUI(ports);
            });
        }
        /// <summary>
        /// 处理连接状态变化事件
        /// </summary>
        private void OnConnectionStateChanged(object sender, bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateConnectionUI(isConnected);
            });
        }
        /// <summary>
        /// 串口数据接收处理
        /// </summary>
        private void OnDataReceived(object sender, MCUBoot.Services.DataReceivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _dataProcessService.ProcessReceivedData(e.Data,_DisplayConfig);
            });
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
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(errorMessage,"串口错误", MessageBoxButton.OK,MessageBoxImage.Error);
            });
        }

        //数据处理事件处理方法
        /// <summary>
        /// 无帧处理数据处理完成事件
        /// </summary>
        private void OnDataProcessed(object sendr,string processedText)
        {
            if (!_DisplayConfig.PauseShowReceived)
            {
                AppendToReceivedText(processedText);
            }
            
        }
        /// <summary>
        /// 有帧处理数据处理完成事件
        /// </summary>
        private void OnReceivedFrameProcessed(object sendr, ReceivedFrameProcessedEventArgs e)
        {
            //AppendToReceivedText(e.DisplayText);
            if (!_DisplayConfig.PauseShowReceived)
            {
                string frameText = $"[帧数据] {e.DisplayText}\r\n";
                AppendToReceivedText(frameText);
            }
        }


        private void OnAutoSendTriggered(object sender,EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(SendData);
            }
            catch (Exception ex)
            {
                RestoreAutoSend(1000);
                MessageBox.Show($"自动发送错误 {ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Error);     
            }

        }

        private void OnAutoSendErrorProcessed(object sender, string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(errorMessage, "自动发送错误", MessageBoxButton.OK, MessageBoxImage.Error);
            });
            RestoreAutoSend(1000);
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
        /// 更新暂停显示按钮
        /// </summary>
        /// <param name="isPaused">暂停显示</param>
        private void UpdatePauseShowRecvUI(bool isPaused)
        {
            BtnPauseShowReceived.Content = isPaused ? "继续显示" : "暂停显示";
            BtnPauseShowReceived.Background = isPaused ? Brushes.Orange : Brushes.LightGreen;
            BtnPauseShowReceived.Foreground = isPaused ? Brushes.White : Brushes.Black;
        }
        /// <summary>
        /// 跟新自动发送的UI
        /// </summary>
        /// <param name="config">自动发送设置</param>
        private void UpdateAutoSendUI(AutoSendConfig config)
        {
            // 临时移除事件绑定
            chkAutoSend.Checked -= chkAutoSend_Changed;
            chkAutoSend.Unchecked -= chkAutoSend_Changed;

            try
            {
                chkAutoSend.IsChecked = config.Enabled;
                AutoSendInterval.Text = config.Interval.ToString();
            }
            finally
            {
                // 重新绑定事件
                chkAutoSend.Checked += chkAutoSend_Changed;
                chkAutoSend.Unchecked += chkAutoSend_Changed;
            }
        }
        private void UpdateFileSavePathUI(FileConfig config)
        {
            FilePathTextBox.Text = config.FilePath;
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
                    AppendToReceivedText($"---已开启串口 {_SerialPortConfig.PortName} ---\r\n");
                }
                else
                {
                    //关闭串口
                    _serialPortService.Close();
                    AppendToReceivedText($"---已关闭串口 {_SerialPortConfig.PortName} ---\r\n");
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
            text = text.Trim();

            if (!string.IsNullOrEmpty(text))
            {
                comReceived.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_receivedTextBuilder.Length > 0)
                    {
                        _receivedTextBuilder.AppendLine();
                    }

                    _receivedTextBuilder.Append(text);
                    comReceived.Text = _receivedTextBuilder.ToString();

                    if (_DisplayConfig.AutoScroll)
                    {
                        comReceived.ScrollToEnd();
                    }
                }));
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
            _DisplayConfig.PauseShowReceived = !(_DisplayConfig.PauseShowReceived);

            UpdatePauseShowRecvUI(_DisplayConfig.PauseShowReceived);
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SendData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送错误 {ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            }

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

            if (!string.IsNullOrEmpty(comSend.Text))
            {
                try
                {
                    byte[] data = _dataProcessService.ProcessSendData(comSend.Text, _DisplayConfig);
                    string displayText = _dataProcessService.EncodeData(data, _DisplayConfig.SendEncoding);
                    comSend.Text = displayText;
                }
                catch (ArgumentException ex)
                {
                    // 专门处理数据格式错误
                    MessageBox.Show($"数据格式错误: {ex.Message}", "格式错误",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }  
            }
            

        }
        private void cmbLineEnding_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_DisplayConfig == null) return;
            _DisplayConfig.LineEnding = GetLineEndingFromUI();
        }

        private void chkRecvAutoWrap_Changed(object sender, RoutedEventArgs e)
        {
            _DisplayConfig.AutoWrap = chkRecvAutoWrap.IsChecked ?? false;
        }
        private void chkAutoScroll_Changed(object sender, RoutedEventArgs e)
        {
            _DisplayConfig.AutoScroll = chkAutoScroll.IsChecked ?? false;
        }

        private void chkShowSend_Changed(object sender, RoutedEventArgs e)
        {
            _DisplayConfig.ShowSend = chkShowSend.IsChecked ?? false;
        }

        private void chkRecvShowRowNum_Changed(object sender, RoutedEventArgs e)
        {
           _DisplayConfig.ShowRowNumbers=chkRecvShowRowNum.IsChecked ?? false;
        }

        private void chkRecvShowTxRx_Changed(object sender, RoutedEventArgs e)
        {
            _DisplayConfig.ShowTxRx = chkRecvShowTxRx.IsChecked ?? false;
        }

        private void chkEnableFrame_Changed(object sender, RoutedEventArgs e)
        {
            _FrameConfig.Enabled = chkEnableFrame.IsChecked ?? false;
            SetFrameConfig(_FrameConfig);
            if (_FrameConfig.Enabled)
            {
                AppendToReceivedText($"帧头：{_FrameConfig.Header} 帧尾：{_FrameConfig.Footer}");
            }
            else
            {
                AppendToReceivedText("已关闭帧处理");
            }
        }
        private void chkAutoSend_Changed(object sender, RoutedEventArgs e)
        {
            _AutoSendConfig.Enabled = chkAutoSend.IsChecked ?? false;
            SetAutoSendConfig(_AutoSendConfig);
            if (_AutoSendConfig.Enabled)
            {
                _autoSendService.Start(_AutoSendConfig);
                AppendToReceivedText($"自动发送间隔为 {_AutoSendConfig.Interval}ms");
            }
            else
            {
                _autoSendService.Stop();
                AppendToReceivedText("已关闭自动发送");
            }
        }

        private void BtnSaveRece2File_Click(object sender, RoutedEventArgs e)
        {
            //获取文件路径
            string filePath = _FileConfig.FilePath;
            string data = _receivedTextBuilder.ToString();
            
            // 验证文件路径
            if (!string.IsNullOrEmpty(filePath))
            {
                bool saveSuccess = _fileService.SaveText2File(data, _FileConfig);

                if (saveSuccess)
                {
                    MessageBox.Show("保存成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("请先选择或输入有效的文件路径！", "警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
                
        }

        private void BtnBrowseFilePath_Click(object sender, RoutedEventArgs e)
        {
            try
            {   
                //获取所选的文件路径
                string filePath = _fileService.SelectSaveFilePath(_FileConfig);

                if (!string.IsNullOrEmpty(filePath))
                {
                    _FileConfig.FilePath = System.IO.Path.GetDirectoryName(filePath);
                    _FileConfig.FileName = System.IO.Path.GetFileName(filePath);
                    UpdateFileSavePathUI(_FileConfig);
                    AppendToReceivedText($"文件保存路径：{filePath}\n");
                }
                else
                {
                    //取消选择，还原为程序路径
                    _FileConfig.FilePath = GetAppDir();
                    _FileConfig.FileName = "serial_data.txt";
                    UpdateFileSavePathUI(_FileConfig);
                    AppendToReceivedText($"取消选择，文件保存路径：{filePath}\n");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择文件路径时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private string GetLineEndingFromUI()
        {
            if (cmbLineEnding.SelectedItem is ComboBoxItem selectedItem)
            {
                string lineEnding = selectedItem.Tag?.ToString() ?? "";
                lineEnding = ConvertDisplayToActual(lineEnding);

                //// 调试查看实际内容
                //Debug.WriteLine($"LineEnding length: {lineEnding.Length}");
                //foreach (char c in lineEnding)
                //{
                //    Debug.WriteLine($"Char: {(int)c} ('{c}')");
                //}

                return lineEnding;
            }
            return "";
        }
        #endregion

        #region 辅助工具
        private void SendData()
        {
            if (!_serialPortService.IsOpen)
            {
                throw new ArgumentException("请先打开串口");
                //MessageBox.Show("请先打开串口", "提示", MessageBoxButton.OK, MessageBoxImage.Error);       
                //return;
            }

            try
            {
                //从发送文本框获取数据
                string sendText = comSend.Text;
                if (string.IsNullOrEmpty(sendText)) return;

                //解码数据，将其变为字节流并添加帧头/帧尾
                byte[] data = _dataProcessService.ProcessSendData(sendText, _DisplayConfig);


                _serialPortService.SendData(data);

                //显示发送
                if (_DisplayConfig.ShowSend && !_DisplayConfig.PauseShowReceived)
                {
                    //发送区显示TX/RX
                    //string displayText = _dataProcessService.FormatWithTxRxPrefix(sendText, _DisplayConfig, false);
                    //AppendToReceivedText(displayText);

                    //将显示的数据按照发送编码形式编码为字符串
                    string displayText = _dataProcessService.EncodeData(data, _DisplayConfig.SendEncoding);
                    //发送区显示TX/RX
                    displayText = _dataProcessService.FormatWithTxRxPrefix(sendText, _DisplayConfig, false);
                    AppendToReceivedText(displayText);
                }
            }
            catch (ArgumentException ex)
            {
                // 专门处理数据格式错误
                MessageBox.Show($"数据格式错误: {ex.Message}", "格式错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void SetFrameConfig(FrameConfig config)
        {
            if (config.Enabled)
            {
                FrameHeader.IsReadOnly = true;
                FrameFooter.IsReadOnly = true;
                config.Header = FrameHeader.Text;
                config.Footer = FrameFooter.Text;
                // 添加调试输出     
            }
            else
            {
  
                FrameHeader.IsReadOnly = false;
                FrameFooter.IsReadOnly = false;           
            }
            _dataProcessService.SetFrameConfig(config);
        }

        private void SetAutoSendConfig(AutoSendConfig config)
        {
            if (config.Enabled)
            {
                //关闭间隔设置
                AutoSendInterval.IsReadOnly = true;
                //读取间隔
                config.Interval = int.Parse(AutoSendInterval.Text);     
            }
            else
            {
                AutoSendInterval.IsReadOnly = false;
                
            }
            _autoSendService.SetAutoSendConfig(config);
        }

        private void SetFileConfig(FileConfig config)
        {
            config.FilePath = GetAppDir();
            _fileService.SetFileConfig(config);
            UpdateFileSavePathUI(config);
        }

        private string GetAppDir()
        {
            try
            {
                //获取当前程序运行的完整路径，包含程序名 xxx.exe
                string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                //提取完整路径的文件夹路径
                string appDir = System.IO.Path.GetDirectoryName(appPath);

                return appDir;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取程序路径失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return string.Empty;
            }
        }
        /// <summary>
        /// 还原自动发送的设置与UI
        /// </summary>
        /// <param name="interval">发送间隔</param>
        private void RestoreAutoSend(int interval)
        {
            AutoSendConfig config = new AutoSendConfig();
            config.Enabled = false;
            config.Interval = interval;
            SetAutoSendConfig(config);
            UpdateAutoSendUI(config);
            _autoSendService.Stop();
        }

        /// <summary>
        /// 将显示的换行符文本转换为实际的控制字符
        /// </summary>
        private string ConvertDisplayToActual(string displayText)
        {
            if (string.IsNullOrEmpty(displayText))
                return "";

            // 将显示的 "\r\n" 等转换为实际的控制字符
            return displayText
                .Replace("\\r", "\r")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t");
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