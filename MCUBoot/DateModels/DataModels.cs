using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCUBoot.DateModels
{
    /// <summary>
    /// 上位机模式控制
    /// </summary>
    public class OperatModeConfig
    {
        public OperatingMode Mode { get; set; } = OperatingMode.Serial;
    }
    /// <summary>
    /// 串口配置参数模型
    /// </summary>
    public class SerialPortConfig
    {
        //串口名称
        public string PortName { get; set; } = "COM1";
        //波特率
        public int BaudRate { get; set; } = 9600;
        //数据位
        public int DataBits { get; set; } = 8;
        //停止位
        public string StopBits { get; set; } = "1";
        //校验位
        public string Parity { get; set; } = "无";
        //是否开启串口
        public bool IsOpen { get; set; }
        //是否使能DTR
        public bool EnableDTR { get; set; } = true;
        public string NewLine = "";
    }

    /// <summary>
    /// 数据显示配置模型
    /// </summary>
    public class DisplayConfig
    {
        /// <summary>
        /// 自动换行
        /// </summary>
        public bool AutoWrap { get; set; } = true;
        /// <summary>
        /// 自动滚动
        /// </summary>
        public bool AutoScroll { get; set; } = true;
        /// <summary>
        /// 显示发送
        /// </summary>
        public bool ShowSend { get; set; } = true;
        /// <summary>
        /// 显示行号
        /// </summary>
        public bool ShowRowNumbers { get; set; }
        /// <summary>
        /// 显示发送/接收方
        /// </summary>
        public bool ShowTxRx { get; set; }
        /// <summary>
        /// 接收解码
        /// </summary>
        public EncodingType ReceiveEncoding { get; set; } = EncodingType.UTF8;
        /// <summary>
        /// 发送编码
        /// </summary>
        public EncodingType SendEncoding { get; set; } = EncodingType.UTF8;
        /// <summary>
        /// 暂停显示接收
        /// </summary>
        public bool PauseShowReceived { get; set; } = false;
        /// <summary>
        /// 尾行
        /// </summary>
        public string LineEnding { get; set; } = "";
    }

    /// <summary>
    /// 帧处理配置模型
    /// </summary>
    public class FrameConfig
    {
        /// <summary>
        /// 启用帧处理
        /// </summary>
        public bool Enabled { get; set; } = false;
        /// <summary>
        /// 帧头
        /// </summary>
        public string Header { get; set; } = "";
        /// <summary>
        /// 帧尾
        /// </summary>
        public string Footer { get; set; } = "";
    }
    public class AutoSendConfig
    {
        /// <summary>
        /// 使能
        /// </summary>
        public bool Enabled { get; set; } = false;
        /// <summary>
        /// 间隔
        /// </summary>
        public int Interval { get; set; } = 0;
    }

    /// <summary>
    /// 串口文件配置
    /// </summary>
    public class FileConfig
    {
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; } = "serial_data.txt";
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
    }

    

    /// <summary>
    /// 编码类型枚举
    /// </summary>
    public enum EncodingType
    {
        ASCII,
        UTF8,
        GB2312,
        Hex,
        Decimal,
        Octal,
        Binary
    }

    //功能模式枚举
    public enum OperatingMode
    {
        Serial,
        Boot
    }
}