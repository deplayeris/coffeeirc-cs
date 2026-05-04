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

using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace io.github.deplayeris.coffeeirc.server;

/// <summary>
/// CIC 服务器核心
/// </summary>
public partial class Server
{
    private static ILogger? sml = null;
    private HttpListener? _httpServer;
    private readonly int _ipProtocol;
    private readonly int _port;
    private readonly string _serverInstanceName;
    private readonly string _serverDescription;
    private readonly string _distributionName;
    private readonly ShowManager _showManager;
    private bool _isRunning = false;
    private bool _isDisposed = false;
    private int _clientIdCounter = 0;
    
    private readonly Dictionary<string, ClientInfo> _connectedClients = new();
    private RSA? _serverRsaKeyPair;
    private byte[]? _serverAesKey;
    private StreamWriter? _chatLogWriter;
    private string? _currentChatLogDate;

    /// <summary>
    /// 客户端信息类
    /// </summary>
    public class ClientInfo
    {
        public string ClientId { get; set; } = "";
        public string Username { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public DateTime ConnectTime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public byte[]? ClientPublicKey { get; set; }
        public byte[]? AesKey { get; set; }
        public bool IsEncryptionEnabled { get; set; }

        public string GetFormattedConnectTime() => ConnectTime.ToString("yyyy-MM-dd HH:mm:ss");
        public string GetFormattedLastActivityTime() => LastActivityTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// 服务器构造函数
    /// </summary>
    public Server(int ipProtocol, int port, string serverInstanceName, string serverDescription, string distributionName, string customKey = "", string showFilePath = ".show")
    {
        _ipProtocol = ipProtocol;
        _port = port;
        _serverInstanceName = serverInstanceName;
        _serverDescription = serverDescription;
        _distributionName = distributionName;
        _showManager = new ShowManager(showFilePath);
        InitializeLogger();
        InitializeServerEncryption(customKey);
        sml?.LogInformation("已创建服务器实例");
    }

    private void InitializeLogger()
    {
        var factory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        sml = factory.CreateLogger<Server>();
    }

    /// <summary>
    /// 启动服务器
    /// </summary>
    public void StartServer()
    {
        sml?.LogInformation("[服务器初始化] 开始初始化HTTP服务器...");
        InitializeChatLog();
        
        _httpServer = new HttpListener();
        _httpServer.Prefixes.Add($"http://+:{_port}/");
        _httpServer.Start();
        _isRunning = true;

        sml?.LogInformation("[路由注册] 注册HTTP路由处理器...");
        Task.Run(() => HandleRequests());

        sml?.LogInformation("---------------------------------------------------------------------------------");
        sml?.LogInformation("[服务器启动成功] IRC服务器已成功启动");
        sml?.LogInformation($"[监听信息] 服务器正在监听 IPv{_ipProtocol}:{_port}");
        sml?.LogInformation($"[实例信息] 服务器实例名称: {_serverInstanceName}");
        sml?.LogInformation($"[实例信息] 服务器描述: {_serverDescription}");
        sml?.LogInformation("");
        sml?.LogInformation("[核心信息]正在使用的CoffeeIRC核心的软件信息:");
        sml?.LogInformation($"        版本号: {SwInfos.Version}");
        sml?.LogInformation($"        开发状态: {SwInfos.SoftwareStatus}");
        sml?.LogInformation($"        版本代号: {SwInfos.VerCodename}");
        sml?.LogInformation($"        支持协议: {SwInfos.Connection}");
        sml?.LogInformation("");
        sml?.LogInformation($"当前运行本核心的发行版: {_distributionName}");
        sml?.LogInformation("");
        sml?.LogInformation("如果遇到核心问题，请提交至: https://github.com/deplayeris/coffeeirc/issues");
        sml?.LogInformation("如在使用基于本核心的发行版(如无忧聊)时出现问题");
        sml?.LogInformation("请先检查是否为核心故障(通过查看核心日志)，若非核心问题请联系发行版作者");
        sml?.LogInformation("");
        sml?.LogInformation("核心问题提交步骤:");
        sml?.LogInformation("1. 在GitHub上创建新的Issue");
        sml?.LogInformation("2. 详细准确地描述遇到的问题");
        sml?.LogInformation("3. 附上出现问题时的核心日志文件");
        sml?.LogInformation("---------------------------------------------------------------------------------");
        sml?.LogInformation("[服务器就绪] 服务器已完全就绪，正在等待客户端连接...");
        _showManager.ShowThis(ShowManager.ShowItem.ShowInfo, "服务器已就绪");
    }

    private async Task HandleRequests()
    {
        while (_isRunning && _httpServer != null)
        {
            try
            {
                var context = await _httpServer.GetContextAsync();
                var path = context.Request.Url?.AbsolutePath;

                if (path == "/connect" && context.Request.HttpMethod == "POST") await HandleConnect(context);
                else if (path == "/message" && context.Request.HttpMethod == "POST") await HandleMessage(context);
                else if (path == "/disconnect" && context.Request.HttpMethod == "POST") await HandleDisconnect(context);
                else if (path == "/broadcast" && context.Request.HttpMethod == "POST") await HandleBroadcast(context);
                else
                {
                    context.Response.StatusCode = 405;
                    context.Response.Close();
                }
            }
            catch (Exception ex)
            {
                if (_isRunning) sml?.LogError($"[请求错误] {ex.Message}");
            }
        }
    }

    private async Task HandleConnect(HttpListenerContext context)
    {
        var body = await new StreamReader(context.Request.InputStream).ReadToEndAsync();
        var username = ExtractJsonField(body, "username") ?? "未知用户";
        var clientPublicKeyStr = ExtractJsonField(body, "publicKey");
        var clientId = $"client_{++_clientIdCounter}";
        var clientIp = context.Request.RemoteEndPoint.Address.ToString();

        var clientInfo = new ClientInfo 
        { 
            ClientId = clientId, 
            Username = username, 
            IpAddress = clientIp,
            ConnectTime = DateTime.Now,
            LastActivityTime = DateTime.Now
        };
        
        lock (_connectedClients) _connectedClients[clientId] = clientInfo;

        sml?.LogInformation($"[连接请求处理] 处理来自用户 '{username}' 的连接请求");
        sml?.LogInformation($"[客户端识别] 分配客户端ID: {clientId}");
        sml?.LogInformation($"[网络信息] 客户端IP地址: {clientIp}");

        string? encryptedAesKeyStr = null;
        bool encryptionSuccess = false;

        if (!string.IsNullOrEmpty(clientPublicKeyStr))
        {
            try
            {
                var clientPubKeyBytes = Convert.FromBase64String(clientPublicKeyStr);
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(clientPubKeyBytes, out _);
                
                encryptedAesKeyStr = EncryptAesKey(rsa);
                if (encryptedAesKeyStr != null)
                {
                    clientInfo.AesKey = _serverAesKey;
                    clientInfo.IsEncryptionEnabled = true;
                    encryptionSuccess = true;
                    sml?.LogInformation($"[加密握手] 与客户端 '{username}' 的加密握手成功");
                }
            }
            catch (Exception e)
            {
                sml?.LogWarning($"[加密握手失败] 与客户端 '{username}' 的加密握手失败: {e.Message}");
            }
        }

        var serverPublicKeyStr = Convert.ToBase64String(_serverRsaKeyPair!.ExportSubjectPublicKeyInfo());

        sml?.LogInformation($"[连接建立] 用户 '{username}' (ID: {clientId}) 成功连接到服务器");
        sml?.LogInformation($"[时间戳记] 连接建立时间: {clientInfo.GetFormattedConnectTime()}");
        sml?.LogInformation($"[统计信息] 当前在线用户总数: {_connectedClients.Count}");
        sml?.LogInformation($"[加密状态] 加密通讯: {(encryptionSuccess ? "已启用" : "未启用")}");

        var responseObj = new Dictionary<string, object?>
        {
            ["status"] = "success",
            ["message"] = "连接成功",
            ["clientId"] = clientId,
            ["serverPublicKey"] = serverPublicKeyStr
        };
        if (encryptedAesKeyStr != null) responseObj["encryptedAesKey"] = encryptedAesKeyStr;

        await SendJsonResponse(context, 200, responseObj);
        sml?.LogInformation($"[响应完成] 已向用户 '{username}' (ID: {clientId}) 发送连接并且成功确认");
    }

    private async Task HandleMessage(HttpListenerContext context)
    {
        var body = await new StreamReader(context.Request.InputStream).ReadToEndAsync();
        var encryptedMessage = ExtractJsonField(body, "message") ?? "无内容";
        var clientId = ExtractJsonField(body, "clientId") ?? "unknown";
        
        ClientInfo? clientInfo = null;
        string username = "未知用户";
        string message = encryptedMessage;

        lock (_connectedClients)
        {
            if (_connectedClients.TryGetValue(clientId, out var info))
            {
                clientInfo = info;
                username = info.Username;
                info.LastActivityTime = DateTime.Now;
                message = DecryptClientMessage(encryptedMessage, info);
            }
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        sml?.LogInformation("[消息处理] 处理来自客户端的消息请求");
        sml?.LogInformation($"[时间标记] 消息接收时间: {timestamp}");
        sml?.LogInformation($"[用户验证] 已验证用户身份: '{username}' (ID: {clientId})");
        sml?.LogInformation($"[内容审查] 接收到的消息内容: {message}");
        sml?.LogInformation($"[会话更新] 已更新用户 '{username}' 的最后活动时间");

        LogChatMessage(username, message);

        if (message.StartsWith("/"))
        {
            sml?.LogInformation($"[命令识别] 检测到特殊命令输入: {message}");
            if (message == "/users")
            {
                sml?.LogInformation($"[用户查询] 用户 '{username}' 请求查看在线用户列表");
                sml?.LogInformation($"[统计查询] 当前在线用户总数: {_connectedClients.Count}");
                sml?.LogInformation("[列表详情] 详细在线用户信息:");
                lock (_connectedClients)
                {
                    foreach (var info in _connectedClients.Values)
                    {
                        sml?.LogInformation($"[用户信息] 用户名: '{info.Username}', 客户端ID: {info.ClientId}, 连接时间: {info.GetFormattedConnectTime()}, 最后活动: {info.GetFormattedLastActivityTime()}");
                    }
                }
            }
            else if (message == "/help")
            {
                sml?.LogInformation($"[帮助请求] 用户 '{username}' 请求帮助文档");
            }
            else
            {
                sml?.LogInformation($"[未知命令] 用户 '{username}' 输入了未识别的命令: {message}");
            }
        }

        await SendJsonResponse(context, 200, new { status = "success", message = "消息接收成功", timestamp });
        sml?.LogInformation($"[响应完成] 已向用户 '{username}' (ID: {clientId}) 发送消息接收确认");
        sml?.LogInformation("[处理结束] 消息处理流程已完成");
    }

    private async Task HandleDisconnect(HttpListenerContext context)
    {
        var body = await new StreamReader(context.Request.InputStream).ReadToEndAsync();
        var clientId = ExtractJsonField(body, "clientId") ?? "unknown";
        string username = "未知用户";

        lock (_connectedClients)
        {
            if (_connectedClients.Remove(clientId, out var info))
            {
                username = info.Username;
                var onlineSeconds = (long)(DateTime.Now - info.ConnectTime).TotalSeconds;
                var hours = onlineSeconds / 3600;
                var minutes = (onlineSeconds % 3600) / 60;
                var seconds = onlineSeconds % 60;
                var onlineDuration = $"{hours:D2}:{minutes:D2}:{seconds:D2}";

                sml?.LogInformation($"[断开请求] 处理用户 '{username}' (ID: {clientId}) 的断开连接请求");
                sml?.LogInformation($"[会话信息] 用户连接建立时间: {info.GetFormattedConnectTime()}");
                sml?.LogInformation($"[会话时长] 用户总在线时长: {onlineDuration}");
                sml?.LogInformation($"[最后活动] 用户最后活跃时间: {info.GetFormattedLastActivityTime()}");
                sml?.LogInformation($"[会话终止] 已从活跃用户列表中移除用户 '{username}' (ID: {clientId})");
                sml?.LogInformation($"[状态更新] 断开后当前在线用户数: {_connectedClients.Count}");
            }
            else
            {
                sml?.LogWarning($"[断开异常] 检测到断开请求中的客户端ID不存在 尝试断开不存在的客户端连接");
                sml?.LogInformation($"[无效ID] 客户端ID: {clientId}");
                sml?.LogInformation($"//当前在线用户数仍为{_connectedClients.Count}，可能是重复断开请求或客户端状态不一致");
            }
        }

        await SendJsonResponse(context, 200, new { status = "success", message = "已断开连接" });
        sml?.LogInformation($"[响应发送] 已确认用户 '{username}' 断开连接");
    }

    private async Task HandleBroadcast(HttpListenerContext context)
    {
        var body = await new StreamReader(context.Request.InputStream).ReadToEndAsync();
        var message = ExtractJsonField(body, "message") ?? "无内容";
        var sender = ExtractJsonField(body, "sender") ?? "未知用户";
        var clientId = ExtractJsonField(body, "clientId") ?? "unknown";

        sml?.LogInformation($"[广播] 收到来自 '{sender}' (ID: {clientId}) 的广播请求, 广播消息: {message}");
        BroadcastMessage(message);
        
        await SendJsonResponse(context, 200, new { status = "success", message = "广播消息已处理" });
    }

    /// <summary>
    /// 停止服务器
    /// </summary>
    public void StopServer()
    {
        sml?.LogInformation("[服务器关闭] 正在关闭IRC服务器...");
        sml?.LogInformation($"[关闭统计] 关闭前在线用户数: {_connectedClients.Count}");
        CloseChatLog();
        
        lock (_connectedClients)
        {
            if (_connectedClients.Count > 0)
            {
                sml?.LogInformation("[强制断开] 以下用户将被强制断开连接:");
                foreach (var clientInfo in _connectedClients.Values)
                {
                    var onlineSeconds = (long)(DateTime.Now - clientInfo.ConnectTime).TotalSeconds;
                    var hours = onlineSeconds / 3600;
                    var minutes = (onlineSeconds % 3600) / 60;
                    var seconds = onlineSeconds % 60;
                    var onlineDuration = $"{hours:D2}:{minutes:D2}:{seconds:D2}";

                    sml?.LogInformation($"[强制断开] 用户: '{clientInfo.Username}' (ID: {clientInfo.ClientId})");
                    sml?.LogInformation($"[会话信息] 连接时间: {clientInfo.GetFormattedConnectTime()}, 在线时长: {onlineDuration}");
                }
            }
            _connectedClients.Clear();
        }
        _clientIdCounter = 0;
        _isRunning = false;
        _httpServer?.Stop();
        
        sml?.LogInformation("[服务器关闭] IRC服务器已完全关闭");
        sml?.LogInformation("[资源清理] 所有客户端连接已清除，资源已释放");
        _showManager.ShowThis(ShowManager.ShowItem.ShowInfo, "服务器已关闭");
    }

    /// <summary>
    /// 获取当前在线用户数
    /// </summary>
    public int GetConnectedClientCount()
    {
        lock (_connectedClients) return _connectedClients.Count;
    }

    private void InitializeChatLog()
    {
        try
        {
            var logDir = "./ciclog";
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

            _currentChatLogDate = DateTime.Now.ToString("yyyy-MM-dd");
            var logFileName = Path.Combine(logDir, $"chatlog-s-{_currentChatLogDate}.log");
            var fileStream = new FileStream(logFileName, FileMode.Append, FileAccess.Write);
            _chatLogWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };
                
            sml?.LogInformation($"[聊天日志] 聊天日志系统已初始化，日志文件: {logFileName}");
        }
        catch (IOException e)
        {
            sml?.LogError($"[聊天日志错误] 初始化聊天日志失败: {e.Message}");
        }
    }

    private void LogChatMessage(string username, string message)
    {
        try
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            if (today != _currentChatLogDate)
            {
                CloseChatLog();
                InitializeChatLog();
            }
                
            if (_chatLogWriter != null)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"{timestamp} [ {username} ] {message}";
                _chatLogWriter.WriteLine(logEntry);
            }
        }
        catch (Exception e)
        {
            sml?.LogError($"[聊天日志错误] 记录聊天消息失败: {e.Message}");
        }
    }

