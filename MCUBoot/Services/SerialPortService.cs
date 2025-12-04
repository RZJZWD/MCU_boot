using MCUBoot.DateModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Timers;

namespace MCUBoot.Services
{
    /// <summary>
    /// 串口通信服务 - 负责所有串口底层操作
    /// </summary>
    public class SerialPortService : IDisposable
    {
        private SerialPort _serialPort;
        private System.Timers.Timer _portCheckTimer= new System.Timers.Timer();
        private string[] _lastAvailablePorts=Array.Empty<string>();
        private readonly object _receiveLock = new object();
        private bool _isReceiving = false;

        //事件定义，数据接收 错误产生 连接状态改变 可用串口改变
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<bool> ConnectionStateChanged;
        public event EventHandler<string[]> AvailablePortsChanged;

        public bool IsOpen => _serialPort?.IsOpen ?? false;

        public SerialPortService()
        {
            _serialPort = new SerialPort()
            {
                ReadTimeout = 100,
                WriteTimeout = 1000,
                ReceivedBytesThreshold = 1
            };
            _serialPort.DataReceived += OnDataReceived;
            InitializePortCheckTimer();
        }

        /// <summary>
        /// 初始化串口检查定时器 - 始终运行，不受连接状态影响
        /// </summary>
        private void InitializePortCheckTimer()
        {
            _portCheckTimer.Interval = 2000; // 2秒检查一次
            _portCheckTimer.Elapsed += OnPortCheckTimerElapsed;
            _portCheckTimer.AutoReset = true;
            _portCheckTimer.Start();
            //System.Diagnostics.Debug.WriteLine("[Service] SerialPortService 初始化，启动定时器");
            // 初始检查一次
            CheckAvailablePorts();
        }

        /// <summary>
        /// 定时器事件处理 - 检查可用串口
        /// </summary>
        private void OnPortCheckTimerElapsed(object sender, ElapsedEventArgs e)
        {
            CheckAvailablePorts();
        }

        /// <summary>
        /// 检查可用串口列表
        /// </summary>
        private void CheckAvailablePorts()
        {
            //System.Diagnostics.Debug.WriteLine($"[Service] 检测可用串口，当前 IsOpen: {_serialPort.IsOpen}, PortName: {_serialPort.PortName}");
            try
            {
                string[] availablePorts = SerialPort.GetPortNames();

                //热拔插检测：如果串口已打开，但其 PortName 不在可用列表中 → 被拔出
                if (!_serialPort.IsOpen)
                {
                    AutoClose(); // 会触发 ConnectionStateChanged(false)
                }

                // 通知 UI 可用端口列表变化（用于刷新下拉框）
                if (!ArePortArraysEqual(_lastAvailablePorts, availablePorts))
                {
                    _lastAvailablePorts = availablePorts;
                    AvailablePortsChanged?.Invoke(this, availablePorts);
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"定时检测失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 比较两个串口数组是否相同
        /// </summary>
        private bool ArePortArraysEqual(string[] array1, string[] array2)
        {
            if (array1 == null && array2 == null) return true;
            if (array1 == null || array2 == null) return false;
            if (array1.Length != array2.Length) return false;

            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 打开串口连接
        /// </summary>
        public void Open(SerialPortConfig config)
        {
            if (_serialPort.IsOpen)
            {
                Close();
            }

            ConfigureSerialPort(config);

            try
            {
                //尝试打开串口，成功调用状态改变事件改变串口状态
                _serialPort.Open();
                ConnectionStateChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"打开串口失败：{ex.Message}");
                
                throw;
            }
        }
        /// <summary>
        /// 配置串口参数
        /// </summary>
        private void ConfigureSerialPort(SerialPortConfig config)
        {
            if (!string.IsNullOrEmpty(config.NewLine))
            {
                _serialPort.NewLine = config.NewLine;
            }
            _serialPort.PortName = config.PortName;
            _serialPort.BaudRate = config.BaudRate;
            _serialPort.DataBits = config.DataBits;
            _serialPort.StopBits = ParseStopBits(config.StopBits);
            _serialPort.Parity = ParseParity(config.Parity);
            _serialPort.DtrEnable = config.EnableDTR;
            
        }
        /// <summary>
        /// 解析停止位字符串为枚举值
        /// </summary>
        private StopBits ParseStopBits(string stopBits)
        {
            return stopBits switch
            {
                "1" => StopBits.One,
                "1.5" => StopBits.OnePointFive,
                "2" => StopBits.Two,
                _ => StopBits.One
            };
        }
        /// <summary>
        /// 解析校验位字符串为枚举值
        /// </summary>
        private Parity ParseParity(string parity)
        {
            return parity switch
            {
                "无" => Parity.None,
                "奇校验" => Parity.Odd,
                "偶校验" => Parity.Even,
                _ => Parity.None
            };
        }


        /// <summary>
        /// 关闭串口连接
        /// </summary>
        public void Close()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
                //System.Diagnostics.Debug.WriteLine("[Service] 串口已手动关闭，触发 ConnectionStateChanged(false)");
                ConnectionStateChanged?.Invoke(this,false);
            }
        }
        /// <summary>
        /// 关闭串口连接
        /// </summary>
        public void AutoClose()
        {
            _serialPort.Close();
            //System.Diagnostics.Debug.WriteLine("[Service] 热拔插串口已关闭，触发 ConnectionStateChanged(false)");
            ConnectionStateChanged?.Invoke(this, false);
        }


        /// <summary>
        /// 发送字节数据
        /// </summary>
        public void SendData(byte[] data)
        {
            if (!_serialPort.IsOpen)
            {
                throw new InvalidOperationException("串口未打开");
            }

            try
            {
                _serialPort.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"发送数据失败：{ex.Message}");
                throw;
            }
        }
        /// <summary>
        /// 发送一行数据
        /// </summary>
        /// <param name="data">数据</param>
        /// <exception cref="InvalidOperationException"></exception>
        public void WriteLine(string data)
        {
            if (!_serialPort.IsOpen)
            {
                throw new InvalidOperationException("串口未打开");
            }
            try
            {
                _serialPort.WriteLine(data);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"发送数据失败：{ex.Message}");
                throw;
            }
        }



