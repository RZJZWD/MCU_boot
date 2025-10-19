using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCUBoot.DateModels
{
    //上位机模式
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
    }

    /// <summary>
    /// 数据显示配置模型
    /// </summary>
    public class DisplayConfig
    {
        public bool AutoWrap { get; set; } = true;
        public bool AutoScroll { get; set; } = true;
        public bool ShowSend { get; set; } = true;
        public bool ShowRowNumbers { get; set; }
        public bool ShowTxRx { get; set; }
        public EncodingType ReceiveEncoding { get; set; } = EncodingType.UTF8;
        public EncodingType SendEncoding { get; set; } = EncodingType.UTF8;
        //暂停显示接收
        public bool PauseShowReceived { get; set; } = false;

        public string LineEnding { get; set; } = "";
    }

    /// <summary>
    /// 帧处理配置模型
    /// </summary>
    public class FrameConfig
    {
        //启用帧处理
        public bool Enabled { get; set; } = false;
        public string Header { get; set; } = "";
        public string Footer { get; set; } = "";
    }
    public class AutoSendConfig
    {
        public bool Enabled { get; set; } = false;
        public int Interval { get; set; } = 0;
    }

    public class FileConfig
    {
        public string FileName { get; set; } = "serial_data.txt";
        public string FilePath { get; set; } = "";
    }

    /// <summary>
    /// Boot配置模型 - 整合所有Boot相关配置
    /// </summary>
    public class BootConfig
    {
        /// <summary>
        /// 固件信息
        /// </summary>
        public FirmwareInfo Firmware { get; set; } = new FirmwareInfo();

        /// <summary>
        /// Boot状态
        /// </summary>
        public BootStatus Status { get; set; } = BootStatus.Disconnected;

        /// <summary>
        /// Boot命令配置
        /// </summary>
        public BootCommandConfig Commands { get; set; } = new BootCommandConfig();

        /// <summary>
        /// 传输参数
        /// </summary>
        public BootTransferConfig Transfer { get; set; } = new BootTransferConfig();
    }

    /// <summary>
    /// 固件信息模型
    /// </summary>
    public class FirmwareInfo
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public byte[] FileData { get; set; }
        public DateTime LastModified { get; set; }
        public string MD5Hash { get; set; } = "";
        public uint WholeFileCRC { get; set; }  // 整个固件的CRC32
        public Dictionary<int, uint> PacketCRCs { get; set; } = new Dictionary<int, uint>();  // 分包CRC32
        public int PacketSize { get; set; }
        public bool IsValid { get; set; }
    }

    /// <summary>
    /// Boot状态枚举
    /// </summary>
    public enum BootStatus
    {
        Disconnected,
        Connected,
        InBootMode,
        Uploading,
        Verifying,
        Completed,
        Error
    }

    /// <summary>
    /// Boot命令配置模型
    /// </summary>
    public class BootCommandConfig
    {
        public string EnterBoot { get; set; } = "55AA01";
        public string Upload { get; set; } = "55AA02";
        public string Verify { get; set; } = "55AA03";
        public string RunApp { get; set; } = "55AA04";
        public string Ack { get; set; } = "55AA06";
        public string Nack { get; set; } = "55AA07";
    }

    /// <summary>
    /// Boot传输配置
    /// </summary>
    public class BootTransferConfig
    {
        public int PacketSize { get; set; }
        public int Timeout { get; set; } = 3000;
        public int RetryCount { get; set; } = 3;
    }

    /// <summary>
    /// Boot命令类型枚举
    /// </summary>
    public enum BootCommandType
    {
        EnterBoot,
        Upload,
        Verify,
        RunApp,
        Ack,
        Nack,
        Unknown
    }

    /// <summary>
    /// Boot响应类型
    /// </summary>
    public enum BootResponseType
    {
        Unknown,
        Ack,
        Nack
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