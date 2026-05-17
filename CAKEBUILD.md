# CoffeeIRC Cake 构建指南

本项目使用 [Cake (C# Make)](https://cakebuild.net/) 作为构建自动化工具，类似于 Gradle。

## 快速开始

### Linux/macOS

```bash
# 赋予执行权限
chmod +x build.sh

# 运行默认构建（Build + Test）
./build.sh

# 运行完整 CI 流程（Build + Test + Package All）
./build.sh --target=CI

# 只构建
./build.sh --target=Build

# 只运行测试
./build.sh --target=Test

# 发布 Linux 版本
./build.sh --target=Publish-Linux

# 打包所有平台
./build.sh --target=Package-All

# 使用 Debug 配置
./build.sh --configuration=Debug
```

### Windows

```powershell
# 运行默认构建
.\build.ps1

# 运行完整 CI 流程
.\build.ps1 --target=CI

# 其他命令与 Linux 类似
.\build.ps1 --target=Build
.\build.ps1 --target=Test
.\build.ps1 --target=Publish-Windows
```

### 直接使用 dotnet cake

```bash
# 首次使用需要安装 Cake.Tool
dotnet tool install -g Cake.Tool

# 运行构建
dotnet cake --target=Default
dotnet cake --target=CI
dotnet cake --target=Publish-Linux
```

## 可用任务（Tasks）

### 基础任务

- **Clean** - 清理所有构建输出目录
- **Restore** - 恢复 NuGet 包
- **Build** - 构建项目
- **Test** - 运行单元测试

### 发布任务（NativeAOT）

- **Publish-Linux** - 发布 Linux x64 版本
- **Publish-Windows** - 发布 Windows x64 版本
- **Publish-MacOS-x64** - 发布 macOS x64 版本
- **Publish-MacOS-Arm64** - 发布 macOS ARM64 版本

### 打包任务

- **Package-Linux** - 创建 Linux tar.gz 包
- **Package-Windows** - 创建 Windows ZIP 包
- **Package-MacOS** - 创建 macOS tar.gz 包（x64 + ARM64）
- **Package-All** - 打包所有平台

### 组合任务

- **Default** - 默认任务：Build + Test
- **CI** - 完整 CI 流程：Build + Test + Package-All

## 参数说明

- `--target=<TaskName>` - 指定要运行的任务（默认：Default）
- `--configuration=<Config>` - 构建配置（默认：Release，可选：Debug）

示例：
```bash
./build.sh --target=Publish-Linux --configuration=Debug
```

## 输出目录

- **构建输出**: `io.github.deplayeris.coffeeirc/bin/<Configuration>/net10.0/`
- **NativeAOT 发布**: `io.github.deplayeris.coffeeirc/bin/<Configuration>/net10.0/<runtime>/native/`
- **打包文件**: `artifacts/`

## 与 GitHub Actions 的对应关系

| GitHub Actions 工作流 | Cake 任务 |
|---------------------|----------|
| publish-linux.yml | `Publish-Linux` → `Package-Linux` |
| publish-windows.yml | `Publish-Windows` → `Package-Windows` |
| publish-macos.yml | `Publish-MacOS-x64` + `Publish-MacOS-Arm64` → `Package-MacOS` |

## 优势

相比直接使用 `dotnet` 命令，Cake 提供了：

1. **统一接口** - 跨平台的构建脚本
2. **任务依赖管理** - 自动处理任务执行顺序
3. **可复用性** - 易于在 CI/CD 和本地开发中使用
4. **类型安全** - 使用 C# 编写，享受 IDE 智能提示
5. **扩展性** - 丰富的插件生态系统

## 故障排除

### Cake.Tool 未找到

```bash
dotnet tool install -g Cake.Tool --version 4.0.0
export PATH="$PATH:$HOME/.dotnet/tools"  # Linux/macOS
```

### NativeAOT 编译失败（Linux）

确保安装了必要的依赖：
```bash
sudo apt-get update
sudo apt-get install -y clang zlib1g-dev
```

### 权限问题（Linux/macOS）

```bash
chmod +x build.sh
```
