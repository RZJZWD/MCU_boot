﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MCUBoot.DateModels;

namespace MCUBoot.Services.BootService
{
    /// <summary>
    /// Boot服务 - 对外暴露固件操作接口，组合各个组件的配置
    /// </summary>
    public class BootService
    {
        private BootFirmware _bootFirmware;
        private BootTransfer _bootTransfer;
        private BootScheduler _bootScheduler;
        private BootConfig _bootConfig;
        private BootTasks _bootTasks;

        // BootService自己的事件
        public event EventHandler<string> LogMessage;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<FirmwareInfo> FirmwareLoaded;
        public event EventHandler<BootStatus> StatusChanged;
        public event EventHandler<int> ProgressChanged;
        public event EventHandler<CommandQueueProgressEventArgs> CommandProgressChanged;

        public BootService(SerialPortService serialPortService)
        {
            _bootFirmware = new BootFirmware();
            _bootTransfer = new BootTransfer(serialPortService);
            _bootScheduler = new BootScheduler(_bootTransfer);
            _bootTasks = new BootTasks();
            _bootConfig = CreateDefaultConfig();

            // 订阅各个组件的事件
            SubscribeToComponentEvents();
        }

        #region boot服务配置管理
        /// <summary>
        /// 设置boot配置
        /// </summary>
        public void SetBootConfig(BootConfig bootConfig)
        {
            _bootConfig = bootConfig;

            // 更新各个组件的配置
            _bootFirmware.SetFirmwareInfo(_bootConfig.Firmware);
            _bootScheduler.SetTransferConfig(_bootConfig.Transfer);

            //LogMessage?.Invoke(this, "Boot配置已更新");

        }
        /// <summary>
        /// 获取Boot配置
        /// </summary>
        public BootConfig GetBootConfig()
        {
            // 从各个组件同步最新状态到配置
            SyncConfigFromComponents();
            return _bootConfig;
        }

        /// <summary>
        /// 从各个组件同步配置状态
        /// </summary>
        private void SyncConfigFromComponents()
        {
            // 同步固件信息
            _bootConfig.Firmware = _bootFirmware.GetFirmwareInfo();

            // 同步传输配置
            var transferConfig = _bootScheduler.GetTransferConfig();
            if (transferConfig != null)
            {
                _bootConfig.Transfer = transferConfig;
            }
        }
        /// <summary>
        /// 创建默认配置
        /// </summary>
        private BootConfig CreateDefaultConfig()
        {
            return new BootConfig
            {
                Firmware = new FirmwareInfo(),
                Status = BootStatus.Disconnected,
                Transfer = new BootTransferConfig()
            };
        }
        /// <summary>
        /// 更新状态
        /// </summary>
        private void UpdateStatus(BootStatus status)
        {
            if (_bootConfig.Status != status)
            {
                _bootConfig.Status = status;
                StatusChanged?.Invoke(this, status);
                LogMessage?.Invoke(this, $"状态更新: {status}");
            }
        }
        #endregion

        #region 固件操作接口
        /// <summary>
        /// 加载固件文件
        /// </summary>
        public bool LoadFirmware(string filePath, int packetSize, uint appLoadAddr)
        {
            LogMessage?.Invoke(this, $"开始固件加载流程...");

            var result = _bootFirmware.LoadFirmware(filePath, packetSize, appLoadAddr);

            if (result)
            {
                SyncConfigFromComponents(); // 同步最新状态
                var firmwareInfo = GetCurrentFirmwareInfo();
                //通知固件信息事件
                FirmwareLoaded?.Invoke(this, firmwareInfo);
                LogMessage?.Invoke(this, $"固件加载流程完成");
            }

            return result;
        }

        /// <summary>
        /// 清除已加载的固件
        /// </summary>
        public void ClearFirmware()
        {
            LogMessage?.Invoke(this, "清除固件...");
            _bootFirmware.ClearFirmware();
            SyncConfigFromComponents(); // 同步最新状态
            LogMessage?.Invoke(this, "固件清除完成");
        }

        /// <summary>
        /// 验证固件
        /// </summary>
        public bool ValidateFirmware()
        {
            LogMessage?.Invoke(this, "开始固件验证流程...");
            var result = _bootFirmware.ValidateFirmware();
            SyncConfigFromComponents(); // 同步最新状态
            LogMessage?.Invoke(this, $"固件验证流程完成，结果: {(result ? "通过" : "失败")}");
            return result;
        }
        /// <summary>
        /// 获取固件包
        /// </summary>
        /// <param name="packetIndex">固件包索引</param>
        /// <returns></returns>
        public byte[] GetFirmwarePacket(int packetIndex)
        {
            LogMessage?.Invoke(this, $"获取固件包({packetIndex + 1}/{_bootFirmware.GetTotalPackets()})");
            return _bootFirmware.BuildFirmwarePacket(packetIndex);
        }
        #endregion

