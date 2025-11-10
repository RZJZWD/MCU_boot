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
            BootCommandItem EnterBoot = new BootCommandItem();
            EnterBoot.Description = "下位机进入Boot";
            EnterBoot.SendCommand = CommandType.EnterBoot;
            EnterBoot.ResponseCommand = CommandType.EnterBoot;
            EnterBoot.TransferTimeoutMs = 1000;
            EnterBoot.TransferRetryCount = 3;
            EnterBoot.ResponseHandler = (response) =>
            {
                if (response.Command == CommandType.EnterBoot)
                {
                    return ResponseAction.Continue;
                }
                if (response.Command == CommandType.Nack)
                {
                    return ResponseAction.Stop;
                }
                if(response.Command == CommandType.ErrorResponse)
                {
                    return ResponseAction.Stop;
                }
                return ResponseAction.Stop;
            };
            return EnterBoot;
        }
        public BootCommandItem CreatRunAppCommand()
        {
            BootCommandItem RunApp = new BootCommandItem();
            RunApp.Description = "下位机运行app";
            RunApp.SendCommand = CommandType.RunApp;
            RunApp.ResponseCommand = CommandType.Ack;
            RunApp.TransferTimeoutMs = 1000;
            RunApp.TransferRetryCount = 3;
            RunApp.ResponseHandler = (response) =>
            {
                if (response.Command == CommandType.Ack)
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
            };
            return RunApp;
        }

    }
}
