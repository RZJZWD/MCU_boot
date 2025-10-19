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

namespace MCUBoot.Services
{
    /// <summary>
    /// Boot服务 - 负责固件加载和CRC32计算
    /// </summary>
    public class BootService
    {
        private BootConfig _bootConfig;

        //事件定义
        public event EventHandler<string> LogMessage;               //boot日志
        public event EventHandler<string> ErrorOccurred;            //错误处理
        public event EventHandler<FirmwareInfo> FirmwareLoaded;     //固件已加载
        public event EventHandler<BootStatus> StatusChanged;        //boot状态改变
        public event EventHandler<int> ProgressChanged;             //进度条改变

        /// <summary>
        /// 获取当前Boot状态
        /// </summary>
        //public BootStatus CurrentStatus => _currentStatus;

        /// <summary>
        /// 设置boot配置
        /// </summary>
        /// <param name="bootConfig"></param>
        public void SetBootConfig(BootConfig bootConfig)
        {
            _bootConfig = bootConfig;
        }

        #region 固件加载和验证
        /// <summary>
        /// 加载固件文件
        /// </summary>
        /// <param name="filePath">固件绝对路径</param>
        /// <param name="packetSize">分包大小</param>
        /// <returns></returns>
        public bool LoadFirmware(string filePath, int packetSize)
        {
            try
            {
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
                FirmwareInfo firmwareInfo = new FirmwareInfo
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
                    IsValid = true
                };

                //绑定到boot配置的固件选项
                _bootConfig.Firmware = firmwareInfo;

                // 触发事件
                FirmwareLoaded?.Invoke(this, firmwareInfo);
                LogMessage?.Invoke(this, $"固件加载成功: {firmwareInfo.FileName}");
                LogMessage?.Invoke(this, $"文件大小: {firmwareInfo.FileSize} 字节");
                LogMessage?.Invoke(this, $"整体CRC32: {CRC32Utility.FormatCRC32(wholeFileCRC)}");
                LogMessage?.Invoke(this, $"分包大小: {packetSize} 字节");
                LogMessage?.Invoke(this, $"分包数量: {packetCRCs.Count}");

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
            _bootConfig.Firmware = new FirmwareInfo();
            LogMessage?.Invoke(this, "已清除固件文件");
        }

        /// <summary>
        /// 验证固件
        /// </summary>
        /// <returns></returns>
        public bool ValidateFirmware()
        {
            if(_bootConfig.Firmware?.FileData == null || _bootConfig.Firmware.FileData.Length == 0)
            {
                ErrorOccurred?.Invoke(this, "没有可验证的固件文件");
                return false;
            }

            try
            {
                // 重新计算MD5进行验证
                string currentMD5 = CalculateMD5Hash(_bootConfig.Firmware.FileData);
                bool md5Valid = currentMD5 == _bootConfig.Firmware.MD5Hash;

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

        #region 固件上传与打包

        #endregion

        #region CRC32计算与校验

        /// <summary>
        /// 获取指定分包的CRC32值
        /// </summary>
        public uint GetPacketCRC(int packetIndex)
        {
            if (_bootConfig?.Firmware?.PacketCRCs == null)
                return 0;

            return _bootConfig.Firmware.PacketCRCs.TryGetValue(packetIndex,out uint crc) 
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
            if (_bootConfig?.Firmware?.FileData == null)
                return false;

            uint actualCRC = CRC32Utility.CalculateCRC32(_bootConfig.Firmware.FileData);
            return actualCRC == _bootConfig.Firmware.WholeFileCRC;
        }

        #endregion

        #region 数据包相关方法
        /// <summary>
        /// 获取分包数据
        /// </summary>
        public byte[] GetPacketData(int packetIndex)
        {
            if (_bootConfig?.Firmware?.FileData == null)
                return null;

            int startIndex = packetIndex * _bootConfig.Firmware.PacketSize;
            if (startIndex >= _bootConfig.Firmware.FileData.Length)
                return null;

            int length = Math.Min(_bootConfig.Firmware.PacketSize,
                                _bootConfig.Firmware.FileData.Length - startIndex);

            byte[] packetData = new byte[length];
            Array.Copy(_bootConfig.Firmware.FileData, startIndex, packetData, 0, length);

            return packetData;
        }

        /// <summary>
        /// 获取总分包数
        /// </summary>
        public int GetTotalPackets()
        {
            if (_bootConfig?.Firmware?.FileData == null)
                return 0;

            return (int)Math.Ceiling((double)_bootConfig.Firmware.FileData.Length / _bootConfig.Firmware.PacketSize);
        }

        #endregion

        #region boot命令封装与解析

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取固件信息摘要
        /// </summary>
        public string GetFirmwareSummary()
        {
            if (_bootConfig.Firmware?.FileData == null)
                return "未加载固件文件";

            return $"{_bootConfig.Firmware.FileName} ({_bootConfig.Firmware.FileSize} 字节)";
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
        /// 更新Boot状态
        /// </summary>
        //private void UpdateStatus(BootStatus newStatus)
        //{
        //    if (_currentStatus != newStatus)
        //    {
        //        _currentStatus = newStatus;
        //        StatusChanged?.Invoke(this, newStatus);
        //    }
        //}

        #endregion
    }
}