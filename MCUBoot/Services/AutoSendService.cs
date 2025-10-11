using MCUBoot.DateModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MCUBoot.Services
{
    /// <summary>
    /// 自动发送服务，负责定时发送
    /// </summary>
    public class AutoSendService
    {
        //创建定时器
        private DispatcherTimer _timer;

        //定义自动发送触发事件
        public event EventHandler AutoSendTriggered;
        public event EventHandler<string> ErrorOccurred;

        private AutoSendConfig _autoSendConfig;
        //运行状态检查
        

        public AutoSendService() 
        {
            _timer = new DispatcherTimer();
            _timer.Tick += OnTimer_Tick;
        }
        /// <summary>
        /// 启动定时器
        /// </summary>
        /// <param name="intervalMs">时间间隔，单位ms</param>
        /// <exception cref="ArgumentException">时间间隔设置错误</exception>
        public void Start(AutoSendConfig config)
        {
            try
            {
                if (config.Interval <= 0)
                {
                    throw new ArgumentException("间隔时间必须大于0");

                }
                _timer.Interval = TimeSpan.FromMilliseconds(config.Interval);
                _timer.Start();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"间隔设置错误 {ex.Message}");
            }


        }
        /// <summary>
        /// 停止自动发送
        /// </summary>
        public void Stop()
        {
            _timer?.Stop();
        }
        public void SetAutoSendConfig(AutoSendConfig config)
        {
            if (config == null) return;

            _autoSendConfig = config;
        }

        /// <summary>
        /// 定时器触发事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTimer_Tick(object? sender, EventArgs e)
        {
            AutoSendTriggered?.Invoke(this,EventArgs.Empty);
        }
    }
}
