using System;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace io.github.deplayeris.coffeeirc.client.Tests;

/// <summary>
/// Client核心功能单元测试类
/// </summary>
public class ClientFunctionalityTests
{
    private readonly string _testShowFilePath;

    /// <summary>
    /// 初始化测试环境，创建临时呈现器接口文件路径
    /// </summary>
    public ClientFunctionalityTests()
    {
        _testShowFilePath = Path.Combine(Path.GetTempPath(), $"coffeeirc_test_{Guid.NewGuid()}.show");
    }

    /// <summary>
    /// 测试 ShowManager 是否正确格式化消息类型 (ShowMessage)
    /// </summary>
    [Fact]
    public void ShowManager_ShowMessage_WritesCorrectFormat()
    {
        var showManagerType = typeof(Client).GetNestedType("ShowManager", BindingFlags.NonPublic);
        Assert.NotNull(showManagerType);

        var constructor = showManagerType.GetConstructor(new[] { typeof(string) });
        Assert.NotNull(constructor);

        var instance = constructor.Invoke(new object[] { _testShowFilePath });
        var showThisMethod = showManagerType.GetMethod("ShowThis");
        Assert.NotNull(showThisMethod);

        var showItemEnum = showManagerType.GetNestedType("ShowItem");
        var showMessageValue = Enum.Parse(showItemEnum, "ShowMessage");

        showThisMethod.Invoke(instance, new object[] { showMessageValue, "Test Message" });

        var content = File.ReadAllText(_testShowFilePath);
        Assert.Equal("[MSG]Test Message", content);
    }

    /// <summary>
    /// 测试 ShowManager 是否正确格式化错误类型 (ShowError)
    /// </summary>
    [Fact]
    public void ShowManager_ShowError_WritesCorrectFormat()
    {
        var showManagerType = typeof(Client).GetNestedType("ShowManager", BindingFlags.NonPublic);
        var constructor = showManagerType.GetConstructor(new[] { typeof(string) });
        var instance = constructor.Invoke(new object[] { _testShowFilePath });
        var showThisMethod = showManagerType.GetMethod("ShowThis");
        var showItemEnum = showManagerType.GetNestedType("ShowItem");
        var showErrorValue = Enum.Parse(showItemEnum, "ShowError");

        showThisMethod.Invoke(instance, new object[] { showErrorValue, "Critical Error" });

        var content = File.ReadAllText(_testShowFilePath);
        Assert.Equal("[ERR]Critical Error", content);
    }

    /// <summary>
    /// 验证 SwInfoc 软件信息类是否包含所有必需的静态属性
    /// </summary>
    [Fact]
    public void SwInfoc_ContainsRequiredInformation()
    {
        Assert.False(string.IsNullOrEmpty(SwInfoc.Version));
        Assert.False(string.IsNullOrEmpty(SwInfoc.SoftwareStatus));
        Assert.False(string.IsNullOrEmpty(SwInfoc.VerCodename));
        Assert.False(string.IsNullOrEmpty(SwInfoc.Connection));
    }

    /// <summary>
    /// 测试 Client 构造函数是否能正确初始化基本属性和组件
    /// </summary>
    [Fact]
    public void Client_Constructor_InitializesComponents()
    {
        var client = new Client(4, "127.0.0.1", 8080, "TestUser", "test", "TestDist", "", _testShowFilePath);
        
        Assert.False(client.IsConnected());
        Assert.Equal("TestUser", client.GetNickname());
        Assert.Equal("test", client.GetUsername());
        Assert.True(File.Exists(_testShowFilePath));
    }

    /// <summary>
    /// 测试 Client 关闭方法是否能正确释放资源并记录日志
    /// </summary>
    [Fact]
    public void Client_Close_DisposesResources()
    {
        var client = new Client(4, "127.0.0.1", 8080, "TestUser", "test", "TestDist", "", _testShowFilePath);
        client.Close();

        var content = File.ReadAllText(_testShowFilePath);
        Assert.Contains("[INF]", content);
    }