        #region 调度器接口
        /// <summary>
        /// 添加单个命令到队列
        /// </summary>
        public void AddCommand(BootCommandItem command)
        {
            _bootScheduler.AddCommand(command);
        }
        /// <summary>
        /// 批量添加命令到队列
        /// </summary>
        public void AddCommands(IEnumerable<BootCommandItem> commands)
        {
            _bootScheduler.AddCommands(commands);
        }
        /// <summary>
        /// 清空命令队列
        /// </summary>
        public void ClearCommands()
        {
            _bootScheduler.ClearCommandQueue();
        }     
        /// <summary>
        /// 开始执行命令队列
        /// </summary>
        public async Task<BootCommandResult> StartTransfer(DisplayConfig displayConfig)
        {
            try
            {
                UpdateStatus(BootStatus.Transfer);
                var result = await _bootScheduler.StartAsync(displayConfig);

                if (result.Success)
                {
                    UpdateStatus(BootStatus.Completed);
                }
                else
                {
                    UpdateStatus(BootStatus.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"命令队列执行失败: {ex.Message}");
                UpdateStatus(BootStatus.Error);
                return new BootCommandResult { Success = false, ErrorMessage = ex.Message };
            }
        }
        /// <summary>
        /// 停止传输并清空命令队列
        /// </summary>
        public void StopTransfer()
        {
            _bootScheduler.StopExecution();
        }
        #endregion
  
        #region 信息/配置获取
        /// <summary>
        /// 获取固件信息摘要
        /// </summary>
        public string GetFirmwareSummary()
        {
            return _bootFirmware.GetFirmwareSummary();
        }

        /// <summary>
        /// 获取当前固件信息
        /// </summary>
        public FirmwareInfo GetCurrentFirmwareInfo()
        {
            SyncConfigFromComponents();
            return _bootConfig.Firmware;
        }

        /// <summary>
        /// 检查是否有固件加载
        /// </summary>
        public bool HasFirmwareLoaded()
        {
            return _bootFirmware.HasFirmwareLoaded();
        }

        /// <summary>
        /// 获取传输配置
        /// </summary>
        public BootTransferConfig GetTransferConfig()
        {
            return _bootConfig.Transfer;
        }

        /// <summary>
        /// 获取当前boot服务层状态
        /// </summary>
        public BootStatus GetCurrentStatus()
        {
            return _bootConfig.Status;
        }

        /// <summary>
        /// 获取队列中的命令数量
        /// </summary>
        public int GetCommandCount()
        {
            return _bootScheduler.GetCommandCount();
        }

        #endregion

        #region 事件订阅与转发
        /// <summary>
        /// 订阅各个组件的事件
        /// </summary>
        private void SubscribeToComponentEvents()
        {
            // 固件组件事件
            _bootFirmware.LogMessage += OnBootFirmwareLogMessage;
            _bootFirmware.ErrorOccurred += OnBootFirmwareErrorOccurred;

            //// 传输组件事件
            //_bootTransfer.LogMessage += OnBootTransferLogMessage;
            //_bootTransfer.ErrorOccurred += OnBootTransferErrorOccurred;

            // 调度组件事件
            _bootScheduler.LogMessage += OnBootSchedulerLogMessage;
            _bootScheduler.ErrorOccurred += OnBootSchedulerErrorOccurred;
            _bootScheduler.CommandProgressChanged += OnBootSchedulerCommandProgressChanged;
        }

        private void OnBootFirmwareLogMessage(object sender, string message)
        {
            LogMessage?.Invoke(this, $"[固件服务] {message}");
        }

        private void OnBootFirmwareErrorOccurred(object sender, string errorMessage)
        {
            ErrorOccurred?.Invoke(this, $"[固件服务] {errorMessage}");
            UpdateStatus(BootStatus.Error);
        }

        //private void OnBootTransferLogMessage(object sender, string message)
        //{
        //    LogMessage?.Invoke(this, $"[传输服务] {message}");
        //}

        //private void OnBootTransferErrorOccurred(object sender, string errorMessage)
        //{
        //    ErrorOccurred?.Invoke(this, $"[传输服务] {errorMessage}");
        //    UpdateStatus(BootStatus.Error);
        //}

        private void OnBootSchedulerLogMessage(object sender, string message)
        {
            LogMessage?.Invoke(this, $"[调度服务] {message}");
        }

        private void OnBootSchedulerErrorOccurred(object sender, string errorMessage)
        {
            ErrorOccurred?.Invoke(this, $"[调度服务] {errorMessage}");
            UpdateStatus(BootStatus.Error);
        }

        private void OnBootSchedulerCommandProgressChanged(object sender, CommandQueueProgressEventArgs e)
        {
            CommandProgressChanged?.Invoke(this, e);
            ProgressChanged?.Invoke(this, (int)e.ProgressPercentage);
        }
        #endregion

        #region 添加指定任务
        public void AddEnterBootCommand()
        {
            BootCommandItem cmd = _bootTasks.CreatEnterBootModeCommand();
            AddCommand(cmd);
        }
        #endregion
    }
}