using System;
using System.IO;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MCUBoot.Services
{   
    /// <summary>
    /// 文件操作服务，负责将接收数据的保存与文件选择
    /// </summary>
    public class FileService
    {
        /// <summary>
        /// 选择保存文件路径
        /// </summary>
        /// <param name="defaultFileName">默认文件名</param>
        /// <returns></returns>
        public string SelectSaveFilePath(string defaultFileName = "serial_data.txt")
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                FileName = defaultFileName
            };

            return saveFileDialog.ShowDialog() == true ? saveFileDialog.FileName : null;
        }
        /// <summary>
        /// 保存文本到文件
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        public bool SaveText2File(string text, string filePath)
        {
            try
            {
                File.WriteAllText(filePath, text);
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
        public string ReadTextFromFile(string filePath)
        {
            try
            {
                return File.ReadAllText(filePath);
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
