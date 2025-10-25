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
            EnterBoot.ExpectedResponse = CommandType.EnterBoot;

            return EnterBoot;
        }
        public BootCommandItem CreatRunAppCommand()
        {
            BootCommandItem RunApp = new BootCommandItem();
            RunApp.Description = "下位机运行app";
            RunApp.SendCommand = CommandType.RunApp;
            RunApp.ExpectedResponse = CommandType.RunApp;

            return RunApp;
        }
    }
}
