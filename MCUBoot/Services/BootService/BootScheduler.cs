using MCUBoot.DateModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCUBoot.Services.BootService
{
    
    internal class BootScheduler : IDisposable
    {
        private readonly BootTransfer _bootTransfer;
        private readonly Queue<BootCommandItem> _commandQueue;
        private bool _isExecuting;                              //正在执行中
        private bool _stopSchedule = false;                     //是否停止调度
        private string deviceErrorMessage = string.Empty;       //设备错误信息
        private readonly object _lock = new object();

        //事件
        public event EventHandler<string> LogMessage;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<CommandQueueProgressEventArgs> CommandProgressChanged;

        public BootScheduler(BootTransfer bootTransfer)
        {
            _bootTransfer = bootTransfer ?? throw new ArgumentNullException(nameof(bootTransfer));
            _commandQueue = new Queue<BootCommandItem>();

            //订阅传输层事件
            _bootTransfer.LogMessage += OnBootTransferLog;
            _bootTransfer.ErrorOccurred += OnBootTransferError;
            _bootTransfer.DeviceErrorReceived += OnDeviceErrorReceived;
        }

        /// <summary>
        /// 设置传输层配置，用于自定义传输配置
        /// </summary>
        /// <param name="config">配置</param>
        public void SetTransferConfig(BootTransferConfig config)
        {
            _bootTransfer.SetTransferConfig(config);
        }
        /// <summary>
        /// 获取当前传输配置
        /// </summary>
        public BootTransferConfig GetTransferConfig()
        {
            return _bootTransfer.GetTransferConfig();
        }

        #region 命令队列操作
        /// <summary>
        /// 添加单个命令到队列
        /// </summary>
        public void AddCommand(BootCommandItem command)
        {
            lock (_lock)
            {
                _commandQueue.Enqueue(command);
                LogMessage?.Invoke(this, $"命令已加入队列: {command.Description}");
            }
        }

        /// <summary>
        /// 批量添加命令到队列
        /// </summary>
        public void AddCommands(IEnumerable<BootCommandItem> commands)
        {
            lock (_lock)
            {
                foreach (var command in commands)
                {
                    _commandQueue.Enqueue(command);
                    LogMessage?.Invoke(this, $"命令已加入队列: {command.Description}");
                }
            }

        }
        /// <summary>
        /// 清空命令队列
        /// </summary>
        public void ClearCommandQueue()
        {
            lock (_lock)
            {
                _commandQueue.Clear();
                LogMessage?.Invoke(this, "命令队列已清空");
            }
        }

        /// <summary>
        /// 获取队列中的命令数量
        /// </summary>
        public int GetCommandCount()
        {
            lock (_lock)
            {
                return _commandQueue.Count;
            }
        }
        #endregion

        #region 调度器开始/停止
        public async Task<BootCommandResult> StartAsync(DisplayConfig displayConfig)
        {
            _stopSchedule = false;
            if (_isExecuting)
            {
                throw new InvalidOperationException("命令队列正在执行中，请等待完成");
            }

            lock (_lock)
            {
                if (_commandQueue.Count == 0)
                {
                    return new BootCommandResult
                    {
                        Success = true,
                        ExecutedCount = 0,
                        TotalCount = 0
                    };
                }
                //命令队列有命令则执行到这里
                _isExecuting = true;
            }

            var result = new BootCommandResult
            {
                TotalCount = _commandQueue.Count
            };

            try
            {
                var tempTransferConfig = new BootTransferConfig();
                tempTransferConfig.LineEnding = displayConfig.LineEnding;
                // 保存原始配置
                var originalConfig = _bootTransfer.GetTransferConfig();
                var originalTimeout = originalConfig?.Timeout ?? 3000;
                var originalRetryCount = originalConfig?.RetryCount ?? 3;
               
                int currentIndex = 0;

                while (_commandQueue.Count > 0)
                {
                    BootCommandItem commandItem;
                    lock (_lock)
                    {
                        //取出一个任务
                        commandItem = _commandQueue.Dequeue();
                    }

                    currentIndex++;

                    // 报告进度
                    CommandProgressChanged?.Invoke(this, new CommandQueueProgressEventArgs
                    {
                        CurrentIndex = currentIndex,
                        TotalCount = result.TotalCount,
                        CurrentCommand = commandItem,
                        ProgressPercentage = (double)currentIndex / result.TotalCount * 100
                    });

                    // 设置自定义配置（如果存在）
                    if (commandItem.TransferTimeoutMs.HasValue || commandItem.TransferRetryCount.HasValue)
                    {
                        tempTransferConfig.Timeout = commandItem.TransferTimeoutMs ?? originalTimeout;
                        tempTransferConfig.RetryCount = commandItem.TransferRetryCount ?? originalRetryCount;
                        _bootTransfer.SetTransferConfig(tempTransferConfig);
                    }

                    // 执行命令
                    LogMessage?.Invoke(this, $"执行命令 [{currentIndex}/{result.TotalCount}]: {commandItem.Description}");

                    CommandFrame response;
                    if (commandItem.SendData != null && commandItem.SendData.Length > 0)
                    {
                        response = await _bootTransfer.SendCommandAsync(commandItem.SendCommand, commandItem.SendData, commandItem.ResponseCommand);
                    }
                    else
                    {
                        response = await _bootTransfer.SendCommandAsync(commandItem.SendCommand, commandItem.ResponseCommand);
                    }
                    //回应为空的情况，直接返回
                    if (response == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"命令执行失败: {commandItem.Description}，通信中断";

                        // 清空剩余队列
                        lock (_lock)
                        {
                            _commandQueue.Clear();
                        }

                        return result;
                    }

                    // 回应不为空，执行回应处理回调
                    ResponseAction action = ResponseAction.Continue;
                    try
                    {
                        //回调不为空
                        if (commandItem.ResponseHandler != null)
                        {
                            action = commandItem.ResponseHandler(response);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage?.Invoke(this, $"响应处理回调执行异常: {ex.Message}");
                    }
                    result = HandlerResponseAction(result, action, response, commandItem);


                    LogMessage?.Invoke(this, $"命令完成: {commandItem.Description}");
                }

                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"命令队列执行异常: {ex.Message}";
                return result;
            }
            finally
            {
                _isExecuting = false;
            }
        }


        /// <summary>
        /// 停止队列执行（在下次命令执行检查时停止）
        /// </summary>
        public void StopExecution()
        {
            lock (_lock)
            {
                _commandQueue.Clear();
                _isExecuting = false;
                _stopSchedule = true;
            }
            LogMessage?.Invoke(this, "队列已停止执行并清空");
        }

        /// <summary>
        /// 处理回应动作
        /// </summary>
        /// <param name="result">调度结果</param>
        /// <param name="action">回应动作，每个命令项都有回调来处理不同接受命令字的对应动作</param>
        /// <param name="response">回应命令帧</param>
        /// <param name="CommandItem">当前调度的命令项目</param>
        /// <returns></returns>
        private BootCommandResult HandlerResponseAction(BootCommandResult result, ResponseAction action, CommandFrame response, BootCommandItem CommandItem)
        {            
            // 根据回调结果执行相应动作
            switch (action)
            {
                case ResponseAction.Continue:
                    if (response.Command == CommandType.EnterBoot)
                    {
                        DeviceInfo deviceInfo = new();
                        deviceInfo.FromBytes(response.Data);
                        string deviceString = ShowDeviceInfo(deviceInfo);
                        LogMessage?.Invoke(this, deviceString);
                    }
                    result.Success = true;
                    result.Responses.Add(response);
                    result.ExecutedCount++;
                    break;

                case ResponseAction.Retry:
                    // 创建重试命令并添加到队列
                    var retryCommand = CommandItem.CreateRetryCommand(CommandItem);
                    if(retryCommand != null)
                    {
                        lock (_lock)
                        {
                            _commandQueue.Enqueue(retryCommand);
                            result.TotalCount++; // 更新总命令数
                        }
                        LogMessage?.Invoke(this, "重试");
                    }
                    else
                    {
                        LogMessage?.Invoke(this,"达到最大重试次数");
                    }
                    break;

                case ResponseAction.Stop:
                    result.Success = false;
                    result.ErrorMessage = deviceErrorMessage;   //将设备错误信息存到调度结果中
                    result.Responses.Add(response);
                    result.ExecutedCount++;
                    lock (_lock) { _commandQueue.Clear(); }
                    break;

                case ResponseAction.Skip:
                    result.ExecutedCount++; // 计数但不保存响应
                    break;
            }

            return result;
        }

        private string ShowDeviceInfo(DeviceInfo info)
        {
            if (info == null)
                return "设备信息为空";
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(" ");
            sb.AppendLine("=== 设备信息 ===");
            sb.AppendLine($"设备型号: {info.Model}");
            sb.AppendLine($"Flash大小: {info.FlashSize} bytes({FormatFileSize(info.FlashSize)})");
            sb.AppendLine($"应用程序地址: 0x{info.AppAddress:X8}");
            sb.AppendLine($"引导程序版本: {info.BootVersion}");

            return sb.ToString();
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

        #endregion

        #region 事件处理
        private void OnBootTransferLog(object sender, string message)
        {
            LogMessage?.Invoke(this, $"[传输层] {message}");
        }

        private void OnBootTransferError(object sender, string errorMessage)
        {
            ErrorOccurred?.Invoke(this, $"[传输层] {errorMessage}");
        }

        /// <summary>
        /// 设备错误处理
        /// </summary>
        private void OnDeviceErrorReceived(object sender, string errorMessage)
        {
            LogMessage?.Invoke(this, $"[传输层] {errorMessage}");
            //将设备错误信息同步到设备错误信息中
            deviceErrorMessage = errorMessage;
            //StopExecution();
        }

        #endregion

        public void Dispose()
        {
            _bootTransfer.LogMessage -= OnBootTransferLog;
            _bootTransfer.ErrorOccurred -= OnBootTransferError;
        }
    }
    /// <summary>
    /// 命令队列进度事件参数
    /// </summary>
    public class CommandQueueProgressEventArgs : EventArgs
    {
        /// <summary>
        /// 当前命令索引
        /// </summary>
        public int CurrentIndex { get; set; }
        /// <summary>
        /// 总命令数
        /// </summary>
        public int TotalCount { get; set; }
        /// <summary>
        /// 当前命令
        /// </summary>
        public BootCommandItem CurrentCommand { get; set; }
        /// <summary>
        /// 处理进度(%)
        /// </summary>
        public double ProgressPercentage { get; set; }
    }
}
