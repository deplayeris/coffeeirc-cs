/*MIT License

Copyright (c) 2026 Deplayer

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace io.github.deplayeris.coffeeirc.server;

/// <summary>
/// 服务器信息呈现管理器
/// </summary>
public class ShowManager
{
    private readonly string _interfaceFilePath;

    public enum ShowItem
    {
        ShowMessage,
        ShowTip,
        ShowError,
        ShowWarning,
        ShowInfo,
        ShowDebug
    }

    public ShowManager(string filePath)
    {
        _interfaceFilePath = filePath;
        if (!File.Exists(_interfaceFilePath))
        {
            File.Create(_interfaceFilePath).Dispose();
        }
    }

    public void ShowThis(ShowItem item, string message)
    {
        string prefix = item switch
        {
            ShowItem.ShowMessage => "[MSG]",
            ShowItem.ShowTip => "[TIP]",
            ShowItem.ShowError => "[ERR]",
            ShowItem.ShowWarning => "[WAN]",
            ShowItem.ShowInfo => "[INF]",
            ShowItem.ShowDebug => "[DBG]",
            _ => "[INF]"
        };

        File.WriteAllText(_interfaceFilePath, prefix + message);
    }
}
