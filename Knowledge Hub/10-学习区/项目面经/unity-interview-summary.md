---
tags: [reference, interview, unity, summary]
created: 2026-04-22
updated: 2026-06-20
source: 面经整理
---

## **引擎基础**
Unity的核心架构：Unity采用GameObject + Component的模式。
其中GameObject是场景中的实体容器，Component是可挂载在实体上的功能模块。
一个物体的行为由多个组件共同决定

**Unity基础类的继承关系**
![[Pasted image 20260422192857.png]]
System.Object->UnityEngine.Object->GameObject
UnityEngine.Object->Component->Behavior->MonoBehavior

MonoBehavior的作用体现在：
1）生命周期管理；
2）组件化架构；

**C#脚本代码与Unity引擎的交互原理**
Unity在运行模式提供了UnityEngine.dll库，在编辑模式下提供了UnityEditor.dll库。
C#脚本代码会先调用这两个库，然后再进一步调用Unity底层的C++代码

C#脚本继承MonoBehavior的作用是为了让脚本能够作为组件挂载在游戏实体上，同时也是为了能够使用生命周期函数。
生命周期函数并不属于 MonoBehaviour类。继承了MonoBehaviour的类能使用生命周期函数是因为，只有继承 MonoBehaviour的脚本，才会被 Unity 当作组件脚本管理，才能参与 Unity 的生命周期回调。
Unity通过反射机制获取脚本中注册的生命周期函数，并将其参与到生命周期的回调中。所以对于空的生命周期函数要将其清除。


总结常见系统的API使用
**Unity如果让物体旋转，传递什么参数**
```
欧拉角
transform.eulerAngles = new Vector3(0, 90, 0);
transform.Rotate(new Vector3(0, 10, 0));
四元数
transform.rotation = Quaternion.Euler(0, 90, 0);

transform.rotation 与 transform.localRotation 进行直接赋值需要传入Quaternion
transform.rotation = Quaternion.Euler(0, 90, 0);
transform.localRotation = Quaternion.Euler(30, 0, 0);
```
UI相关API

**物理系统常见API**
```
Rigidbody.AddForce()  //添加力，实现真实的物理移动
Rigidbody.velocity //直接改变速度，一般用于跳跃，角色控制
Rigidbody.MovePosition() //适合平滑移动
OnCollisionEnter()
OnTriggerEnter()
Physics.Raycast()
Rigidbody2D.AddForce()
Rigidbody2D.velocity
OnCollisionEnter2D()
OnTriggerEnter2D()
Physics2D.Raycast()
```
**两个物体发生触发Collision的条件，触发Trigger的条件**
- 两个物体都要有 Collider
- 两个物体的 Collider 都不能勾选 Is Trigger（触发Collision）其中一个勾选Is Trigger（触发Trigger）
- 两个物体里至少有一个物体挂了 Rigidbody

**动画系统常见API**
animation 与 animator
IK原理

**数据持久化常见API**
常见方法如下
- PlayPrefs
```
PlayPrefs.SetXXX(stirng key,int value): 设置变量值，XXX为类型，包括Int，Float，String
PlayPrefs.GetXXX(string key) : 获取变量值
PlayPrefs.HasKey(string key) : 查询键
PlayPrefs.DeleteAll() ：删除所有存储数据
PlayPrefs.Save() : 保存数据
```
- JSON序列化
```
JsonUtility.ToJson(object obj) : 将对象转换为Json数据
JsonUtility.FromJson<T>(stirng json) : 从Json数据中获取T类型实例
```
- 文件读写
```
Application.persistenDataPath:提供Unity持久化目录路径
File.WriteAllText(path,content) : 将字符串内容写入文件，如果文件不存在则创建，存在则覆盖内容
File.ReadAllText(path) : 一次性读取文件中的所有文本内容，常用于读取Json配置或存档
File.Exists(path) : 检查某个文件是否存在
File.Delete(path) : 删除指定文件
Directory.Exists(path) : 检查文件夹是否存在
Directory.CreateDirectory(path) : 创建文件夹，如果文件夹已经存在，也不会报错

```
通常小项目使用`PlayPrefs`进行存储
大项目使用Json + 文件读写+ `Application.persistenDataPath` 存储 

