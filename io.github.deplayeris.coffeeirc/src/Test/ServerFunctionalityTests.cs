using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace io.github.deplayeris.coffeeirc.server.Tests;

/// <summary>
/// Server核心功能单元测试类
/// </summary>
public class ServerFunctionalityTests
{
    private readonly string _testShowFilePath;

    /// <summary>
    /// 初始化测试环境
    /// </summary>
    public ServerFunctionalityTests()
    {
        _testShowFilePath = Path.Combine(Path.GetTempPath(), $"coffeeirc_server_test_{Guid.NewGuid()}.show");
    }

    /// <summary>
    /// 测试 ShowManager 是否正确格式化信息类型 (ShowInfo)
    /// </summary>
    [Fact]
    public void ShowManager_ShowInfo_WritesCorrectFormat()
    {
        var manager = new ShowManager(_testShowFilePath);
        manager.ShowThis(ShowManager.ShowItem.ShowInfo, "Server Ready");
        
        var content = File.ReadAllText(_testShowFilePath);
        Assert.Equal("[INF]Server Ready", content);
    }

    /// <summary>
    /// 验证 SwInfos 服务器信息类是否包含所有必需的静态属性
    /// </summary>
    [Fact]
    public void SwInfos_ContainsRequiredInformation()
    {
        Assert.False(string.IsNullOrEmpty(SwInfos.Version));
        Assert.False(string.IsNullOrEmpty(SwInfos.SoftwareStatus));
        Assert.False(string.IsNullOrEmpty(SwInfos.VerCodename));
        Assert.False(string.IsNullOrEmpty(SwInfos.Connection));
    }

    /// <summary>
    /// 测试 Server 构造函数是否能正确初始化组件
    /// </summary>
    [Fact]
    public void Server_Constructor_InitializesComponents()
    {
        var server = new Server(4, 10025, "TestInstance", "TestDesc", "TestDist", "", _testShowFilePath);
        Assert.NotNull(server);
        Assert.True(File.Exists(_testShowFilePath));
    }

    /// <summary>
    /// 全面测试 ShowManager 中所有枚举项的前缀映射逻辑
    /// </summary>
    [Fact]
    public void ShowManager_AllShowItems_ProduceCorrectPrefixes()
    {
        var manager = new ShowManager(_testShowFilePath);
        var expectedMappings = new System.Collections.Generic.Dictionary<ShowManager.ShowItem, string>
        {
            { ShowManager.ShowItem.ShowMessage, "[MSG]" },
            { ShowManager.ShowItem.ShowTip, "[TIP]" },
            { ShowManager.ShowItem.ShowError, "[ERR]" },
            { ShowManager.ShowItem.ShowWarning, "[WAN]" },
            { ShowManager.ShowItem.ShowInfo, "[INF]" },
            { ShowManager.ShowItem.ShowDebug, "[DBG]" }
        };

        foreach (var mapping in expectedMappings)
        {
            manager.ShowThis(mapping.Key, "Content");
            var content = File.ReadAllText(_testShowFilePath);
            Assert.StartsWith(mapping.Value, content);
        }
    }

    /// <summary>
    /// 测试 ShowManager 处理空消息或特殊字符的健壮性
    /// </summary>
    [Fact]
    public void ShowManager_ShowSpecialCharacters_HandlesCorrectly()
    {
        var manager = new ShowManager(_testShowFilePath);
        
        // 测试空消息
        manager.ShowThis(ShowManager.ShowItem.ShowMessage, "");
        Assert.Equal("[MSG]", File.ReadAllText(_testShowFilePath));
        
        // 测试包含特殊字符的消息
        manager.ShowThis(ShowManager.ShowItem.ShowError, "Error: \"Null\" & <Empty>");
        Assert.Contains("Null", File.ReadAllText(_testShowFilePath));
    }

    /// <summary>
    /// 测试 Server 实例的生命周期（启动与停止）
    /// </summary>
    [Fact]
    public void Server_Lifecycle_StartAndStop()
    {
        var server = new Server(4, 10026, "LifecycleTest", "Desc", "TestDist", "", _testShowFilePath);
        
        server.StartServer();
        server.StopServer();
        
        var content = File.ReadAllText(_testShowFilePath);
        Assert.Contains("[INF]", content);
    }

    /// <summary>
    /// 测试 Native 导出函数对无效句柄的安全性校验
    /// </summary>
    [Fact]
    public void NativeExports_InvalidHandle_DoesNotCrash()
    {
        var methodStart = typeof(Server).GetMethod("_StartServer", BindingFlags.NonPublic | BindingFlags.Static);
        var methodDestroy = typeof(Server).GetMethod("_DestroyServer", BindingFlags.NonPublic | BindingFlags.Static);
        var methodCount = typeof(Server).GetMethod("_GetOnlineCount", BindingFlags.NonPublic | BindingFlags.Static);
        
        methodStart?.Invoke(null, new object[] { IntPtr.Zero });
        methodDestroy?.Invoke(null, new object[] { IntPtr.Zero });
        var result = methodCount?.Invoke(null, new object[] { IntPtr.Zero });
        Assert.Equal(0, Convert.ToInt32(result ?? 0));
    }
}
