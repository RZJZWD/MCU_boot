using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCUBoot.utils
{
    /// <summary>
    /// CRC32计算工具类（以太网标准多项式：0x4C11DB7）
    /// </summary>
    public static class CRC32Utility
    {
        private static readonly uint[] Table;
        private const uint Polynomial = 0x04C11DB7; // 适用stm32硬件CRC

        static CRC32Utility()
        {
            Table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) == 1)
                        crc = (crc >> 1) ^ Polynomial;
                    else
                        crc >>= 1;
                }
                Table[i] = crc;
            }
        }

        /// <summary>
        /// 计算数据的CRC32校验值
        /// </summary>
        /// <param name="data">要计算的数据</param>
        /// <returns>CRC32校验值</returns>
        public static uint CalculateCRC32(byte[] data)
        {
            return CalculateCRC32(data, 0, data?.Length ?? 0);
        }

        /// <summary>
        /// 计算数据指定范围的CRC32校验值
        /// </summary>
        /// <param name="data">要计算的数据</param>
        /// <param name="offset">起始偏移量</param>
        /// <param name="count">数据长度</param>
        /// <returns>CRC32校验值</returns>
        public static uint CalculateCRC32(byte[] data, int offset, int count)
        {
            if (data == null || data.Length == 0)
                return 0;

            uint crc = 0xFFFFFFFF;
            for (int i = offset; i < offset + count; i++)
            {
                byte index = (byte)((crc ^ data[i]) & 0xFF);
                crc = (crc >> 8) ^ Table[index];
            }
            return crc ^ 0xFFFFFFFF;
        }

        /// <summary>
        /// 计算分包CRC32
        /// </summary>
        /// <param name="data">整个固件数据</param>
        /// <param name="packetSize">分包大小</param>
        /// <returns>分包CRC32字典，key为包序号，value为CRC32值</returns>
        public static Dictionary<int, uint> CalculatePacketCRCs(byte[] data, int packetSize)
        {
            var packetCRCs = new Dictionary<int, uint>();

            if (data == null || data.Length == 0)
                return packetCRCs;

            int totalPackets = (int)Math.Ceiling((double)data.Length / packetSize);

            for (int packetIndex = 0; packetIndex < totalPackets; packetIndex++)
            {
                int startIndex = packetIndex * packetSize;
                int length = Math.Min(packetSize, data.Length - startIndex);
                uint packetCRC = CalculateCRC32(data, startIndex, length);
                packetCRCs[packetIndex] = packetCRC;
            }

            return packetCRCs;
        }

        /// <summary>
        /// 验证数据的CRC32
        /// </summary>
        /// <param name="data">要验证的数据</param>
        /// <param name="expectedCRC">期望的CRC32值</param>
        /// <returns>验证结果</returns>
        public static bool VerifyCRC32(byte[] data, uint expectedCRC)
        {
            if (data == null) return false;
            uint actualCRC = CalculateCRC32(data);
            return actualCRC == expectedCRC;
        }

        /// <summary>
        /// 验证指定数据范围的CRC32
        /// </summary>
        /// <param name="data">要验证的数据</param>
        /// <param name="offset">起始偏移量</param>
        /// <param name="count">数据长度</param>
        /// <param name="expectedCRC">期望的CRC32值</param>
        /// <returns>验证结果</returns>
        public static bool VerifyCRC32(byte[] data, int offset, int count, uint expectedCRC)
        {
            if (data == null) return false;
            uint actualCRC = CalculateCRC32(data, offset, count);
            return actualCRC == expectedCRC;
        }

        /// <summary>
        /// 将CRC32值格式化为十六进制字符串
        /// </summary>
        /// <param name="crc">CRC32值</param>
        /// <returns>格式化的十六进制字符串</returns>
        public static string FormatCRC32(uint crc)
        {
            return $"0x{crc:X8}";
        }
    }
}