**输入系统常见API**
```
Input.GetKey
Input.GetKeyDown
Input.GetKeyUp
Input.GetMouseButtonDown
Input.mousePosition
Input.GetAxis
Input.GetAxisRaw
Input.touchCount
Input.GetTouch
```
**音/视频常见API**
```
AudioSource.Play()
AudioSource.Pause()
AudioSource.Stop()
AudioSource.PlayOneShot()
AudioSource.volume
AudioSource.loop
AudioSource.isPlaying

VideoPlayer.Play()
VideoPlayer.Pause()
VideoPlayer.Stop()
VideoPlayer.isPlaying
VideoPlayer.isLooping
VideoPlayer.time
VideoPlayer.loopPointReached
```
**资源管理常见API**
```
Resources.Load<T>()
Resources.LoadAll<T>()
Resources.UnloadAsset()
Resources.UnloadUnusedAssets()
Instantiate()
Destroy()
AssetBundle.LoadFromFile()
AssetBundle.LoadAsset<T>()
AssetBundle.Unload()
Addressables.LoadAssetAsync<T>()
Addressables.InstantiateAsync()
Addressables.Release()
```
## 引擎优化
### UGUI优化内容

UGUI渲染原理
1、网格重建/Graphic Rebuild ： 像Image、Text这类继承自Graphic的组件，会先将自己变为可绘制的顶点数据
2、Layout Rebuild ：决定每个UI元素绘制在哪，如果使用了自动布局组件，则Unity会先计算布局
3、Canvas Batch: Canvas会将它下面的所有可渲染UI收集起来，按层级和渲染顺序来整理。然后尝试进行批处理：材质相同、纹理相同、Shader相同、Mask/Stencil状态一致、顺序连续。满足条件的会被合并为一个批次，不满足就拆开
4、CanvasRenderer会把整理后的批次提交给底层渲染管线
5、一旦UI发生以下变化，就可能触发重建
-RectTransform变化
-文本内容变化
-图片变化
-颜色变化
-激活/隐藏
-层级变化
然后Unity在后续阶段继续做 Layout Rebuild -> Graphic Rebuild->Canvas Batch

**Canvas只影响其子节点，但不会影响子Canvas**

发生Rebuild的情况
1、重新计算一个Layout组件子节点的合适位置，或者可能的大小
2、Graphic发生了变化，比如：大小、旋转、文字的变化，图片的修改等

对于UGUI的优化方案如下:
1、动静分离，即尽量将更新频率高的UI元素放入一个Canvas中，将更新频率低的UI元素放入一个Canvas
2、少对Canvas中元素 增加/删除，显示/隐藏 ；少对Canvas中的元素进行颜色/材质/纹理等的改变
3、通过预加载UI模块来降低首次打开UI可能造成的卡顿
4、不直接关闭，设置UI界面为其他的Layer
5、按需开启Raycaster Target
6、对于不使用富文本的Text，取消勾选 Rich Text

> [!NOTE]
> 注：SetActive并不推荐频繁调用，每次对其的调用都会涉及OnEnable/OnDisable生命周期函数的调用，是一次完整的启用/停用流程。如果这个对象下面挂了很多 UI、组件、子节点，SetActive 是整批生效，不是只改当前节点。


除了Rebuild以外，需要进行优化的是UGUI的渲染问题
overdraw问题与图集问题
overdraw是指同一像素被多次绘制，尤其是在透明UI叠加时最明显。
优化方法为 1、减少UI重叠 2、隐藏被遮挡的界面 3、压缩透明区域
### DrawCall优化
Draw Call的定义：CPU向GPU发送的一次绘制指令。CPU在发送DrawCall之前需要收集信息

