using MCUBoot.DateModels;

using MCUBoot.Services;
using System;
using System.Text;

/// <summary>
/// Boot传输服务 - 负责与设备的命令和数据传输
/// </summary>
public class BootTransfer : IDisposable
{
    private readonly SerialPortService _serialPort;
    private BootTransferConfig _transferConfig;
    private readonly CancellationTokenSource _cts;
    private readonly object _responseLock = new object();
    private CommandFrame _lastReceivedFrame;
    private bool _responseReceived;

    // 标准事件定义
    public event EventHandler<string> LogMessage;               // boot日志
    public event EventHandler<string> ErrorOccurred;            // 错误处理
    public event EventHandler<string> DeviceErrorReceived;      // 设备错误
    public event EventHandler<CommandFrame> ResponseReceived;   // 响应接收

    public BootTransfer(SerialPortService serialPort)
    {
        _serialPort = serialPort;
        _cts = new CancellationTokenSource();
        _serialPort.DataReceived += OnDataReceived;
        _serialPort.ErrorOccurred += OnSerialError;
    }

    /// <summary>
    /// 设置传输配置
    /// </summary>
    public void SetTransferConfig(BootTransferConfig config)
    {
        _transferConfig = config;
    }
    /// <summary>
    /// 获取传输配置
    /// </summary>
    public BootTransferConfig GetTransferConfig()
    {
        return _transferConfig;
    }

    /// <summary>
    /// 发送命令帧并等待特定回应
    /// </summary>
    /// <param name="sendFrame">发送的命令帧</param>
    /// <param name="expectedCommand">期望收到的命令类型</param>
    /// <returns>实际接收到的命令帧</returns>
    public async Task<CommandFrame> SendCommandAsync(CommandFrame sendFrame, CommandType expectedCommand)
    {
        if (sendFrame == null)
        {
            ErrorOccurred?.Invoke(this, "命令帧为空");
            return null;
        }

        if (!_serialPort.IsOpen)
        {
            ErrorOccurred?.Invoke(this, "串口未打开，无法发送命令");
            return null;
        }

        int timeoutMs = _transferConfig?.Timeout ?? 3000;
        int retryCount = _transferConfig?.RetryCount ?? 3;

        for (int attempt = 0; attempt < retryCount; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    LogMessage?.Invoke(this, $"第{attempt + 1}次重试命令: {sendFrame.Command}");
                }
                else
                {
                    LogMessage?.Invoke(this, $"发送命令: {sendFrame.Command}");
                }

                // 重置响应状态
                ResetResponseState();

                // 发送命令
                var data = sendFrame.ToBytes();
                byte[] lineEndingBytes = Encoding.UTF8.GetBytes(_transferConfig.LineEnding);    //添加尾行
                byte[] combined = data.Concat(lineEndingBytes).ToArray();
                _serialPort.SendData(combined);


                // 等待期望的响应
                var response = await WaitForExpectedResponseAsync(timeoutMs, expectedCommand);
                if (response != null)
                {
                    // 检查是否是错误响应
                    if (response.Command == CommandType.ErrorResponse)
                    {
                        string errorMsg = ParseErrorMessage(response);
                        //LogMessage?.Invoke(this, $"收到设备错误: {errorMsg}");
                        DeviceErrorReceived?.Invoke(this, $"设备错误: {errorMsg}");
                    }
                    else
                    {
                        // 正常响应
                        LogMessage?.Invoke(this, $"收到期望响应: {response.Command}");
                    }      
                    return response;
                }
            }
            catch (TimeoutException)
            {
                if (attempt == retryCount - 1)
                {
                    ErrorOccurred?.Invoke(this, $"命令 {sendFrame.Command} 响应超时，已重试{retryCount}次");
                    throw;
                }
                LogMessage?.Invoke(this, $"第{attempt + 1}次尝试超时，准备重试");
            }
            catch (Exception ex)
            {
                if (attempt == retryCount - 1)
                {
                    ErrorOccurred?.Invoke(this, $"发送命令失败: {ex.Message}");
                    throw;
                }
                LogMessage?.Invoke(this, $"第{attempt + 1}次尝试失败: {ex.Message}，准备重试");
            }

            // 重试前等待
            if (attempt < retryCount - 1)
            {
                await Task.Delay(100);
            }
        }

        return null;
    }

    /// <summary>
    /// 发送简单命令并等待特定回应
    /// </summary>
    public async Task<CommandFrame> SendCommandAsync(CommandType sendCommand, CommandType expectedCommand)
    {
        var frame = new CommandFrame { Command = sendCommand };
        return await SendCommandAsync(frame, expectedCommand);
    }

    /// <summary>
    /// 发送带数据的命令并等待特定回应
    /// </summary>
    public async Task<CommandFrame> SendCommandAsync(CommandType sendCommand, byte[] data, CommandType expectedCommand)
    {
        var frame = new CommandFrame { Command = sendCommand, Data = data };
        return await SendCommandAsync(frame, expectedCommand);
    }

    /// <summary>
    /// 重置响应状态
    /// </summary>
    private void ResetResponseState()
    {
        lock (_responseLock)
        {
            _lastReceivedFrame = null;
            _responseReceived = false;
        }
    }

    /// <summary>
    /// 等待期望的响应
    /// </summary>
    private async Task<CommandFrame> WaitForExpectedResponseAsync(int timeoutMs, CommandType expectedCommand)
    {
        var startTime = DateTime.Now;

        while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
        {
            lock (_responseLock)
            {
                if (_responseReceived && _lastReceivedFrame != null)
                {
                    // 检查是否是我们期望的命令类型
                    if (_lastReceivedFrame.Command == expectedCommand || _lastReceivedFrame.Command == CommandType.ErrorResponse)
                    {
                        var response = _lastReceivedFrame;
                        ResetResponseState(); // 重置状态以便下次使用
                        return response;
                    }
                    else
                    {
                        // 收到非期望的响应，记录下来但继续等待
                        LogMessage?.Invoke(this, $"收到非期望响应: {_lastReceivedFrame.Command}，期望: {expectedCommand}");
                        ResetResponseState(); // 重置状态继续等待期望的响应
                    }
                }
            }

            await Task.Delay(10); // 短暂等待
        }

        throw new TimeoutException($"等待期望响应 {expectedCommand} 超时: {timeoutMs}ms");
    }

    private void OnDataReceived(object sender, DataReceivedEventArgs e)
    {
        try
        {

            var frame = CommandFrame.FromBytes(e.Data);
            if (frame != null)
            {
                lock (_responseLock)
                {
                    _lastReceivedFrame = frame;
                    _responseReceived = true;
                }

                ResponseReceived?.Invoke(this, frame);
                LogMessage?.Invoke(this, $"收到响应: {frame.Command}");
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"解析响应失败: {ex.Message}");
        }
    }

    private void OnSerialError(object sender, string errorMessage)
    {
        ErrorOccurred?.Invoke(this, $"[串口错误] {errorMessage}");
    }

    /// <summary>
    /// 解析错误信息
    /// </summary>
    private string ParseErrorMessage(CommandFrame errorFrame)
    {
        if (errorFrame.Data == null || errorFrame.Data.Length == 0)
            return "未知错误信息";

        try
        {
            return Encoding.UTF8.GetString(errorFrame.Data);
        }
        catch
        {
            return "错误信息无法解析";
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        if (_serialPort != null)
        {
            _serialPort.DataReceived -= OnDataReceived;
            _serialPort.ErrorOccurred -= OnSerialError;
        }
    }
}