    private void InitializeServerEncryption(string customKey)
    {
        try
        {
            sml?.LogInformation("[服务器加密初始化] 开始初始化服务器加密系统...");
            _serverRsaKeyPair = RSA.Create(2048);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.GenerateKey();
            _serverAesKey = aes.Key;
            
            sml?.LogInformation("[服务器加密初始化] 服务器密钥对和AES密钥生成成功");
        }
        catch (Exception e)
        {
            sml?.LogError($"[服务器加密错误] 初始化加密系统失败: {e.Message}");
        }
    }
    
    private string? EncryptAesKey(RSA clientPublicKey)
    {
        try
        {
            var encryptedKey = clientPublicKey.Encrypt(_serverAesKey!, RSAEncryptionPadding.Pkcs1);
            return Convert.ToBase64String(encryptedKey);
        }
        catch (Exception e)
        {
            sml?.LogError($"[密钥加密错误] AES密钥加密失败: {e.Message}");
            return null;
        }
    }
    
    private string DecryptClientMessage(string encryptedMessage, ClientInfo clientInfo)
    {
        try
        {
            if (!clientInfo.IsEncryptionEnabled || clientInfo.AesKey == null)
            {
                return encryptedMessage;
            }
            
            using var aes = Aes.Create();
            aes.Key = clientInfo.AesKey;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;

            var decryptor = aes.CreateDecryptor();
            var decodedBytes = Convert.FromBase64String(encryptedMessage);
            var decryptedBytes = decryptor.TransformFinalBlock(decodedBytes, 0, decodedBytes.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception e)
        {
            sml?.LogError($"[消息解密错误] 客户端消息解密失败: {e.Message}");
            return encryptedMessage;
        }
    }
    
    private void CloseChatLog()
    {
        if (_chatLogWriter != null)
        {
            _chatLogWriter.Flush();
            _chatLogWriter.Close();
            _chatLogWriter.Dispose();
            _chatLogWriter = null;
            sml?.LogInformation("[聊天日志] 聊天日志文件已关闭");
        }
    }

    private async void BroadcastMessage(string message)
    {
        sml?.LogInformation($"[广播] 向所有在线用户广播消息: {message}");
        sml?.LogInformation($"[广播统计] 当前在线用户数: {_connectedClients.Count}");
            
        if (_connectedClients.Count == 0)
        {
            sml?.LogWarning("[广播警告] 没有在线用户，广播消息未发送");
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var broadcastMsg = JsonSerializer.Serialize(new { type = "broadcast", message, timestamp });
            
        int successCount = 0;
        int failCount = 0;
            
        lock (_connectedClients)
        {
            foreach (var clientInfo in _connectedClients.Values)
            {
                try
                {
                    SendPushMessage(clientInfo, broadcastMsg).ConfigureAwait(true);
                    sml?.LogInformation($"[广播发送] 向用户 '{clientInfo.Username}' (ID: {clientInfo.ClientId}) 发送广播消息");
                    successCount++;
                }
                catch (Exception e)
                {
                    sml?.LogError($"[广播失败] 向用户 '{clientInfo.Username}' 发送广播时出错: {e.Message}");
                    failCount++;
                }
            }
        }
            
        sml?.LogInformation($"[广播完成] 广播发送统计 - 成功: {successCount}人, 失败: {failCount}人");
    }

    private async Task SendPushMessage(ClientInfo clientInfo, string message)
    {
        if (clientInfo == null) throw new Exception("客户端不存在");
        
        var pushUrl = $"http://{clientInfo.IpAddress}:10026/push";
        using var httpClient = new HttpClient();
        var content = new StringContent(message, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(pushUrl, content);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"推送失败，状态码: {response.StatusCode}");
        }
    }

    private static string? ExtractJsonField(string json, string field)
    {
        var key = $"\"{field}\":";
        var start = json.IndexOf(key);
        if (start == -1) return null;
        start += key.Length + 1;
        if (start >= json.Length) return null;
        var end = json.IndexOf("\"", start);
        return end > start ? json.Substring(start, end - start) : null;
    }

    private async Task SendJsonResponse(HttpListenerContext context, int code, object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var buffer = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = code;
        context.Response.ContentType = "application/json";
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.Close();
    }

    /// <summary>
    /// 创建服务器句柄
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "CreateServer", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static IntPtr _CreateServer(int ipProtocol, int port, IntPtr namePtr, IntPtr descPtr, IntPtr distPtr, IntPtr keyPtr, IntPtr showPathPtr)
    {
        try
        {
            string name = Marshal.PtrToStringUTF8(namePtr) ?? "DefaultServer";
            string desc = Marshal.PtrToStringUTF8(descPtr) ?? "IRC Server";
            string dist = Marshal.PtrToStringUTF8(distPtr) ?? "UnknownDist";
            string key = Marshal.PtrToStringUTF8(keyPtr) ?? "";
            string showPath = Marshal.PtrToStringUTF8(showPathPtr) ?? ".show";
            
            var server = new Server(ipProtocol, port, name, desc, dist, key, showPath);
            GCHandle handle = GCHandle.Alloc(server);
            return GCHandle.ToIntPtr(handle);
        }
        catch { return IntPtr.Zero; }
    }

    /// <summary>
    /// 导出函数启动服务器
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "StartServer", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void _StartServer(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        try
        {
            GCHandle gch = GCHandle.FromIntPtr(handle);
            if (gch.Target is Server server && !server._isDisposed) server.StartServer();
        }
        catch { }
    }

    /// <summary>
    /// 导出函数销毁服务器
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "DestroyServer", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void _DestroyServer(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        try
        {
            GCHandle gch = GCHandle.FromIntPtr(handle);
            if (gch.Target is Server server)
            {
                if (!server._isDisposed)
                {
                    server._isDisposed = true;
                    server.StopServer();
                }
            }
            if (gch.IsAllocated) gch.Free();
        }
        catch { }
    }

    /// <summary>
    /// 导出函数获取在线人数
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "GetOnlineCount", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int _GetOnlineCount(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return 0;
        try
        {
            GCHandle gch = GCHandle.FromIntPtr(handle);
            return (gch.Target is Server server) ? server.GetConnectedClientCount() : 0;
        }
        catch { return 0; }
    }
}
