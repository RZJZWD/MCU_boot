using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace MCUBoot.DateModels
{
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
        /// 传输参数
        /// </summary>
        public BootTransferConfig Transfer { get; set; } = new BootTransferConfig();
    }

    /// <summary>
    /// 固件信息模型
    /// </summary>
    public class FirmwareInfo
    {
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; } = "";
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; } = "";
        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize { get; set; }
        /// <summary>
        /// 文件数据
        /// </summary>
        public byte[] FileData { get; set; }
        /// <summary>
        /// 最后一次修改时间
        /// </summary>
        public DateTime LastModified { get; set; }
        /// <summary>
        /// MD5哈希值
        /// </summary>
        public string MD5Hash { get; set; } = "";
        /// <summary>
        /// 整个固件的CRC32
        /// </summary>
        public uint WholeFileCRC { get; set; }  // 整个固件的CRC32
        /// <summary>
        /// 分包CRC32，每个包序号对应一个CRC32
        /// </summary>
        public Dictionary<int, uint> PacketCRCs { get; set; } = new Dictionary<int, uint>();  // 分包CRC32
        /// <summary>
        /// 固件分包大小
        /// </summary>
        public int PacketSize { get; set; }
        /// <summary>
        /// 是否校验成功
        /// </summary>
        public bool IsValid { get; set; }
        /// <summary>
        /// 应用加载地址(下位机)
        /// </summary>
        public uint AppLoadAddr { get; set; }
    }

    /// <summary>
    /// Boot传输配置
    /// </summary>
    public class BootTransferConfig
    {
        /// <summary>
        /// 超时时长
        /// </summary>
        public int Timeout { get; set; } = 3000;
        /// <summary>
        /// 重试次数
        /// </summary>
        public int RetryCount { get; set; } = 3;
        /// <summary>
        /// 行尾
        /// </summary>
        public string LineEnding { get; set; } = "";
    }

    /// <summary>
    /// 命令类型
    /// </summary>
    public enum CommandType : byte
    {
        /// <summary>
        /// 下位机进入boot模式
        /// </summary>
        EnterBoot = 0x01,
        /// <summary>
        /// 上传固件
        /// </summary>
        Upload = 0x02,
        /// <summary>
        /// 验证固件
        /// </summary>
        Verify = 0x03,
        /// <summary>
        /// 下位机运行应用
        /// </summary>
        RunApp = 0x04,
        /// <summary>
        /// 应答，正向
        /// </summary>
        Ack = 0x05,
        /// <summary>
        /// 应答，有错误
        /// </summary>
        Nack = 0x06,
        /// <summary>
        /// 错误响应命令
        /// </summary>
        ErrorResponse = 0x07,
        //未知命令，测试
        unknow = 0x77
    }

    /// <summary>
    /// 命令帧格式：[帧头][命令][长度][数据][帧校验]
    /// 帧头 (2字节)：0xAA 0x55
    /// 命令字 (1字节)
    /// 数据长度 (2字节)：小端序
    /// 数据 (N字节)
    /// 校验和 (1字节)：从命令字到数据结束的累加和取反
    /// </summary>
    public class CommandFrame
    {
        private const byte HEADER_HIGH = 0xAA;
        private const byte HEADER_LOW = 0x55;

        public CommandType Command { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// 将命令帧序列化为字节数组
        /// </summary>
        public byte[] ToBytes()
        {
            var bytes = new List<byte>();

            // 帧头
            bytes.Add(HEADER_HIGH);
            bytes.Add(HEADER_LOW);

            // 命令字
            bytes.Add((byte)Command);

            // 数据长度 (小端序)
            ushort dataLength = (ushort)(Data?.Length ?? 0);
            bytes.Add((byte)(dataLength & 0xFF));        // 低字节
            bytes.Add((byte)((dataLength >> 8) & 0xFF)); // 高字节

            // 数据
            if (Data != null && Data.Length > 0)
            {
                bytes.AddRange(Data);
            }

            // 计算校验和 (从命令字到数据结束的累加和取反，不含帧头)
            byte checksum = CalculateChecksum(bytes.Skip(2).ToArray());
            bytes.Add(checksum);

            return bytes.ToArray();
        }

        /// <summary>
        /// 从字节数组解析命令帧
        /// </summary>
        public static CommandFrame FromBytes(byte[] data)
        {
            if (data == null || data.Length < 6) // 最小帧长度: 2(帧头)+1(命令)+2(长度)+1(校验)
                return null;

            // 检查帧头
            if (data[0] != HEADER_HIGH || data[1] != HEADER_LOW)
                return null;

            // 提取数据长度 (小端序)
            ushort dataLength = (ushort)(data[3] | (data[4] << 8));

            // 检查数据长度是否合理
            if (data.Length != 6 + dataLength) // 6 = 2(帧头)+1(命令)+2(长度)+1(校验)
                return null;

            // 验证校验和
            byte receivedChecksum = data[data.Length - 1];
            byte calculatedChecksum = CalculateChecksum(data.Skip(2).Take(3 + dataLength).ToArray());

            if (receivedChecksum != calculatedChecksum)
                return null;

            var frame = new CommandFrame
            {
                Command = (CommandType)data[2]
            };

            // 提取数据
            if (dataLength > 0)
            {
                frame.Data = new byte[dataLength];
                Array.Copy(data, 5, frame.Data, 0, dataLength);
            }

            return frame;
        }

        /// <summary>
        /// 计算校验和
        /// </summary>
        private static byte CalculateChecksum(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            byte sum = 0;
            foreach (byte b in data)
            {
                sum += b;
            }
            return (byte)~sum; // 取反
        }
    }

    /// <summary>
    /// 设备信息
    /// </summary>
    public class DeviceInfo
    {
        // 固定长度定义
        private const int MODEL_LENGTH = 32;        // 型号名称固定32字节
        private const int BOOT_VERSION_LENGTH = 16; // 引导版本固定16字节
        private const int DeviceInfoSize = MODEL_LENGTH + sizeof(uint) + sizeof(uint) + sizeof(uint) + BOOT_VERSION_LENGTH;
        /// <summary>
        /// 设备名
        /// </summary>
        public string Model { get; set; } = "";
        /// <summary>
        /// 设备flash大小
        /// </summary>
        public uint FlashSize { get; set; }
        /// <summary>
        /// 设备app加载地址
        /// </summary>
        public uint AppAddress { get; set; }
        /// <summary>
        /// 固件分包大小
        /// </summary>
        public uint FirmwarePacketSize { get; set; }
        /// <summary>
        /// boot程序版本
        /// </summary>
        public string BootVersion { get; set; } = "";

        /// <summary>
        /// 小端序
        /// </summary>
        /// <returns>字节数组</returns>
        public byte[] ToBytes()
        {                
            using(MemoryStream ms = new MemoryStream())
            using(BinaryWriter bw = new BinaryWriter(ms))
            {
                // 写入model字节，固定32字节，右填充
                byte[] modelBytes = Encoding.UTF8.GetBytes(Model.PadRight(MODEL_LENGTH,'\0'));
                bw.Write(modelBytes,0,MODEL_LENGTH);
                // 写入FlashSize，4字节
                bw.Write(FlashSize);
                // 写入AppAddress，4字节  
                bw.Write(AppAddress);
                // 写入固件包大小信息，4字节  
                bw.Write(FirmwarePacketSize);
                // 写入BootVersion，固定16字节
                byte[] bootVersionBytes = Encoding.UTF8.GetBytes(BootVersion.PadRight(BOOT_VERSION_LENGTH,'\0'));
                bw.Write(bootVersionBytes, 0, BOOT_VERSION_LENGTH);

                return ms.ToArray();
            }
        }
        /// <summary>
        /// 小端序
        /// </summary>
        /// <param name="data">字节数组</param>
        /// <returns>DeviceInfo类</returns>
        /// <exception cref="ArgumentException"></exception>
        public void FromBytes(byte[] data)
        {
            if (data == null || data.Length != DeviceInfoSize)
            {
                throw new ArgumentException("无效的字节数组长度");
            }
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(ms))
            {

                // 读取Model
                byte[] modelBytes = reader.ReadBytes(MODEL_LENGTH);
                Model = Encoding.UTF8.GetString(modelBytes).TrimEnd('\0');

                // 读取FlashSize
                FlashSize = reader.ReadUInt32();

                // 读取AppAddress
                AppAddress = reader.ReadUInt32();

                // 读取FirmwarePacketSize
                FirmwarePacketSize = reader.ReadUInt32();

                // 读取BootVersion
                byte[] bootVersionBytes = reader.ReadBytes(BOOT_VERSION_LENGTH);
                BootVersion = Encoding.UTF8.GetString(bootVersionBytes).TrimEnd('\0');
            }
        }
    }

    /// <summary>
    /// 命令调度控制项
    /// </summary>
    public class BootCommandItem
    {
        /// <summary>
        /// 发送的命令
        /// </summary>
        public CommandType SendCommand { get; set; }
        /// <summary>
        /// 发送的数据
        /// </summary>
        public byte[] SendData { get; set; } = new byte[0];
        /// <summary>
        /// 期待的回应
        /// </summary>
        public CommandType ResponseCommand { get; set; }
        /// <summary>
        /// 命令描述
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// 自定义传输层超时
        /// </summary>
        public int? TransferTimeoutMs { get; set; }
        /// <summary>
        /// 自定义传输层重试次数
        /// </summary>
        public int? TransferRetryCount { get; set; }
        /// <summary>
        /// 自定义调度层重试次数
        /// </summary>
        public int? ScheduleRetryCount { get; set; }
        /// <summary>
        /// 默认调度层重试次数
        /// </summary>
        private int defaultScheduleRetryCount { get; } = 3;
        /// <summary>
        /// 当前调度层重试次数
        /// </summary>
        private int currentScheduleRetryCount { get; set; } = 0;
        /// <summary>
        /// 调度层回应处理器 - 根据响应决定是否重试
        /// </summary>
        public Func<CommandFrame, ResponseAction> ResponseHandler { get; set; }

        public BootCommandItem CreateRetryCommand(BootCommandItem original)
        {
            //int maxRetryCount = BCI.ScheduleRetryCount ?? BCI.defaultScheduleRetryCount; // 默认重试3次
            //if (BCI.currentScheduleRetryCount < maxRetryCount)
            //{
            //    BCI.currentScheduleRetryCount++;
            //    return BCI;
            //}
            //else
            //{
            //    return null;
            //}
            if (ScheduleRetryCount <= 0)
                return null;

            return new BootCommandItem
            {
                SendCommand = original.SendCommand,
                ResponseCommand = original.ResponseCommand,
                SendData = original.SendData,
                Description = $"{original.Description} (重试)",
                TransferTimeoutMs = original.TransferTimeoutMs,
                TransferRetryCount = original.TransferRetryCount,
                ScheduleRetryCount = original.ScheduleRetryCount - 1, // 减少重试次数
                ResponseHandler = original.ResponseHandler
            };
        }
    }

    /// <summary>
    /// 回应动作，针对不同回应给出操作
    /// </summary>
    public enum ResponseAction
    {
        /// <summary>
        /// 继续下一个命令
        /// </summary>
        Continue,
        /// <summary>
        /// 非阻塞重试当前命令，追加到命令队列末尾
        /// </summary>
        Retry,
        /// <summary>
        /// 停止执行
        /// </summary>
        Stop,
        /// <summary>
        /// 跳过当前命令
        /// </summary>
        Skip,
    }
    /// <summary>
    /// 命令队列执行结果
    /// </summary>
    public class BootCommandResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }
        /// <summary>
        /// 回应列表
        /// </summary>
        public List<CommandFrame> Responses { get; set; } = new List<CommandFrame>();
        /// <summary>
        /// 执行次数
        /// </summary>
        public int ExecutedCount { get; set; }
        /// <summary>
        /// 命令总数
        /// </summary>
        public int TotalCount { get; set; }
    }


    /// <summary>
    /// Boot状态枚举
    /// </summary>
    public enum BootStatus
    {
        /// <summary>
        /// 未连接
        /// </summary>
        Disconnected,
        /// <summary>
        /// 已连接
        /// </summary>
        Connected,
        /// <summary>
        /// 下位机进入boot模式
        /// </summary>
        InBootMode,
        /// <summary>
        /// 传输中
        /// </summary>
        Transfer,
        /// <summary>
        /// 验证中
        /// </summary>
        Verifying,
        /// <summary>
        /// 完成
        /// </summary>
        Completed,
        /// <summary>
        /// 错误
        /// </summary>
        Error
    }
}
