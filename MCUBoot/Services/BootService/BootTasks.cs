using MCUBoot.DateModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCUBoot.Services.BootService
{
    internal class BootTasks
    {
        /// <summary>
        /// 创建下位机进入Boot命令
        /// </summary>
        /// <returns></returns>
        public BootCommandItem CreatEnterBootModeCommand()
        {
            var enterBootCommand = new BootCommandItem()
            {
                SendCommand = CommandType.EnterBoot,
                ResponseCommand = CommandType.EnterBoot,
                Description = "下位机进入Boot",
                TransferTimeoutMs = 1000,
                TransferRetryCount = 3,
                ResponseHandler = (response) =>
                {
                    if (response.Command == CommandType.EnterBoot)
                    {
                        return ResponseAction.Continue;
                    }
                    if (response.Command == CommandType.Nack)
                    {
                        return ResponseAction.Stop;
                    }
                    if (response.Command == CommandType.ErrorResponse)
                    {
                        return ResponseAction.Stop;
                    }
                    return ResponseAction.Stop;
                }
            };

            return enterBootCommand;
        }
        public BootCommandItem CreatUploadCommand(byte[] firmwareData)
        {
            var uploadCommand = new BootCommandItem
            {
                SendCommand = CommandType.Upload,
                ResponseCommand = CommandType.Ack,
                SendData = firmwareData,
                Description = "发送固件包数据，期望回复Ack，预计回复ack",
                TransferTimeoutMs = 2000,
                TransferRetryCount = 0,
                ScheduleRetryCount = 3,
                ResponseHandler = (response) =>
                {
                    if (response.Command == CommandType.Ack)
                    {
                        return ResponseAction.Continue;
                    }
                    if (response.Command == CommandType.ErrorResponse)
                    {
                        // 验证失败，停止
                        return ResponseAction.Retry;
                    }
                    return ResponseAction.Stop;
                }
            };

            return uploadCommand;       
        }
        public BootCommandItem CreatRunAppCommand()
        {
            var runAppCommand = new BootCommandItem
            {
                SendCommand = CommandType.RunApp,
                ResponseCommand = CommandType.Ack,
                Description = "跳转到应用程序，预计回复Ack",
                TransferTimeoutMs = 1000,
                TransferRetryCount = 0,
                ResponseHandler = (response) =>
                {
                    if (response.Command == CommandType.Ack)
                    {
                        return ResponseAction.Continue;
                    }
                    if (response.Command == CommandType.ErrorResponse)
                    {
                        return ResponseAction.Stop;
                    }
                    return ResponseAction.Stop;
                }
            };

            return runAppCommand;
        }

    }
}
