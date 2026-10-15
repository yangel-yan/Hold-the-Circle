# HOLD THE CIRCLE Android Gradle Export

这是《守住圆环》（HOLD THE CIRCLE）的 Unity Android Gradle 导出工程，用于提交 VibeLab 云构建。仓库不是 Unity 源工程，也不包含可直接上传的 APK。

## 构建基线

| 项目 | 值 |
| --- | --- |
| Unity | 6000.3.14f1 |
| Backend / ABI | IL2CPP / arm64-v8a |
| PICO SDK | PICO XR 6.0.0 |
| Android | compileSdk 36 / targetSdk 36 / minSdk 29 |
| Android Gradle Plugin | 8.10.0 |
| Gradle | 8.13 |
| JDK | 17 |
| NDK | 27.2.12479018 (r27c) |
| Application ID | `com.yangel.holdthecircle` |

`libil2cpp.so` 已在开发机上预编译并保存在 `unityLibrary/src/main/jniLibs/arm64-v8a/`。`unityLibrary/src/main/Il2CppOutputProject/` 已移除，Gradle 的 `buildIl2Cpp` 任务保持原定义但被禁用。

## VibeLab 提交

1. 将仓库推送到 GitHub。
2. 记录需要提交的固定 commit ID。
3. 在 VibeLab 中填写仓库地址和该 commit ID。

`.vibelab-prebuilt.json` 记录预编译产物和 native inventory 的 SHA-256。平台构建环境需要在构建前重写 `gradle.properties` 和两个 module `build.gradle` 中指向本机 Unity 安装目录的五处路径，并确认 `buildIl2Cpp SKIPPED`。

## 本地验证

Unity 6 导出的工程保留 `gradle/wrapper/gradle-wrapper.properties`，但不生成 `gradlew` / `gradlew.bat`，这是标准导出行为。使用 Gradle 8.13、JDK 17，并显式设置 Android SDK 环境变量：

```powershell
$env:JAVA_HOME = "<JDK 17>"
$env:ANDROID_HOME = "<Android SDK>"
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
gradle assembleRelease --console=plain
```

预期结果：

- `BUILD SUCCESSFUL`
- `:unityLibrary:buildIl2Cpp SKIPPED`
- APK 内 `libil2cpp.so` 与 `.vibelab-prebuilt.json` 的 `il2cpp_so_sha256` 一致

`local.properties`、Gradle 缓存、构建目录、APK 和签名材料不应提交。

## 已知警告

- `libil2cpp.so` 约 92.4 MB，低于 GitHub 100 MB 硬限制，但会触发 GitHub 的 50 MB 大文件警告。
- 最终 APK 中 16 个 ARM64 native libraries 有 7 个 LOAD segment 对齐不低于 16 KB；9 个 PICO/第三方库为 4 KB 对齐。当前按 VibeLab 文档的 PICO `page_size_policy=warn` 处理，不声明支持 16 KB page-size 设备。
- 此工程在 Windows 上完成本机构建终验；参考流程对 Windows 真机与 VibeLab 线上环境未作保证，最终结果以平台端到端构建为准。

## License

项目原创代码采用 MIT License。PICO SDK、Unity 运行时、字体及其他第三方组件继续适用各自许可，本仓库根目录许可文件不会重新许可这些组件。
