# AOT 与零反射

TinyOrm 的实体与 DTO 物化器由 Source Generator 在编译期生成，不依赖运行时反射；查询与写操作依赖已生成的委托与映射元数据，天然适配 AOT 与 trimming。

## 原理概述

- 编译期生成 `EntityMap` 与 Materializer：注册到 `MappingRegistry`（`src/TinyOrm.Runtime/Mapping/MappingRegistry.cs:31-42`）
- 运行时按类型获取映射与委托，绑定参数、读取数据、生成 SQL，无反射调用
- 日志输出采用 `Action<string>` 委托，无动态代理

## 发布示例

```bash
# Windows x64 原生 AOT
 dotnet publish src/TinyOrm.Examples/TinyOrm.Examples.csproj -c Release -r win-x64 /p:PublishAot=true

# Linux x64 原生 AOT
 dotnet publish src/TinyOrm.Examples/TinyOrm.Examples.csproj -c Release -r linux-x64 /p:PublishAot=true
```

## 注意事项

- 保持实体/DTO 的属性为可访问，Source Generator 能正确生成读取代码
- 避免运行时反射操作（当前 TinyOrm 不依赖反射）
- 依赖库需兼容 AOT；TinyOrm 核心不使用动态代码生成