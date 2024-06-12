Mono 是一个开源的.Net 框架实现，Boehm-Demers-Weriser 垃圾收集器（简称 Boehm GC）是其支持的垃圾收集器之一。Boehm GC 是一个保守的标记-清除垃圾收集器，适用于 C 和 C++等不具备内置垃圾收集机制的预研。分析 Boehm GC 的源码可以帮助我们理解其工作原理和实现细节。

# 1. 源码结构

Boehm GC 的源码可以在 GitHub 上找到，主要位于`mono/mono/boehm`目录下，以下是一些关键文件和目录：

- `gc.h`：Boehm GC 的头文件，定义了主要的数据结构和函数接口。
- `alloc.h`：内存分配相关的实现。
- `mark.c`：标记阶段的实现。
- `reclaim.c`：清除阶段的实现。
- `headers.c`：对象头信息管理的实现。
- `malloc.c`：`malloc`和`free`函数的实现。
- `finalize.c`：对象终结器(finalizer)相关的实现。

# 2. 初始化

Boehm GC 的初始化过程在`gc.c`文件中实现。主要步骤包括：

1. 内存分配：为垃圾收集器分配所需的内存，包括堆、对象对信息等。
2. 数据结构初始化：初始化各种数据结构，如自由列表(free list)、根集等。
3. 线程管理：创建和管理用于并发垃圾收集的线程。

```c
void GC_init(void)
{
  // 初始化内存分配器
  GC_init_inner();

  // 初始化根集
  GC_init_roots();

  // 初始化并发垃圾收集线程
  GC_init_parallel();

  // 其他初始化操作
  ...
}
```

# 3. 内存分配

Boehm GC 提供了`GC_malloc`函数用于内存分配。该函数首先尝试从自由列表中分配内存，如果自由列表中没有合适的块，则触发垃圾收集。

```c
void *GC_malloc(size_t size)
{
  void *result;

  // 尝试从自由列表中分配内存
  result = GC_alloc_from_free_list(size);

  // 如果自由列表中没有合适的块，则触发垃圾收集
  if (result == NULL) {
    GC_collect();
    result = GC_alloc_from_free_list(size);
  }

  return result;
}
```

# 4. 标记阶段

标记阶段的主要认为是从根对象开始，递归地标记所有可达地对象。Boehm GC 使用深度优先搜索(DFS)算法进行标记。标记阶段的实现主要在`mark.c`文件中。

```c
void GC_mark_from_roots(void)
{
  // 遍历根集，标记所有可达的对象
  for (each root in root_set) {
    GC_mark_object(root);
  }
}

void GC_mark_object(void *obj)
{
  // 如果对象已经被标记，则返回
  if (GC_is_marked(obj)) {
    return;
  }

  // 标记对象
  GC_set_mark(obj);

  // 递归标记对象的所有引用
  for (each reference in obj) {
    GC_mark_object(reference);
  }
}
```

# 5. 清除阶段

清除阶段的主要任务是回收未标记的对象，将其加入自由列表。清除阶段的实现主要在`reclaim.c`文件中。

```c
void GC_reclaim(void)
{
  // 遍历堆中的所有对象
  for (each obj in heap) {
    // 如果对象未被标记，则回收
    if (!GC_is_marked(obj)) {
      GC_add_to_free_list(obj);
    }
  }
}
```

# 6. 并发垃圾收集

Boehm GC 支持并发垃圾收集，可以在应用程序运行时进行垃圾收集，从而减少暂停时间。并发垃圾收集的主要步骤包括：

1. 初始标记：暂停应用程序，标记从根对象到可达的对象。
2. 并发标记：在应用程序继续运行的同时，标记从初始标记阶段标记的对象可达的所有对象。
3. 重新标记：暂停应用程序，标记在并发标记阶段新创建或修改的对象。
4. 并发清除：在应用程序继续运行的同时，回收未标记的对象。

```c
void GC_concurrent_collect(void)
{
  // 初始标记
  GC_initial_mark();
  // 并发标记
  GC_concurrent_mark();
  // 重新标记
  GC_final_mark();
  // 并发清除
  GC_concurrent_sweep();

  // 其他操作
  ...
}
```

# 7. 写屏障（Write Barrier）

为了支持并发垃圾收集， Boehm GC 使用写屏障机制。写屏障是一种在对象引用或修改时执行的代码，用于跟踪对象引用的变化。写屏障的实现主要在`gc.c`文件中。

```c
void GC_write_barrier(void *obj, void *value)
{
  // 更新卡表
  GC_cardtable_update(obj, value);

  // 其他操作
  ...
}
```

# 8. 终结器(Finalizer)

终结器时对象在被垃圾收集器回收之前执行的一段代码，用于释放非托管资源或执行其他清理操作。Boehm GC 支持终结器的注册和执行，相关实现主要在`finalize.c`文件中。

终结器注册

当对象需要终结器时，可以通过`GC_register_finalizer`函数注册终结器。该函数将对象和终结器函数添加到一个终结器列表中。