对于DrawCall的优化有以下几个方案
1、静态合批：对于静止（属性面板勾选Static）且具有相同材质的物体，静态批处理允许引擎对任意大小的几何物体进行批处理来降低绘制调用
2、动态合批：具有顶点数、属性格式等限制，适合小模型
动态批处理的约束:
- 动态批处理仅支持顶点数目小于900的网格物体
- 如果着色器使用位置，法线，uv值三种属性，那么只能批处理300顶点以下的物体；如果着色器需要使用顶点位置，法线，uv0，uv1和切向量，那么只能批处理180顶点以下的物体
- 统一缩放的物体不会与非统一缩放的物体进行批处理
- 使用不同材质的实例化物体（instance）将导致批处理失败
- 拥有lightmap的物体含有二外的材质属性，比如：lightmap的偏移与缩放参数等，所以拥有lightmap的物体将不会进行批处理（除非它们指向lightmap的同一部分）
- 多通道的shader会妨碍批处理操作 比如，几乎unity中所有的着色器在前向渲染中都支持多个光源，并为它们有效地开辟多个通道
3、GPU Instance：同网格，同材质 ， 同一 Shader 变体。
   支持差异位置 / 旋转 / 缩放、颜色、自定义实例属性
   常用于处理粒子，场景中的树木等


SRP Batch：并不减少DrawCall，减少的SetPass的数量

Draw Call、Batch、SetPass Call区别
 Draw Call 是 CPU向 GPU 发送的一次绘制命令
 Batch 是Unity 进行合批之后的批次数，目标是减少绘制提交次数
 SetPass Call 是一次材质/Shader渲染状态切换

### 内存优化
#### Unity的GC
Unity的GC是一种**非精准、不分代、不压缩**的GC

使用的是标记清除方法
标记：收集器从根对象开始遍历，对从根对象可以访问到的对象（正在运行的局部变量、静态变量、重写finalizer方法的变量、正在调用的函数传递的参数）的同步索引块标记为可达

清除：收集器会对堆内存从头到尾的进行线性遍历，如果发现某个对象没有标记则将其回收（回收之前会先执行它的Finalize()方法，如果重写了的话）

缺点：容易造成内存的碎片化

2019年Unity引入了增量GC，
优点：避免了出现GC时的CPU峰值，导致掉帧或者卡顿；
缺点：因为是分帧执行遍历，所以得采用写屏障（可能导致bug）；总GC时间变长

增量GC并不一定是项目的最优解：增量 GC 适合降低单帧卡顿，但会带来持续 CPU 开销和写屏障成本。如果项目本身分配量已经控制得很好，或者对象引用变化特别频繁，增量 GC 的收益可能不明显

GC的触发：1、向托管堆申请内存，但是托管堆中的连续内存空间不足
2、GC自动触发
3、手动调用GC

针对GC可以做的优化：
1、对于一些常用的变量进行缓存
2、使用对象池，减少频繁的对象创建与销毁
3、尽量避免使用闭包、匿名函数以及协程
4、对于字符串拼接注意频繁的拼接使用StringBulider
5、在一些对卡顿不敏感的情况下（如转换场景时），手动调用GC

手动设置GC的方法
```
GarbageCollector.GCMode = GarbageCollector.Mode.Manual; //设置纯手动GC
GarbageCollector.GCMode = GarbageCollector.Mode.Disable; //关闭GC
GarbageCollector.GCMode = GarbageCollector.Mode.Enable; //启动GC
```

> [!NOTE]
> 注意:Manual 不是关闭 GC，而是关闭自动 GC，把回收时机交给程序员；Disabled 才是彻底禁用 GC。两种模式下对象分配都还会继续，只是如果程序员不主动处理，托管堆都会持续增长。直到内存溢出，因为OOM进程被杀掉
> 

