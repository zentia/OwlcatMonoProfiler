Unity 的增量垃圾收集(Incremental Garbage Collection,IGC)是 Unity 引擎中的一个重要特性，它通过将垃圾收集过程分解为多个小步骤，以减少垃圾收集对游戏性能的影响。增量 GC 的实现涉及到多个方面，包括内存分配、标记和清除阶段、写屏障等。

# 1. 源码结构

il2cpp 的增量 GC 源码通常位于 `External/il2cpp/build/il2cpp/gc`：

- `GarbageCollector.cpp`：增量 GC 的主要实现文件。
- `GarbageCollector.h`：增量 GC 的头文件，定义了主要的数据结构和函数接口。
- `GCWriteBarrierValidation.cpp`：写屏障的实现。
- `Marking.cpp`：标记阶段的实现。
- `Sweeping.cpp`：清除阶段的实现。

# 2. 初始化

增量 GC 的初始化过程在`Runtime.cpp`文件中实现。主要步骤包括：

1. 内存分配：为垃圾收集器分配所需的内存，包括堆、对象头信息等。
2. 数据结构初始化：初始化各种数据结构，如自由列表（free list）、根集等。
3. 线程管理：创建和管理用于并发垃圾收集的线程。

```cpp
bool Runtime::Init(const char* domainName)
{
  // 初始化内存分配器
  gc::GarbageCollector::Initialize();

  // 初始化根集
  InitializeRoots();

  // 初始化并发垃圾收集线程
  InitializeParallel();

  // 其他初始化操作
  ...
}
```

# 3. 内存分配

增量 GC 提供了`Allocate`函数用于内存分配。该函数首先尝试从自由列表中分配内存，如果自由列表中没有合适的块，则触发垃圾收集。

```cpp
void *GarbageCollector::Allocate(size_t size)
{
  void *result;

  // 尝试从自由列表中分配内存
  result = AllocateFromFreeList(size);

  // 如果自由列表中没有合适的块，则触发垃圾收集
  if (result == nullptr) {
    Collect();
    result = AllocateFromFreeList(size);
  }

  return result;
}
```

# 4. 标记阶段

标记阶段的主要任务是从根对象开始，递归地标记所有可达地对象。增量 GC 使用深度优先搜索(DFS)算法进行标记。标记阶段地实现主要`Marking.cpp`文件中。

```cpp
void GarbageCollector::MarkFromRoots()
{
  // 遍历根集，标记所有可达地对象
  for (auto root : rootSet) {
    MarkObject(root);
  }
}

void GarbageCollector::MarkObject(void *obj)
{
  // 如果对象已经被标记，则返回
  if (IsMarked(obj)) {
    return;
  }

  // 标记对象
  SetMark(obj);

  // 递归标记对象地所有引用
  for (auto reference : GetReferences(obj)) {
    MarkObject(reference);
  }
}
```

# 5. 清除阶段

清除阶段地主要任务是回收未标记地对象，将其加入到自由列表。清楚阶段的实现主要`Sweeping.cpp`文件中。

```cpp
void GarbageCollector::Sweep()
{
  // 遍历堆中所有对象
  for (auto obj : heap) {
    // 如果对象未被标记，则回收
    if (!IsMarked(obj)) {
      AddToFreeList(obj);
    }
  }
}
```

# 6. 增量垃圾收集

增量垃圾收集通过将垃圾收集过程分解为多个小步骤，以减少垃圾收集对游戏性能的影响。增量垃圾收集的主要步骤包括：

1. 初始标记：暂停应用程序，标记从根对象可达的对象。
2. 增量标记：在应用程序继续运行的同时，标记从初始标记阶段标记的对象可达的所有对象。
3. 重新标记：暂停应用程序，标记在增量标记阶段新创建或修改的对象。
4. 增量消除：在应用程序继续运行的同时，回收未标记的对象。

```cpp
void GarbageCollector::IncrementalCollect()
{
  // 初始标记
  InitialMark();

  // 增量标记
  IncrementalMark();

  // 重新标记
  FinalMark();

  // 增量消除
  IncrementalSweep();

  // 其他操作
  ...
}
```

# 7. 写屏障(Write Barrier)