        /// <summary>
        /// 数据接收事件处理
        /// </summary>
        //private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        //{
        //    try
        //    {
        //        // 获取当前可读字节数
        //        int bytesToRead = _serialPort.BytesToRead;
        //        if (bytesToRead <= 0) return;

        //        // 创建缓冲区并读取数据
        //        byte[] buffer = new byte[Math.Min(bytesToRead, BUFFER_SIZE)];
        //        int bytesRead = _serialPort.Read(buffer, 0, buffer.Length);

        //        // 处理接收到的数据
        //        if (bytesRead > 0)
        //        {
        //            // 如果实际读取的字节数小于缓冲区大小，创建合适大小的数组
        //            byte[] receivedData;
        //            if (bytesRead < buffer.Length)
        //            {
        //                receivedData = new byte[bytesRead];
        //                Array.Copy(buffer, 0, receivedData, 0, bytesRead);
        //            }
        //            else
        //            {
        //                receivedData = buffer;
        //            }

        //            DataReceived?.Invoke(this, new DataReceivedEventArgs(receivedData));
        //        }
        //    }
        //    catch (TimeoutException)
        //    {
        //        // 读取超时是正常情况，不视为错误
        //    }
        //    catch (Exception ex)
        //    {
        //        ErrorOccurred?.Invoke(this, $"接收数据错误: {ex.Message}");
        //    }
        //}
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // 防止重入
            if (_isReceiving) return;

            lock (_receiveLock)
            {
                if (_isReceiving) return;
                _isReceiving = true;
            }

            try
            {
                // 等待一小段时间，让数据积累
                Thread.Sleep(10);

                List<byte> tempBytes = new List<byte>();

                // 循环读取直到没有数据
                while (_serialPort.IsOpen && _serialPort.BytesToRead > 0)
                {
                    int bytesToRead = Math.Min(_serialPort.BytesToRead, 4096);
                    byte[] buffer = new byte[bytesToRead];

                    int bytesRead = _serialPort.Read(buffer, 0, bytesToRead);
                    if (bytesRead > 0)
                    {
                        // 只添加实际读取的字节
                        if (bytesRead < buffer.Length)
                        {
                            byte[] actualData = new byte[bytesRead];
                            Array.Copy(buffer, 0, actualData, 0, bytesRead);
                            tempBytes.AddRange(actualData);
                        }
                        else
                        {
                            tempBytes.AddRange(buffer);
                        }
                    }
                }

                // 如果有数据，触发事件
                if (tempBytes.Count > 0)
                {
                    byte[] dataBuf = tempBytes.ToArray();
                    DataReceived?.Invoke(this, new DataReceivedEventArgs(dataBuf));
                }
            }
            catch (TimeoutException)
            {
                // 超时是正常情况
            }
            catch (InvalidOperationException ex)
            {
                // 串口可能在读取过程中被关闭
                if (!ex.Message.Contains("端口已关闭"))
                {
                    ErrorOccurred?.Invoke(this, $"接收数据时串口操作异常: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"接收数据错误: {ex.Message}");
            }
            finally
            {
                lock (_receiveLock)
                {
                    _isReceiving = false;
                }
            }
        }

        public void Dispose()
        {
            _portCheckTimer?.Stop();
            _portCheckTimer?.Dispose();
            _serialPort?.Close();
            _serialPort?.Dispose();
                  
        }
    }

    /// <summary>
    /// 数据接收事件参数
    /// </summary>
    public class DataReceivedEventArgs : EventArgs
    {
        public byte[] Data { get; }
        public DateTime Timestamp { get; }
        public DataReceivedEventArgs(byte[] data)
        {
            Data = data;
            Timestamp = DateTime.Now;
        }
    }
}