因此设置之后，就要小心的设置GC的回收时机
当设置手动GC后，就可以通过代码控制是立即执行GC还是进行增量GC。
```

void Update()
{
	//获取当前堆的大小
	long mem = Profiler.GetMonoUsedSizeLong();
	
	//立即触发GC
	System.GC.Collect(0);
	
	//触发增量式GC
	GarbageCollector.CollectIncremental();
}
```

 **Untiy的gc为什么不引入分代压缩，c#的gc指的是哪里的，从哪个版本引入分代和压缩的**
 C# 的 GC 不是语言自己的，而是 CLR/CoreCLR 运行时的 GC。.NET 从 CLR 1.0 开始就是分代 GC，小对象堆会压缩；大对象堆早期不压缩，直到 .NET Framework 4.5.1 才支持按需压缩。Unity 没直接采用这套机制，主要是因为要兼顾 Mono、IL2CPP、AOT、多平台和 C++ 引擎互操作，所以长期使用非分代、非压缩的 GC，并通过增量 GC 来降低停顿。
 
 - 分代 GC 需要写屏障，记录老对象对新对象的引用，否则只回收年轻代时可能漏标。
 - 压缩 GC 会移动对象，移动后必须更新所有引用。
 - Unity 有大量 C# 托管对象 和 C++ 引擎对象 互相绑定，移动对象会让引用维护更复杂。
 - Unity 要支持 IL2CPP、AOT、iOS、主机、移动端等多平台，不能完全依赖桌面 CLR 那套运行时机制。
 
#### Unity 内存监测 Profiler
Unity的内存分为三部分，
- 原生内存：Unity中的所有资源，比如贴图、模型、AB等都属于原生内存
- 托管内存：托管堆内存，所有非空引用类型的对象和已装箱值类型对象
- 非托管内存：无法被GC的内存，包括C#中启动不安全代码由C++分配的内存，需要通过程序手动释放,否则就会造成内存泄漏
```
unsafe void Test(){
	float[] arr = new float(5);
	//NativeXXX系列API是Unity提供的非托管对象，需要手动调用Dispose函数释放内存
	NativeArray<float> array = new NativeArray<float>(5,Allocator.Temp);
	array.Dispose();
	
	//分配内存
	void* allocs = UnsafeUtility.Malloc(4,4,Allocator.Temp);
	//释放内存
	UnsafeUtility.Free(allocs,Allocator.Temp);
}
```
Memory Profiler
支持快照截取与快照对比功能

Unity的原生内存使用时都有对应的C#的引用，由于托管堆中存储的的引用，因此内存很小，而资源的内存存储于原生内存中

**GC问题怎么定位**
通过Unity Profiler中，查找每帧 GC Alloc和 GC.Collect卡顿
**内存泄漏怎么定位**
内存泄漏看 Memory Profiler，通过操作前后快照对比找增长对象和引用链。
### 资源优化
音频资源设置
- **Force To Mono**:强制单声道。双声道音频在包体和内存中占用空间加倍。大部分音效（如脚步声、枪声、UI音效）左右声道数据实际相同，勾选后可节省约一半内存。背景音乐等需要立体声效果的除外
- **Compression Format**:不同的平台有不同的声音格式的支持,IOS对MP3有硬件支持,Android暂时没有硬件支持 建议IOS适合使用ADPCM和MP3格式,Android适合使用Vorbis格式
- **Load Type**:决定声音在内存中的存在形态:
	- **Decompress On Load**当audio clip被加载时,解压声音数据适用于小型音频文件(<200kb)
	- **Compressed In Memory**声音数据将以压缩的形式保存在内存当中适用于中型音频文件(>=200kb)
	- **Streaming**从磁盘读取声音数据适用于大型音频文件,例如背景音

> [!NOTE]
 注:例如Decompress On Load,要求文件必须小于200kb,因为内部内存管理的问题,如果是大于200kb的文件,那么也还是只会被分配到不足200kb的内存

- **Bitrate**:可以对音频文件本身进行压缩,降低文件的比特率(bitrate),前提音频品质不会被破坏太严重
## UnityShader总结

**Unity Shader、ShaderLab、HLSL/CG 之间是什么关系？**
ShaderLab 是 Unity shader 的外层配置语言，包括Properties、Tag等，
HLSL/CG 是里面真正写 GPU 计算逻辑的语言，从CGPROGRAM/HLSLPROGRAM开始到ENDCG/ENDHLSL之间的代码是HLSL/CG
UnityShader是指所有.shader的文件
什么是Tags？SubShader中的Tags和Pass中的Tags的区别？
SubShader Tags 决定“这个材质/物体整体怎么进入渲染系统”；Pass Tags 决定“这个 Pass 在渲染管线的哪个阶段被调用”。
**什么是语义，比如 POSITION、NORMAL、TEXCOORD0、SV_POSITION**
**Forward Rendering 和 Deferred Rendering 在 Shader 编写上有什么区别**

