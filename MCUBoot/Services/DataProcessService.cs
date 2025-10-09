using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using MCUBoot.DateModels;

namespace MCUBoot.Services
{
    /// <summary>
    /// 数据处理服务 - 负责数据编码解码和格式转换
    /// </summary>
    public class DataProcessService
    {
        private readonly List<byte> _frameBuffer = new List<byte>();
        private FrameConfig _frameConfig;

        //事件定义 数据处理(带帧处理)完成 数据处理(无帧处理)完成
        public event EventHandler<ReceivedFrameProcessedEventArgs> ReceivedFrameProcessed;
        public event EventHandler<string> ReceivedDaraProcessed;

        /// <summary>
        /// 设置帧处理配置
        /// </summary>
        public void SetFrameConfig(FrameConfig Config)
        {
            _frameConfig = Config;
            //如果没有启用帧处理
            if (!Config.Enabled)
            {
                _frameBuffer.Clear();
            }
        }

        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        public void ProcessReceivedData(byte[] data, DisplayConfig displayConfig)
        {
            //是否开启帧处理
            if (_frameConfig?.Enabled == true)
            {
                ProcessReceiveDataWithFrame(data, displayConfig);
            }
            else
            {
                ProcessReceiveDataDirect(data, displayConfig);
            }
        }
        #region 处理接收数据

        /// <summary>
        /// 直接处理数据（无帧处理）
        /// </summary>
        /// <param name="data">字节数据</param>
        /// <param name="displayConfig">帧处理设置</param>
        private void ProcessReceiveDataDirect(byte[] data, DisplayConfig displayConfig)
        {
            string processedText = EncodeData(data, displayConfig.ReceiveEncoding);

            //添加tx/rx标识
            processedText = FormatWithTxRxPrefix(processedText, displayConfig, true);
            ReceivedDaraProcessed?.Invoke(this,processedText);
        } 

