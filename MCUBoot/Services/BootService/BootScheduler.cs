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
        private bool _stopSchedule = false;                       //是否停止调度
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
                    if (commandItem.TimeoutMs.HasValue || commandItem.RetryCount.HasValue)
                    {
                        tempTransferConfig.Timeout = commandItem.TimeoutMs ?? originalTimeout;
                        tempTransferConfig.RetryCount = commandItem.RetryCount ?? originalRetryCount;
                        _bootTransfer.SetTransferConfig(tempTransferConfig);
                    }

                    // 执行命令
                    LogMessage?.Invoke(this, $"执行命令 [{currentIndex}/{result.TotalCount}]: {commandItem.Description}");

                    CommandFrame response;
                    if (commandItem.SendData != null && commandItem.SendData.Length > 0)
                    {
                        response = await _bootTransfer.SendCommandAsync(commandItem.SendCommand, commandItem.SendData, commandItem.ExpectedResponse);
                    }
                    else
                    {
                        response = await _bootTransfer.SendCommandAsync(commandItem.SendCommand, commandItem.ExpectedResponse);
                    }
                    // 如果收到错误响应，传输层已经通过事件处理了停止逻辑
                    // 我们只需检查是否需要退出循环
                    if (_stopSchedule)
                    {
                        result.Success = false;
                        result.ErrorMessage = deviceErrorMessage;
                        result.Responses.Add(response);
                        result.ExecutedCount++;
                        break;
                    }
                    if (response == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"命令执行失败: {commandItem.Description}";

                        // 清空剩余队列
                        lock (_lock)
                        {
                            _commandQueue.Clear();
                        }

                        return result;
                    }

                    // 保存响应
                    result.Success = true;
                    result.Responses.Add(response);
                    result.ExecutedCount++;

                    //// 执行响应处理回调
                    //try
                    //{
                    //    commandItem.ResponseHandler?.Invoke(response);
                    //}
                    //catch (Exception ex)
                    //{
                    //    LogMessage?.Invoke(this, $"响应处理回调执行异常: {ex.Message}");
                    //}

                    //LogMessage?.Invoke(this, $"命令完成: {commandItem.Description}");
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
            LogMessage?.Invoke(this, "命令调度器停止运行");
            //将设备错误信息同步到类中
            deviceErrorMessage = errorMessage;
            StopExecution();
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