**Lambert 光照模型的核心公式是什么**
**Half-Lambert 相比 Lambert 解决了什么问题**
**Blinn-Phong 和 Phong 高光有什么区别**

**Unity渲染管线**
**Unity渲染批次**
**简单光照效果Shader的编写**
**一张图片渲染最终渲染到屏幕上的一个过程**
**深度测试，Pre-z与EarlyZ的区别**
**在Shader中使用if-else分支语句会发生什么，什么是Shader变体？**
## 其他补充知识

### Unity跨平台的原因（IL2CPP）
Unity之所以能够跨平台是因为Mono与IL2CPP

**AOT** : 提前编译 即程序运行之前，将.exe或.dll文件中的byte code部分转译为目标平台的原生码并且存储

**JIT** : 即时编译  类似java/C#的语言，会将源代码编译为中间语言(IL)，然后在虚拟机中运行时将其编译为相对应的native code，然后执行

**CIL与CLR**：.Net标准定义了所谓CIL（Common Intermediate Language 通用中间语言）与可以在各个平台运行的CLR（Common Language Runtime 通用语言运行）。
CIL就是中间语言，CLR就是虚拟机

**Mono** : Mono是实现了CIL与CLR标准的实现，核心是将各种高级语言编译为IL的编译器，以及运行IL的CLR。Unity的跨平台就是基于Mono，开发时写的各种类型的脚本语言都会被Mono的编译器编译为IL，然后在各个平台的Mono虚拟机上运行，这个就实现了跨平台机制

**IL2CPP** : 是Unity支持的另一种脚本后端机制。和mono不同的是，编译生成中间语言之后会将IL转换为C++，然后再通过各个平台的编译器编译成相应的native code

### 代码序列化
Unity会序列化与反序列化脚本的常用数据。脚本定义可序列化的属性，Unity将字段值保存到场景文件、Prefab文件、.asset文件等

一般来说只要使用public声明的属性，数据就会被Unity自动序列化。
不希望属性参加序列化可以使用属性
`[System.NonSerialized]` 
如果想要属性参与序列但不想显示在Inspector中可以使用属性
`[HideInInspector]`

Unity的序列化支持的类型不仅包括基础类型，如int,float,bool等值类型数据,还支持string数据和UnityObject对象。支持数组容器，但是不支持字典，不支持静态数据，不支持常量数据，不支持只读数据

虽然数据嵌套不支持，但是可以通过嵌套序列化做到。
```
[System.Serializable]
public class Data{
	public List<int> a;
}

public List<Data> array;//支持序列化
```

`[SerializeField]`是让某个字段能够被序列化
`[Serializeable]`是让一个自定义类或结构体类型被序列化


ScriptableObject是Unity提供的一种轻量级数据容器，主要用来存放配置数据，而不是挂在场景中的行为逻辑。
主要用于存储配置、解耦数据与逻辑和做数据共享。适合存放静态或可复用的数据


序列化的开销：主要是对象遍历(看对象的哪些字段需要序列化)、数据转换（将内存中的对象状态转换为可存储模式）、反序列化时的对象重建和引用恢复

### .meta文件
Unity的.meta文件本质上是资源的元数据文件。项目中每个资源文件或文件夹，Unity通常都会为它生成这个文件，用来记录这个资源在Unity工程中的身份信息（GUID）和导入配置（比如图片资源的设置等）

### Resource
Resource文件夹里的内容在游戏启动时就会全部加载到内存中构建索引，不可卸载。所以 Resource 越大，启动越慢、内存压力越大。建议使用 AssetBundle 或 Addressables 替代（Unity 官方也推荐按需加载而非依赖 Resources）

### LOD
LOD(Level of detail)多层次细节。是根据物体与摄像机的距离，动态调整物体的精细程度。当物体距离摄像机较远时，使用较低细节的模型，以减少渲染负担，从而提升性能

