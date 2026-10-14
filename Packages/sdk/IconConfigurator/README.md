# PICO Icon Configurator

Icon Configurator is a Unity Editor tool bundled with the PICO XR SDK. It helps
configure layered spatial launcher icons, localized app names, previews, and
Android export resources for PICO projects.

Open the tool from the Unity Editor menu:

```text
PICO > Icon Configurator
```

The tool writes project-specific state and generated resources under the host
Unity project, including:

- `Assets/IconConfigurator`
- `Assets/Plugins/Android/IconConfigurator.androidlib`
- `Assets/Plugins/Android/AndroidManifest.xml`
- `Assets/Plugins/Android/launcherManifest.xml`

Package-owned editor code and UI assets live in this repository under:

```text
Packages/sdk/IconConfigurator
```

Unity package asset references should use the PICO XR SDK package name:

```text
Packages/com.bytedance.pico.xr/IconConfigurator
```

The C# assembly and namespace use the PICO SDK namespace pattern:

```text
ByteDance.PICO.IconConfigurator.Editor
```
