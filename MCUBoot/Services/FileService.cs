using MCUBoot.DateModels;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MCUBoot.Services
{   
    /// <summary>
    /// 文件操作服务，负责将接收数据的保存与文件选择
    /// </summary>
    public class FileService
    {

        private FileConfig _fileConfig;

        /// <summary>
        /// 设置串口文件服务配置
        /// </summary>
        /// <param name="config">文件配置</param>
        public void SetFileConfig(FileConfig config)
        {
            if (config == null) return;
            _fileConfig = config;
        }
        /// <summary>
        /// 选择保存文件路径
        /// </summary>
        /// <param name="defaultFileName">默认文件名</param>
        /// <returns></returns>
        public string SelectSaveFilePath(FileConfig config)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                FileName = config.FileName
            };

            return saveFileDialog.ShowDialog() == true ? saveFileDialog.FileName : null;
        }
        /// <summary>
        /// 保存文本到文件
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        public bool SaveText2File(string text, FileConfig config)
        {
            try
            {
                string fullPath = Path.Combine(config.FilePath, config.FileName);

                // 确保目录存在
                string directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fullPath, text);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存文件失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        /// <summary>
        /// 从文件读取文本
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文本</returns>
        public string ReadTextFromFile(FileConfig config)
        {
            try
            {
                return File.ReadAllText(config.FilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取文件失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }
}