### UGUI相关
**UGUI的消息传递机制**
UGUI的消息传递机制主要依靠EventSystem，它负责接收输入事件，例如：鼠标、触摸、键盘、手柄，然后同故宫Raycaster找到当前被点击或悬停的UI对象，将事件分发给实现了对应事件接口的组件，流程为：
1、输入模块收集输入，常见为StandaloneInputModule或新输入系统InputSystemUIInputModule
2、Raycaster检测目标，UGUI常用GraphicRaycaster，它会从当前屏幕坐标发射UI射线，检测哪些Graphic组件被命中，比如：Image、Text、RawImage
3、EventSystem确定事件对象
根据射线检测结果，找到最上层、可交互、可接受射线的UI对象
4、ExecuteEvents分发事件

如果某些Image只是装饰，不需要接收点击，那么应该关闭Raycast Target。

**UI渲染顺序**
UGUI的渲染顺序主要由Canvas、Sorting Layer、Order in Layer、层级顺序等因素决定的，一般来说越靠后的UI越后绘制，也就显示在上面
不同Canvas之间 通过 Sorting Layer 与 Order in Layer进行排序
同一个Canvas下主要是看层级顺序

#### mask与RectMask原理
Mask组件与Image组件配合工作，根据Image/Text等UI组件的内容来定位显示范围，所有该组件的子级元素，超出此区域的部分会被隐藏（包括UI的交互事件）
Mask会赐予UI一个特殊的材质，这个材质会给UI的每个像素点进行标记，将标记结果存放在一个Stencil Buffer中；当子级UI进行渲染的时候就会去检查这个Stencil Buffer内的标记，如果当前覆盖的区域存在标记则进行渲染，否则不渲染

RectMask2D 不需要UI组件内容作为裁剪区域，它的裁剪区域永远是矩形大小，进而CPU计算元素是否在矩形区域之内，如果在，则节点下常规方式合批之后进行顶点裁剪，如果一个元素完全不在矩形区域，则这个元素不会被渲染

Mask内的元素和外面的元素不会合批（但是可以和其他Mask组件合批）
由于Mask需要先写入一次Stencil Buffer，所以会多一个写入的Draw Call

#### Image和rawimage
Image 显示 Sprite，适合常规 UI，并支持图集和九宫格
RawImage 直接显示 Texture，适合 RenderTexture、视频、相机画面这类原始纹理。

### 热更新的一些解决方案
热更新包含两个方面：
资源热更新：解决图片、配置、音频、特效等资源的热更新，使用AssetBundle包或Addressable
代码热更新：修复逻辑Bug，更新玩法逻辑，使用Lua、ILRuntime、HybridCLR
**AssetBundle加载**：从硬盘或者网络加载ab包到非托管内存，再从ab包内加载所需资源
```
//加载ab包
LoadFromFile() //从磁盘中加载ab包
LoadFromMemory() //从字节数组中加载ab包
UnityWebRequest()//从网络中加载ab包

//加载ab包中的资源
LoadAsset 同步加载资源
LoadAssetAsync 异步加载资源
```
**AssetBundle热更资源方式**
去游戏版本服务器获取当前游戏版本号，然后根据版本号下载对应的资源列表，并于本地资源进行对比，根据相差的资源下载资源包
**AssetBundle的依赖问题**
一个 AB 里的资源可能引用了另一个 AB 里的资源，加载时如果依赖包没加载，就可能出现资源丢
  失、材质丢失、贴图丢失、Prefab 引用为空等问题。
AssetBundle的依赖问题主要包括依赖加载、重复打包和卸载管理。解决方式是构建时通过AssetBundleManifest 记录依赖，运行时先加载依赖再加载主包，卸载时用引用计数控制，同时把公共资源独立打包，避免重复和提前卸载。
### 为什么c#不能热更新
Unity 默认 C# 不能热更新，主要是因为 C# 代码会在打包时编译进程序集，IL2CPP 下还会变成本地机器码，运行时不能直接替换源码执行；再加上 iOS 等平台限制 JIT，所以需要 Lua、ILRuntime、HybridCLR 这类方案来绕过或补足。


帧同步和状态同步，比如优缺点，应用场景，什么时候用，有什么问题怎么解决