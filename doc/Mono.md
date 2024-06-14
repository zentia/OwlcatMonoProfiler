https://www.mono-project.com/docs/

Unity 在 2022.3 的 mono 代码仓库里面，已经彻底放飞自我了，将 unity 需要的代码和 mono 代码柔和在一块了。

比如 5.6 的时候还有一个单独的 unity 目录，2022 已经去掉了。

mono 目录是 mono CLR 的 C++实现

mono\mono\metadata 提供了对 IL 字节码里面各种结构解析和操作，比如 Class、Object 数据维护和操作接口

`mono/mono/mini`是 mono 实现的 jit 编译器。

Mono 还支持 AOT

`mono/mono/arch`对平台的支持
