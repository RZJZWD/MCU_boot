using System;
using System.IO.Ports;
using System.Timers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MCUBoot.DateModels;
using System.Diagnostics;
using System.Threading.Channels;
using System.Linq.Expressions;

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

        //事件定义，数据接收 错误产生 连接状态改变 可用串口改变
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<bool> ConnectionStateChanged;
        public event EventHandler<string[]> AvailablePortsChanged;

        public bool IsOpen => _serialPort?.IsOpen ?? false;

        public SerialPortService()
        {
            _serialPort = new SerialPort();
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
            try
            {
                string[] currentPorts = SerialPort.GetPortNames();

                // 检查端口列表是否发生变化
                if (!ArePortArraysEqual(_lastAvailablePorts, currentPorts))
                {
                    _lastAvailablePorts = currentPorts;
                    AvailablePortsChanged?.Invoke(this, currentPorts);
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"检查可用串口失败: {ex.Message}");
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
                ConnectionStateChanged?.Invoke(this,false);
            }
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
        /// 数据接收事件处理
        /// </summary>
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {   
                //检查当前串口缓冲区数据长度
                if (_serialPort.BytesToRead > 0)
                {
                    //根据缓冲区长度创建接收事件数组
                    byte[] buffer = new byte[_serialPort.BytesToRead];
                    _serialPort.Read(buffer, 0, buffer.Length);
                    //将接收的数据传给接收事件的订阅者
                    DataReceived?.Invoke(this, new DataReceivedEventArgs(buffer));
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"接收数据错误: {ex.Message}");
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