        /// <summary>
        /// 帧处理数据
        /// </summary>
        /// <param name="data">字节数据</param>
        /// <param name="displayConfig">帧处理设置</param>
        private void ProcessReceiveDataWithFrame(byte[] data, DisplayConfig displayConfig)
        {
            byte[] frameHeader = ParseHexString(_frameConfig.Header);
            byte[] frameFooter = ParseHexString(_frameConfig.Footer);

            foreach (byte b in data)
            {
                _frameBuffer.Add(b);

                // 检查帧尾，防止越界，每次检查帧尾长度的数组末尾
                if (_frameBuffer.Count >= frameFooter.Length &&
                    CheckSequence(_frameBuffer, _frameBuffer.Count - frameFooter.Length, frameFooter))
                {
                    // 查找帧头，且帧头帧尾必须要存在并能够放下，最小帧长度就是帧头+帧尾
                    for (int i = 0; i <= _frameBuffer.Count - frameHeader.Length - frameFooter.Length; i++)
                    {
                        if (CheckSequence(_frameBuffer, i, frameHeader))
                        {
                            ExtractAndProcessFrame(i, frameHeader.Length, frameFooter.Length, displayConfig);
                            break;
                        }
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// 处理发送数据，根据是否帧处理来选择是否添加帧头帧尾
        /// </summary>
        /// <param name="data">原始数据</param>
        /// <returns></returns>
        public byte[] ProcessSendData(string sendText, DisplayConfig displayConfig)
        {
            byte[] data=DecodeData(sendText,displayConfig.SendEncoding);
            if (_frameConfig?.Enabled == true)
            {
                byte[] frameHeader = ParseHexString(_frameConfig.Header);
                byte[] frameFooter = ParseHexString(_frameConfig.Footer);
                byte[] frameData = new byte[frameHeader.Length + data.Length+frameFooter.Length];
                //将帧头帧尾添加到数据头尾
                Buffer.BlockCopy(frameHeader,0, frameData, 0,frameHeader.Length);
                Buffer.BlockCopy(data,0, frameData, frameHeader.Length, data.Length);
                Buffer.BlockCopy(frameFooter,0,frameData,frameHeader.Length+data.Length, frameFooter.Length);
                return frameData;
            }
            else
            {
                return data;
            }    
        }

        /// <summary>
        /// 添加TX/RX
        /// </summary>
        /// <param name="text">待格式化字符串</param>
        /// <param name="config">TX/RX显示设置</param>
        /// <param name="isReceived">添加接收标志</param>
        /// <returns></returns>
        public string FormatWithTxRxPrefix(string text, DisplayConfig config, bool isReceived)
        {
            if (config.ShowTxRx)
            {
                string prefix = isReceived ? "[RX]" : "[TX]";
                return $"{prefix} {text}";
            }
            return text;
        }

        #region 编解码字节/字符串
        /// <summary>
        /// 将数据编码为指定格式的字符串
        /// </summary>
        /// <param name="data">字节数据</param>
        /// <param name="encodeingType">编码格式</param>
        /// <returns>字符串</returns>
        public string EncodeData(byte[] data, EncodingType encodeingType)
        {
            return encodeingType switch
            {
                EncodingType.ASCII => Encoding.ASCII.GetString(data),
                EncodingType.UTF8 => Encoding.UTF8.GetString(data),
                EncodingType.GB2312 => Encoding.GetEncoding("GB2312").GetString(data),
                EncodingType.Hex => BitConverter.ToString(data).Replace("-", " "), //将默认的连字符去除
                EncodingType.Decimal => string.Join(" ", data.Select(b => b.ToString())),
                EncodingType.Octal => string.Join(" ", data.Select(b => Convert.ToString(b, 8))),
                EncodingType.Binary => string.Join(" ", data.Select(b => Convert.ToString(b, 2).PadLeft(8, '0'))),
                _ => Encoding.UTF8.GetString(data)
            };
        }

        /// <summary>
        /// 将字符串解码为字节数组
        /// </summary>
        /// <param name="text">格式为不同进制的字符串</param>
        /// <param name="encodeingType">解码格式</param>
        /// <returns>字节数组</returns>
        public byte[] DecodeData(string text, EncodingType encodeingType)
        {
            return encodeingType switch
            {
                EncodingType.ASCII => Encoding.ASCII.GetBytes(text),
                EncodingType.UTF8 => Encoding.UTF8.GetBytes(text),
                EncodingType.GB2312 => Encoding.GetEncoding("GB2312").GetBytes(text),
                EncodingType.Hex => ParseHexString(text),
                EncodingType.Decimal => ParseDecimalString(text),
                EncodingType.Octal => ParseOctalString(text),
                EncodingType.Binary => ParseBinaryString(text),
                _ => Encoding.UTF8.GetBytes(text)
            };
        }

        #endregion

        #region 解析字符串工具
        /// <summary>
        /// 解析十六进制字符串
        /// </summary>
        private byte[] ParseHexString(string hexString)
        {
            hexString = hexString.Replace(" ", "").Replace("-", "");
            if (hexString.Length % 2 != 0) return new byte[0];

            byte[] result = new byte[hexString.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            return result;
        }

        /// <summary>
        /// 解析十进制字符串
        /// </summary>
        private byte[] ParseDecimalString(string decimalString)
        {
            string[] numbers = decimalString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return numbers.Select(n => byte.Parse(n)).ToArray();
        }

        /// <summary>
        /// 解析八进制字符串
        /// </summary>
        private byte[] ParseOctalString(string octalString)
        {
            string[] numbers = octalString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return numbers.Select(n => Convert.ToByte(n, 8)).ToArray();
        }

        /// <summary>
        /// 解析二进制字符串
        /// </summary>
        private byte[] ParseBinaryString(string binaryString)
        {
            string[] numbers = binaryString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return numbers.Select(n => Convert.ToByte(n, 2)).ToArray();
        }
        /// <summary>
        /// 检查字节序列是否匹配
        /// </summary>
        private bool CheckSequence(List<byte> buffer, int startIndex, byte[] sequence)
        {
            for (int i = 0; i < sequence.Length; i++)
            {
                //防止越界访问
                if (startIndex + i >= buffer.Count || buffer[startIndex + i] != sequence[i])
                    return false;
            }
            return true;
        }
        /// <summary>
        /// 提取并处理完整帧
        /// </summary>
        private void ExtractAndProcessFrame(int headerIndex, int headerLength, int footerLength, DisplayConfig config)
        {
            int frameStart = headerIndex + headerLength;
            int frameEnd = _frameBuffer.Count - footerLength;
            int frameLength = frameEnd - frameStart;

            if (frameLength > 0)
            {
                byte[] frameData = new byte[frameLength];

                _frameBuffer.CopyTo(frameStart, frameData, 0, frameLength);

                string displayText = EncodeData(frameData, config.ReceiveEncoding);

                //添加tx/rx标识
                displayText = FormatWithTxRxPrefix(displayText, config, true);
                ReceivedFrameProcessed?.Invoke(this, new ReceivedFrameProcessedEventArgs(frameData, displayText));
            }

            // 移除已处理的数据
            _frameBuffer.RemoveRange(0, frameEnd + footerLength);
        }
        
        #endregion

        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void ClearFrameBuffer()
        {
            _frameBuffer.Clear();
        }
    }

    /// <summary>
    /// 帧处理事件参数
    /// </summary>
    public class ReceivedFrameProcessedEventArgs : EventArgs
    {
        public byte[] FrameData { get; }
        public string DisplayText { get; }

        public ReceivedFrameProcessedEventArgs(byte[] frameData, string displayText)
        {
            FrameData = frameData;
            DisplayText = displayText;
        }
    }
}
