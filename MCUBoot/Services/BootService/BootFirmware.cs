using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using MCUBoot.DateModels;
using MCUBoot.utils;

namespace MCUBoot.Services.BootService
{
    /// <summary>
    /// Boot服务 - 负责固件加载和CRC32计算
    /// </summary>
    internal class BootFirmware
    {

        private FirmwareInfo _firmwareInfo;

        //事件定义
        public event EventHandler<string> LogMessage;               //boot日志
        public event EventHandler<string> ErrorOccurred;            //错误处理

        /// <summary>
        /// 设置固件信息
        /// </summary>
        /// <param name="info"></param>
        public void SetFirmwareInfo(FirmwareInfo info)
        {
            _firmwareInfo = info;
        }
        /// <summary>
        /// 获取固件信息
        /// </summary>
        /// <returns></returns>
        public FirmwareInfo GetFirmwareInfo()
        {
            return _firmwareInfo;
        }

        #region 固件加载和验证
        /// <summary>
        /// 加载固件文件
        /// </summary>
        /// <param name="filePath">固件绝对路径</param>
        /// <param name="packetSize">分包大小</param>
        /// <param name="appLoadAddr">固件加载目标地址</param>
        /// <returns></returns>
        public bool LoadFirmware(string filePath, int packetSize, uint appLoadAddr)
        {
            try
            {
                LogMessage?.Invoke(this, $"开始加载固件: {filePath}");

                if (string.IsNullOrEmpty(filePath))
                {
                    ErrorOccurred?.Invoke(this, "文件路径不能为空");
                    return false;
                }

                if (!File.Exists(filePath))
                {
                    ErrorOccurred?.Invoke(this, $"固件文件不存在: {filePath}");
                    return false;
                }

                //文件消息
                FileInfo fileInfo = new FileInfo(filePath);

                //验证文件大小
                if(fileInfo.Length == 0)
                {
                    ErrorOccurred?.Invoke(this, "固件文件为空");
                    return false;
                }
                //文件大小超过10MB
                if(fileInfo.Length > 10 * 1024 * 1024)
                {
                    ErrorOccurred?.Invoke(this, $"固件文件过大: {fileInfo.Length} 字节，最大支持10MB");
                    return false;
                }

                //读取文件数据（byte）
                byte[] fileData = File.ReadAllBytes(filePath);

                //计算MD5哈希
                string md5Hash = CalculateMD5Hash(fileData);

                //计算CRC32校验
                uint wholeFileCRC = CRC32Utility.CalculateCRC32(fileData);
                var packetCRCs = CRC32Utility.CalculatePacketCRCs(fileData, packetSize);

                //创建固件消息
                _firmwareInfo = new FirmwareInfo
                {
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    FileSize = fileInfo.Length,
                    FileData = fileData,
                    LastModified = fileInfo.LastWriteTime,
                    MD5Hash = md5Hash,
                    WholeFileCRC = wholeFileCRC,
                    PacketCRCs = packetCRCs,
                    PacketSize = packetSize,
                    IsValid = true,
                    AppLoadAddr = appLoadAddr,
                };

                // 触发事件
                LogMessage?.Invoke(this, $"固件加载成功: {_firmwareInfo.FileName}");
                LogMessage?.Invoke(this, $"文件大小: {FormatFileSize(_firmwareInfo.FileSize)}");
                LogMessage?.Invoke(this, $"整体CRC32: {CRC32Utility.FormatCRC32(wholeFileCRC)}");
                LogMessage?.Invoke(this, $"分包大小: {packetSize} 字节");
                LogMessage?.Invoke(this, $"分包数量: {packetCRCs.Count}");
                LogMessage?.Invoke(this, $"固件加载地址: 0x{_firmwareInfo.AppLoadAddr:X8}");

                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                ErrorOccurred?.Invoke(this, $"没有权限访问文件: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                ErrorOccurred?.Invoke(this, $"文件读写错误: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"加载固件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 清除已加载的固件
        /// </summary>
        public void ClearFirmware()
        {
            _firmwareInfo = new FirmwareInfo();
            LogMessage?.Invoke(this, "已清除固件文件");
        }

        /// <summary>
        /// 验证固件
        /// </summary>
        /// <returns></returns>
        public bool ValidateFirmware()
        {
            if(_firmwareInfo?.FileData == null || _firmwareInfo.FileData.Length == 0)
            {
                ErrorOccurred?.Invoke(this, "没有可验证的固件文件");
                return false;
            }

            try
            {
                // 重新计算MD5进行验证
                string currentMD5 = CalculateMD5Hash(_firmwareInfo.FileData);
                bool md5Valid = currentMD5 == _firmwareInfo.MD5Hash;

                // 验证整体CRC32
                bool crcValid = VerifyWholeFileCRC();

                // 验证所有分包CRC32
                bool allPacketsValid = true;
                int totalPackets = GetTotalPackets();
                int verifiedPackets = 0;

                for (int i = 0; i < totalPackets; i++)
                {
                    if (!VerifyPacketCRC(i))
                    {
                        allPacketsValid = false;
                        break;
                    }
                    verifiedPackets++;
                }

                if (md5Valid && crcValid && allPacketsValid)
                {
                    LogMessage?.Invoke(this, "固件验证通过，文件完整性良好");
                    LogMessage?.Invoke(this, $"MD5验证: 通过");
                    LogMessage?.Invoke(this, $"整体CRC32验证: 通过");
                    LogMessage?.Invoke(this, $"{verifiedPackets}个分包CRC32验证: 全部通过");
                    return true;
                }
                else
                {
                    ErrorOccurred?.Invoke(this, "固件验证失败，文件可能已损坏");
                    if (!md5Valid) LogMessage?.Invoke(this, "MD5验证: 失败");
                    if (!crcValid) LogMessage?.Invoke(this, "整体CRC32验证: 失败");
                    if (!allPacketsValid) LogMessage?.Invoke(this, $"分包CRC32验证: 失败 (已验证 {verifiedPackets}/{totalPackets} 包)");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"固件验证失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        //固件打包
        public byte[] BuildFirmwarePacket(int packetIndex)
        {
            if (_firmwareInfo?.FileData == null)
            {
                ErrorOccurred?.Invoke(this, "没有可打包的固件数据");
                return null;
            }
            try
            {
                int totalPackets = GetTotalPackets();
                if (packetIndex < 0 || packetIndex >= totalPackets) {
                    ErrorOccurred?.Invoke(this, $"无效的包索引: {packetIndex}，有效范围: 0-{totalPackets - 1}");
                    return null;
                }
                byte[] packetdata = GetPacketData(packetIndex);
                if (packetdata == null)
                {
                    ErrorOccurred?.Invoke(this, $"获取分包数据失败: {packetIndex}");
                    return null;
                }
                uint packetCRC = GetPacketCRC(packetIndex);

                byte[] packedData = PackData(packetIndex, totalPackets, packetdata, packetCRC);

                return packedData;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"打包数据包失败 (索引{packetIndex}): {ex.Message}");
                return null;
            } 

        }

        #region CRC32计算与校验

        /// <summary>
        /// 获取指定分包的CRC32值
        /// </summary>
        public uint GetPacketCRC(int packetIndex)
        {
            if (_firmwareInfo?.PacketCRCs == null)
                return 0;

            return _firmwareInfo.PacketCRCs.TryGetValue(packetIndex,out uint crc) 
                ? crc 
                : 0;
        }

        /// <summary>
        /// 验证指定分包的CRC32
        /// </summary>
        public bool VerifyPacketCRC(int packetIndex)
        {
            var packetData = GetPacketData(packetIndex);
            if (packetData == null)
                return false;

            uint expectedCRC = GetPacketCRC(packetIndex);
            uint actualCRC = CRC32Utility.CalculateCRC32(packetData);

            return actualCRC == expectedCRC;
        }

        /// <summary>
        /// 验证整个固件的CRC32
        /// </summary>
        public bool VerifyWholeFileCRC()
        {
            if (_firmwareInfo?.FileData == null)
                return false;

            uint actualCRC = CRC32Utility.CalculateCRC32(_firmwareInfo.FileData);
            return actualCRC == _firmwareInfo.WholeFileCRC;
        }

        #endregion

        #region 数据包相关方法
        /// <summary>
        /// 获取分包数据
        /// </summary>
        public byte[] GetPacketData(int packetIndex)
        {
            if (_firmwareInfo?.FileData == null)
                return null;

            int startIndex = packetIndex * _firmwareInfo.PacketSize;
            if (startIndex >= _firmwareInfo.FileData.Length)
                return null;

            int length = Math.Min(_firmwareInfo.PacketSize,
                                _firmwareInfo.FileData.Length - startIndex);

            byte[] packetData = new byte[length];
            Array.Copy(_firmwareInfo.FileData, startIndex, packetData, 0, length);

            return packetData;
        }

        /// <summary>
        /// 获取总分包数
        /// </summary>
        public int GetTotalPackets()
        {
            if (_firmwareInfo?.FileData == null)
                return 0;

            return (int)Math.Ceiling((double)_firmwareInfo.FileData.Length / _firmwareInfo.PacketSize);
        }
        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取固件信息摘要
        /// </summary>
        public string GetFirmwareSummary()
        {
            if (_firmwareInfo?.FileData == null)
                return "未加载固件文件";

            return $"{_firmwareInfo.FileName} ({_firmwareInfo.FileSize} 字节)";
        }
        /// <summary>
        /// 检查是否有固件加载
        /// </summary>
        public bool HasFirmwareLoaded()
        {
            return _firmwareInfo?.FileData != null && _firmwareInfo.FileData.Length > 0;
        }
        /// <summary>
        /// 计算MD5哈希
        /// </summary>
        private string CalculateMD5Hash(byte[] data)
        {
            byte[] hashBytes = MD5.HashData(data);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size = size / 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
        /// <summary>
        /// 按照指定结构打包：包索引 + 包总数 + 包数据 + CRC32
        /// </summary>
        private byte[] PackData(int packetIndex, int totalPackets, byte[] packetData, uint crc32)
        {
            // 包结构（严格按照要求）：
            // [包索引(4字节)] [包总数(4字节)] [包数据(N字节)] [CRC32(4字节)]

            // 计算总长度
            int totalLength = 4 + 4 + packetData.Length + 4; // 索引4 + 总数4 + 数据N + CRC4

            // 创建结果数组
            byte[] result = new byte[totalLength];
            int offset = 0;

            // 1. 包索引 (4字节)
            byte[] indexBytes = BitConverter.GetBytes(packetIndex);
            Array.Copy(indexBytes, 0, result, offset, 4);
            offset += 4;

            // 2. 包总数 (4字节)
            byte[] totalBytes = BitConverter.GetBytes(totalPackets);
            Array.Copy(totalBytes, 0, result, offset, 4);
            offset += 4;

            // 3. 包数据 (N字节)
            Array.Copy(packetData, 0, result, offset, packetData.Length);
            offset += packetData.Length;

            // 4. CRC32 (4字节)
            byte[] crcBytes = BitConverter.GetBytes(crc32);
            Array.Copy(crcBytes, 0, result, offset, 4);

            return result;
        }

        #endregion
    }
}