```c
void GC_register_finalizer(void *obj, GC_finalization_proc finalizer, void *client_data)
{
  // 创建终结器条目
  finalizer_entry *entry = GC_new_finalizer_entry(obj, finalizer,client_data);

  // 将终结器条目添加到终结器列表中
  GC_add_to_finalizer_list(entry);
}
```

终结器执行

在垃圾收集的清除阶段，Boehm GC 会检查每个未标记的对象是否注册了终结器。如果注册了终结器，则将其添加到一个带执行的终结器队列中，而不是立即回收该对象。待垃圾收集完成后，Boehm GC 会执行这些终结器。

```c
void GC_finalize(void)
{
  // 遍历待执行的终结器列表
  for (each entry in finalizer_queue) {
    //  执行终结器
    entry->finalizer(entry->obj, entry->client_data);
  }
}
```

# 9. 根集管理

根集(Root Set)是垃圾收集器的起点，包括全局变量、栈变量和寄存器中的引用。Boehm GC 需要扫描根集以标记所有可达的对象。根集管理的实现主要在`root.c`文件中。

根集注册

Boehm GC 提供了`GC_add_roots`函数用于注册根集。该函数将根集的起始地址和结束地址添加到一个根集列表中。

```c
void GC_add_roots(void *start, void *end)
{
  // 创建根集条目
  root_entry *entry = GC_new_root_entry(start, end);

  // 将根集条目添加到根集列表中
  GC_add_to_root_list(entry);
}
```

根集扫描

在标记阶段， Boehm GC 会遍历根集列表，扫描每个根集中的引用并标记相应的对象。

```c
void GC_mark_from_roots(void)
{
  // 遍历根集列表
  for (each entry in root_list) {
    // 扫描根集中的引用
    GC_scan_root(entry->start, entry->end);
  }
}

void GC_scan_root(void *start, void *end)
{
  // 遍历根集中的每个引用
  for (each reference in start to end) {
    // 标记引用的对象
    GC_mark_object(reference);
  }
}
```

# 10. 并发垃圾收集的细节

并发垃圾收集是 Boehm GC 的一个重要特性，额可以在应用程序运行时进行垃圾收集，从而减少暂停时间。并发垃圾收集的实现涉及多个线程的协调和同步。

初始标记

初始标记阶段需要暂停应用程序，以确保根集和堆的一致性。初始标记阶段的主要任务是标记从根对象可达的对象。

```c
void GC_initial_mark(void)
{
  // 暂停应用程序
  GC_stop_world();

  // 标记从根对象可达的对象
  GC_mark_from_roots();

  // 恢复应用程序
  GC_start_world();
}
```

并发标记

并发标记阶段在应用程序继续运行的同时进行。Boehm GC 使用一个并发标记线程来标记从初始标记阶段标记的对象可达的所有对象。

```c
void GC_concurrent_mark(void)
{
  // 创建并发标记线程
  GC_create_mark_thread();

}

// 并发标记线程的主循环
void GC_mark_thread(void *arg)
{
  while (GC_is_marking()) {
    // 标记对象
    GC_mark_some_objects();
  }
  return NULL;
}
```

重新标记

重新标记阶段需要再次暂停应用阶段，以确保在并发标记阶段新创建或修改的对象也被标记。

```c
void GC_final_mark(void)
{
  // 暂停应用程序
  GC_stop_world();

  // 标记新创建或修改的对象
  GC_mark_from_roots();

  // 恢复应用程序
  GC_start_world();
}
```

并发清除

并发清除阶段在应用程序继续运行的同时进行。Boehm GC 使用一个并发清除线程来回收标记的对象。

```c
void GC_concurrent_sweep(void)
{
  // 创建并发清除的线程
  GC_create_sweep_thread();

}

// 并发清除线程的主循环
void *GC_sweep_thread(void *arg)
{
  while (GC_is_sweeping()) {
    // 回收未标记的对象
    GC_sweep_some_objects();
  }
  return NULL;
}
```

# 11. 写屏障的实现

写屏障是一种在对象引用或被修改时执行的代码，用于根据对象引用的变化。写屏障的实现主要在`gc.c`文件中。

写屏障的基本原理

写屏障的基本原理是，在每次修改对象引用时，记录下被修改的对象和新引用的对象。这样可以在并发垃圾收集时，确保新创建或修改的对象不会被错误地回收。

```c
void GC_write_barrier(void *obj, void *value)
{
  // 更新卡表
  GC_cardtable_update(obj, value);

  // 其他操作
  ...
}
```

卡表更新

卡表是一种用于根据对象引用变化地数据结构。每个卡片（card）对应堆内存中地一个固定大小地区域，每个卡片对应一个卡表条目。卡表条目是一个布尔值或标记为，表示该卡片是否包含对对象地引用。

```c
void GC_cardtable_update(void *obj, void *value)
{
  // 计算对象所在地卡片
  card_index = GC_card_index(obj);

  // 将卡片标记为“脏”
  GC_cardtable[card_index] = DIRTY;
}
```