为了支持增量垃圾收集，增量 GC 使用写屏障机制。写屏障是一种在对象引用被修改时执行的代码，用于跟踪对象引用的变化。写屏障的实现主要在`WriteBarrier.cpp`文件中。

```cpp
void GarbageCollector::WriteBarrier(void *obj, void* value)
{
  // 更新卡表
  UpdateCardTable(obj, value);

  // 其他操作
  ...
}
```

# 终结器(Finalizer)

```cpp
void GarbageCollector::InitializeFinalizer()
{
  GarbageCollector::InvokeFinalizers();
}
```

# 8. 根集管理

根集(RootSet)时垃圾收集器的起点，包括全局变量、栈变量和寄存器中的引用。增量 GC 需要扫描根集以标记所有可达的对象。根集管理的实现主要在`GarbageCollector.cpp`文件中。

根集注册

增量 GC 提供了`RegisterRoot`函数用于注册根集。该函数将根集的起始地址和结束地址添加到一个根集列表中。

```cpp
static void*
register_root(void *arg)
{
  RootData* root_data = (RootData*)arg;
  s_Root.insert(std::make_pair(root_data->start, root_data->end));
  return NULL;
}

void il2cpp::gc::GarbageCollector::RegisterRoot(chat* start, size_t size)
{
  // 创建根集条目
  RootData root_data;
  root_data.start = start;
  /* Boehm root processing requires ont byte past end of region to be scanned */
  root_data.end = start + size + 1;
  // 将根集条目添加到根集列表中
  CallWithAllocLockHeld(register_root, &root_data);
}
```

根集扫描

在标记阶段，增量 GC 会遍历根集列表，扫描每个根集中的引用并标记相应的对象。

```cpp
void GarbageCollector::MarkFromRoots()
{
  // 遍历根集列表
  for (auto entry : rootSet) {
    // 扫描根集中的引用
    ScanRoot(entry.start, entry.end);
  }
}

void GarbageCollector::ScanRoot(void *start, void* end)
{
  // 遍历根集中的每个引用
  for (auto reference = start; reference < end; reference += sizeof(void*)>) {
    // 标记引用的对象
    MarkObject(*reinterpret_cast<void**>(reference));
  }
}
```

# 9. 增量垃圾收集的细节

增量垃圾收集是 Unity 增量 GC 的一个重要特性，可以在应用程序运行时进行垃圾收集，从而减少暂停时间。增量垃圾收集的实现涉及多个线程的协调和同步。

初始标记

初始标记阶段需要暂停应用程序，以确保根集和堆的一致性。初始标记阶段的主要任务是标记从根对象可达的对象。

```cpp
void GarbageCollector::InitialMark()
{
  // 暂停应用程序
  StopWorld();

  // 标记从根对象可达的对象
  MarkFromRoots();

  // 恢复应用程序
  StartWorld();
}
```

增量标记

增量标记阶段在应用程序继续运行的同时进行。增量 GC 使用一个增量标记线程来标记从初始标记阶段标记的对象可达的所有对象。

```cpp
void GarbageCollector::IncrementalMark()
{
  // 创建增量标记线程
  CreateMarkThread();


}

// 增量标记线程的主循环
void *MarkThread(void *arg)
{
  while (IsMarking()) {
    // 标记对象
    MarkSomeObjects();
  }

  return nullptr;
}
```

重新标记

重新标记阶段需要再次暂停应用程序，以确保在增量标记阶段新创建或修改的对象也被标记。

```cpp
void GarbageCollector::FinalMark()
{
  // 暂停应用程序
  StopWorld();

  // 标记新创建或修改的对象
  MarkFromRoots();

  // 恢复应用程序
  StartWorld();
}
```

增量消除

增量消除阶段在应用程序继续运行的同时进行。增量 GC 使用一个增量消除线程来回收被标记的对象。

```cpp
void GarbageCollector::IncrementalSweep()
{
  // 创建增量消除线程
  CreateSweepThread();

}

// 增量消除线程的主循环
void *SweepThread(void *arg)
{
  while (IsSweeping()) {
    // 回收为标记的对象
    SweepSomeObjects();
  }
  return nullptr;
}
```
