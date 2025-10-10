using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCUBoot.DateModels
{
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
        public bool ShowSend {  get; set; } = true;
        public bool ShowRowNumbers { get; set; }
        public bool ShowTxRx { get; set; }
        public EncodingType ReceiveEncoding { get; set; } = EncodingType.UTF8;
        public EncodingType SendEncoding { get; set; } = EncodingType.UTF8;
        //同步发送编码方式到接收解码
        public bool SyncDecode2Encode { get; set; }

        //暂停显示接收
        public bool PauseShowReceived { get; set; } = false;
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

}
