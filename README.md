# MajdataEdit BatchOutput Tool
这是一个基于[MajdataEdit](https://github.com/LingFeng-bbben/MajdataEdit)所修改的用于批量导出谱面的工具。


## 使用方法
* 将本项目拉到本地，随后在Visual studio中以debug模式运行。
* 将[MajdataView](https://github.com/LingFeng-bbben/MajdataView)最新的release文件下载下来，然后将里面的所有内容复制到本项目中的\bin\Debug\net6.0-windows（第一步中运行了之后会自动生成bin文件夹）中。
* 修改本项目\MainWindow.xaml.cs文件中第289行的baseDir变量，将其改为您需要批量导出谱面的根目录（比如您有一个目录叫`E:\Maimai_map\21. FESTiVAL PLUS`，里面有很多子文件夹放着不同的谱面）
* 再次在Visual studio中以debug模式运行，运行后，点击左上角的“文件>更改分辨率”，就可以保证该分辨率可以正常录制。（如需要修改，请修改本项目\MainWindow.xaml.cs文件中第331行的代码）
* 最后点击左上角的“文件>全部导出”，就可以让其批量导出了。

## 注意事项
* 该项目在debug模式下运行并不会影响性能，因为MajdataEdit仅仅作为一个发送指令的工具，真正需要渲染视频的是MajdataView，我们避开了对MajdataView的修改，所以可以直接使用高性能的release版本。
* 批量导出的过程中，您的MajdataEdit窗口会卡住，这是正常现象，您可以观察Visual studio的“输出”窗口，查看是否正常导出；也可以查看MajdataView是否有正常运行。
* 导出的时候，一开始MajdataView和MajdataEdit都会卡顿，这是因为程序正在自动生成音乐文件，请稍微耐心等待。
* 导出一个谱面大约需要1分钟时间，您可以根据谱面数量来预估AFK的时间而去做其他的事情。

## 特殊需求
* 因为本项目设计的初衷是生成正解音谱面预览，所以您可以通过删除\bin\Debug\net6.0-windows文件夹中的SFX等文件来实现您的特殊需求。
* 另外本项目还提供了自动删除谱面bg.png以及将track.mp3进行静音的脚本文件\scripts\preprocess.py，用于生成纯粹只有正解音的谱面预览