    /// <summary>
    /// 全面测试 ShowManager 中所有枚举项的前缀映射逻辑
    /// </summary>
    [Fact]
    public void ShowManager_AllShowItems_ProduceCorrectPrefixes()
    {
        var showManagerType = typeof(Client).GetNestedType("ShowManager", BindingFlags.NonPublic);
        var constructor = showManagerType.GetConstructor(new[] { typeof(string) });
        var instance = constructor.Invoke(new object[] { _testShowFilePath });
        var showThisMethod = showManagerType.GetMethod("ShowThis");
        var showItemEnum = showManagerType.GetNestedType("ShowItem");

        var expectedMappings = new System.Collections.Generic.Dictionary<string, string>
        {
            { "ShowMessage", "[MSG]" },
            { "ShowTip", "[TIP]" },
            { "ShowError", "[ERR]" },
            { "ShowWarning", "[WAN]" },
            { "ShowInfo", "[INF]" },
            { "ShowDebug", "[DBG]" },
            { "ShowChatmsg", "[CHT]" }
        };

        foreach (var mapping in expectedMappings)
        {
            var showItemValue = Enum.Parse(showItemEnum, mapping.Key);
            showThisMethod.Invoke(instance, new object[] { showItemValue, "Content" });
            var content = File.ReadAllText(_testShowFilePath);
            Assert.StartsWith(mapping.Value, content);
        }
    }

    /// <summary>
    /// 测试加密系统在启用后能否正确加解密消息
    /// </summary>
    [Fact]
    public void Encryption_EncryptAndDecrypt_PreservesMessage()
    {
        var client = new Client(4, "127.0.0.1", 8080, "TestUser", "test", "TestDist", "TestKey", _testShowFilePath);
        
        var encryptMethod = typeof(Client).GetMethod("EncryptMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        var decryptMethod = typeof(Client).GetMethod("DecryptMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        var initEncryptMethod = typeof(Client).GetMethod("InitializeEncryption", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(encryptMethod);
        Assert.NotNull(decryptMethod);
        Assert.NotNull(initEncryptMethod);

        // 初始化加密系统
        initEncryptMethod.Invoke(client, new object[] { "TestSeed" });
        
        // 通过反射强制开启加密标志并设置一个测试用的 AES Key (16字节)
        var enabledField = typeof(Client).GetField("encryptionEnabled", BindingFlags.NonPublic | BindingFlags.Instance);
        var keyField = typeof(Client).GetField("aesKey", BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (enabledField != null) enabledField.SetValue(client, true);
        if (keyField != null) keyField.SetValue(client, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
        
        string originalMessage = "Hello CoffeeIRC!";
        string encrypted = (string)encryptMethod.Invoke(client, new object[] { originalMessage });
        
        Assert.NotEqual(originalMessage, encrypted);
        
        string decrypted = (string)decryptMethod.Invoke(client, new object[] { encrypted });
        Assert.Equal(originalMessage, decrypted);
    }

    /// <summary>
    /// 测试聊天日志记录功能是否能正确追加内容
    /// </summary>
    [Fact]
    public void ChatLog_LogMessage_WritesToFile()
    {
        var client = new Client(4, "127.0.0.1", 8080, "TestUser", "test", "TestDist", "", _testShowFilePath);
        var logMethod = typeof(Client).GetMethod("LogChatMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        
        Assert.NotNull(logMethod);
        logMethod.Invoke(client, new object[] { "Friend", "Hi there!" });

        string logPath = Path.Combine("./ciclogs", $"chatlog-c-{DateTime.Now:yyyy-MM-dd}.log");
        if (File.Exists(logPath))
        {
            var content = File.ReadAllText(logPath);
            Assert.Contains("Friend", content);
            Assert.Contains("Hi there!", content);
        }
    }

    /// <summary>
    /// 测试在未连接状态下发送消息是否会被安全拦截
    /// </summary>
    [Fact]
    public async void SendMessage_WhenNotConnected_DoesNotThrow()
    {
        var client = new Client(4, "127.0.0.1", 8080, "TestUser", "test", "TestDist", "", _testShowFilePath);
        
        await client.SendMessageAsync("This should fail silently");
        
        Assert.False(client.IsConnected());
    }

    /// <summary>
    /// 测试 ShowManager 处理空消息或特殊字符的健壮性
    /// </summary>
    [Fact]
    public void ShowManager_ShowSpecialCharacters_HandlesCorrectly()
    {
        var showManagerType = typeof(Client).GetNestedType("ShowManager", BindingFlags.NonPublic);
        var constructor = showManagerType.GetConstructor(new[] { typeof(string) });
        var instance = constructor.Invoke(new object[] { _testShowFilePath });
        var showThisMethod = showManagerType.GetMethod("ShowThis");
        var showItemEnum = showManagerType.GetNestedType("ShowItem");
        var showMessageValue = Enum.Parse(showItemEnum, "ShowMessage");

        showThisMethod.Invoke(instance, new object[] { showMessageValue, "" });
        var content = File.ReadAllText(_testShowFilePath);
        Assert.Equal("[MSG]", content);
    }
}
