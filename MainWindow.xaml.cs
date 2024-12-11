﻿using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DiscordRPC.Logging;
using MajdataEdit.AutoSaveModule;
using Microsoft.Win32;
using Un4seen.Bass;
using System.Runtime.InteropServices;
using MajdataEdit.SyntaxModule;
using Timer = System.Timers.Timer;

namespace MajdataEdit;

/// <summary>
///     MainWindow.xaml 的交互逻辑
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (Environment.GetCommandLineArgs().Contains("--ForceSoftwareRender"))
        {
            MessageBox.Show("正在以软件渲染模式运行\nソフトウェア・レンダリング・モードで動作\nBooting as software rendering mode.");
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        CheckAndStartView();

        TheWindow.Title = GetWindowsTitleString();

        SetWindowGoldenPosition();

        DCRPCclient.Logger = new ConsoleLogger { Level = LogLevel.Warning };
        DCRPCclient.Initialize();

        var handle = new WindowInteropHelper(this).Handle;
        Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_CPSPEAKERS, handle);
        InitWave();

        ReadSoundEffect();
        ReadEditorSetting();

        chartChangeTimer.Elapsed += ChartChangeTimer_Elapsed;
        chartChangeTimer.AutoReset = false;
        currentTimeRefreshTimer.Elapsed += CurrentTimeRefreshTimer_Elapsed;
        currentTimeRefreshTimer.Start();
        visualEffectRefreshTimer.Elapsed += VisualEffectRefreshTimer_Elapsed;
        waveStopMonitorTimer.Elapsed += WaveStopMonitorTimer_Elapsed;
        playbackSpeedHideTimer.Elapsed += PlbHideTimer_Elapsed;

        if (editorSetting!.AutoCheckUpdate) CheckUpdate(true);

        #region 异常退出处理

        if (!SafeTerminationDetector.Of().IsLastTerminationSafe())
        {
            // 若上次异常退出，则询问打开恢复窗口
            var result = MessageBox.Show(GetLocalizedString("AbnormalTerminationInformation"),
                GetLocalizedString("Attention"), MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                var lastEditPath = File.ReadAllText(SafeTerminationDetector.Of().RecordPath).Trim();
                if (lastEditPath.Length != 0)
                    // 尝试打开上次未正常关闭的谱面 然后再打开恢复页面
                    try
                    {
                        initFromFile(lastEditPath);
                    }
                    catch (Exception error)
                    {
                        Console.WriteLine(error.StackTrace);
                    }

                Menu_AutosaveRecover_Click(new object(), new RoutedEventArgs());
            }
        }

        SafeTerminationDetector.Of().RecordProgramClose();

        #endregion
    }


    //start the view and wait for boot, then set window pos
    private void SetWindowPosTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        var setWindowPosTimer = (Timer)sender!;
        Dispatcher.Invoke(() => { InternalSwitchWindow(); });
        setWindowPosTimer.Stop();
        setWindowPosTimer.Dispose();
    }

    //Window events
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!isSaved)
            if (!AskSave())
            {
                e.Cancel = true;
                return;
            }

        var process = Process.GetProcessesByName("MajdataView");
        if (process.Length > 0)
        {
            var result = MessageBox.Show(GetLocalizedString("AskCloseView"), GetLocalizedString("Attention"),
                MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
                process[0].Kill();
        }

        currentTimeRefreshTimer.Stop();
        visualEffectRefreshTimer.Stop();

        soundSetting.Close();
        //if (bpmtap != null) { bpmtap.Close(); }
        //if (muriCheck != null) { muriCheck.Close(); }
        SaveSetting();

        Bass.BASS_ChannelStop(bgmStream);
        Bass.BASS_StreamFree(bgmStream);
        Bass.BASS_ChannelStop(answerStream);
        Bass.BASS_StreamFree(answerStream);
        Bass.BASS_ChannelStop(breakStream);
        Bass.BASS_StreamFree(breakStream);
        Bass.BASS_ChannelStop(judgeExStream);
        Bass.BASS_StreamFree(judgeExStream);
        Bass.BASS_ChannelStop(hanabiStream);
        Bass.BASS_StreamFree(hanabiStream);
        Bass.BASS_Stop();
        Bass.BASS_Free();

        // 正常退出
        SafeTerminationDetector.Of().RecordProgramClose();
    }

    //Window grid events
    private void Grid_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
    }

    private void Grid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            //Console.WriteLine(e.Data.GetData(DataFormats.FileDrop).ToString());
            if (e.Data.GetData(DataFormats.FileDrop).ToString() == "System.String[]")
            {
                var path = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
                if (path.ToLower().Contains("maidata.txt"))
                {
                    if (!isSaved)
                        if (!AskSave())
                            return;
                    var fileInfo = new FileInfo(path);
                    initFromFile(fileInfo.DirectoryName!);
                }
            }
    }

    private void FindClose_MouseDown(object sender, MouseButtonEventArgs e)
    {
        FindGrid.Visibility = Visibility.Collapsed;
        FumenContent.Focus();
    }

    #region MENU BARS

    private void Menu_New_Click(object sender, RoutedEventArgs e)
    {
        if (!isSaved)
            if (!AskSave())
                return;
        var openFileDialog = new OpenFileDialog
        {
            Filter = "track.mp3, track.ogg|track.mp3;track.ogg"
        };
        if ((bool)openFileDialog.ShowDialog()!)
        {
            var fileInfo = new FileInfo(openFileDialog.FileName);
            CreateNewFumen(fileInfo.DirectoryName!);
            initFromFile(fileInfo.DirectoryName!);
        }
    }

    private void Menu_Open_Click(object sender, RoutedEventArgs e)
    {
        if (!isSaved)
            if (!AskSave())
                return;
        var openFileDialog = new OpenFileDialog
        {
            Filter = "maidata.txt|maidata.txt"
        };
        if ((bool)openFileDialog.ShowDialog()!)
        {
            var fileInfo = new FileInfo(openFileDialog.FileName);
            Debug.WriteLine(openFileDialog.FileName);
            Debug.WriteLine(fileInfo.DirectoryName!);
            initFromFile(fileInfo.DirectoryName!);
        }
    }

    private void Menu_Save_Click(object sender, RoutedEventArgs e)
    {
        SaveFumen(true);
        SystemSounds.Beep.Play();
    }

    private void Menu_SaveAs_Click(object sender, RoutedEventArgs e)
    {
    }

    private void Menu_ExportRender_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayAndPause(PlayMethod.Record);
    }

    static bool IsFfmpegFinished(string folderPath)
    {
        string filePath = Path.Combine(folderPath, "out.mp4");

        // 如果文件不存在，直接返回 false
        if (!File.Exists(filePath))
        {
            return false;
        }

        // 获取文件的最后修改时间
        DateTime lastWriteTime = File.GetLastWriteTime(filePath);
        Console.WriteLine($"File last write time: {lastWriteTime}");

        // 等待文件修改时间不再改变，假设我们检查两次之间修改时间没有变化，则认为文件完成
        Thread.Sleep(2000); // 等待2秒
        DateTime newWriteTime = File.GetLastWriteTime(filePath);

        // 如果两次修改时间相同，说明文件没有再被写入
        return lastWriteTime == newWriteTime;
    }

    // 关闭文件资源管理器窗口
    static void CloseExplorerWindow(string folderPath)
    {
        // 获取当前所有正在运行的进程
        var processes = Process.GetProcessesByName("explorer");
        string subDirName = Path.GetFileName(folderPath);
        // 查找与目标文件夹路径相关的进程
        foreach (var process in processes)
        {
            try
            {
                Debug.WriteLine(process.MainWindowTitle);
                // 检查窗口标题是否包含文件夹路径
                if (process.MainWindowTitle.Contains(subDirName))
                {
                    Debug.WriteLine($"Closing explorer window for: {subDirName}");
                    process.Kill();  // 关闭文件资源管理器进程
                    break;
                }
            }
            catch (Exception ex)
            {
                // 忽略访问错误
                Console.WriteLine($"Error closing explorer: {ex.Message}");
            }
        }
    }

    private void Menu_AllExport_Click(object sender, RoutedEventArgs e)
    {
		/*string filedir = "E:\\Maimai_map\\test\\Churros Parlor";
		initFromFile(filedir);
		LevelSelector.SelectedIndex = 4;
		TogglePlayAndPause(PlayMethod.Record);*/

        string baseDir = @"E:\Maimai_map\test";  // 基础目录
        string[] subDirs = Directory.GetDirectories(baseDir);  // 获取所有子目录

        foreach (string subDir in subDirs)
        {
            Debug.WriteLine($"Processing folder: {subDir}");
            initFromFile(subDir);  // 对每个子目录执行操作
            LevelSelector.SelectedIndex = 4;
            TogglePlayAndPause(PlayMethod.Record);  // 播放录制操作

            // 检查是否达到EditorControlMethod.Start状态，如果是则跳到下一个文件夹
            while (true)
            {
                // 每2秒检查一次当前的控制方法
                if (IsFfmpegFinished(subDir))
                {
                    Debug.WriteLine("FFmpeg has finished processing the video. Moving to next folder...");
                    Thread.Sleep(2000);
                    CloseExplorerWindow(subDir);
                    break;  // 如果状态是Start，跳出当前循环，处理下一个文件夹
                }

                // 如果还未结束，等待2秒继续检查
                Thread.Sleep(2000);
            }
            ToggleStop();
        }

        Debug.WriteLine("All folders processed.");

    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int width, int height, uint flags);

    // 常量定义
    private static readonly IntPtr HWND_TOP = new IntPtr(0);
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOMOVE = 0x0002;

    private void Menu_Change_Click(object sender, RoutedEventArgs e)
    {
        var process = Process.GetProcessesByName("MajdataView");
        SetWindowPos(process[0].MainWindowHandle, HWND_TOP, 0, 0, 800, 800, SWP_NOZORDER | SWP_NOMOVE);
    }

    private void MirrorLeftRight_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var result = Mirror.NoteMirrorHandle(FumenContent.Selection.Text, Mirror.HandleType.LRMirror);
        FumenContent.Selection.Text = result;
    }

    private void MirrorUpDown_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var result = Mirror.NoteMirrorHandle(FumenContent.Selection.Text, Mirror.HandleType.UDMirror);
        FumenContent.Selection.Text = result;
    }

    private void Mirror180_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var result = Mirror.NoteMirrorHandle(FumenContent.Selection.Text, Mirror.HandleType.HalfRotation);
        FumenContent.Selection.Text = result;
    }

    private void Mirror45_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var result = Mirror.NoteMirrorHandle(FumenContent.Selection.Text, Mirror.HandleType.Rotation45);
        FumenContent.Selection.Text = result;
    }

    private void MirrorCcw45_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var result = Mirror.NoteMirrorHandle(FumenContent.Selection.Text, Mirror.HandleType.CcwRotation45);
        FumenContent.Selection.Text = result;
    }

    private void BPMtap_MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var tap = new BPMtap();
        tap.Owner = this;
        tap.Show();
    }

    private void MenuItem_InfomationEdit_Click(object? sender, RoutedEventArgs e)
    {
        var infoWindow = new Infomation();
        SetSavedState(false);
        infoWindow.ShowDialog();
        TheWindow.Title = GetWindowsTitleString(SimaiProcess.title!);
    }

    private void MenuItem_Majnet_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo() { FileName = "https://majdata.net", UseShellExecute = true });
        //maidata.txtの譜面書式
    }

    private void MenuItem_GitHub_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo() { FileName = "https://github.com/LingFeng-bbben/MajdataView", UseShellExecute = true });
    }

    private void MenuItem_SoundSetting_Click(object? sender, RoutedEventArgs e)
    {
        soundSetting = new SoundSetting
        {
            Owner = this
        };
        soundSetting.ShowDialog();
    }

    private void MuriCheck_Click_1(object? sender, RoutedEventArgs e)
    {
        var muriCheck = new MuriCheck
        {
            Owner = this
        };
        muriCheck.Show();
    }
    private void SyntaxCheckButton_Click(object sender, RoutedEventArgs e)
    {
        ShowErrorWindow();
    }
    void ShowErrorWindow()
    {
        var mcrWindow = new MuriCheckResult
        {
            Owner = this
        };
        var errList = SyntaxChecker.ErrorList;
        errList.ForEach(e =>
        {
            e.positionY--;
            mcrWindow.errorPosition.Add(e);
            var eRow = new ListBoxItem
            {
                Content = e.eMessage,
                Name = "rr" + mcrWindow.CheckResult_Listbox.Items.Count
            };
            eRow.AddHandler(PreviewMouseDoubleClickEvent,
                new MouseButtonEventHandler(mcrWindow.ListBoxItem_PreviewMouseDoubleClick));
            mcrWindow.CheckResult_Listbox.Items.Add(eRow);
        });
        mcrWindow.Show();
    }
    private void SyntaxCheckButton_Click(object sender, MouseButtonEventArgs e)
    {
        ShowErrorWindow();
    }
    private void MenuItem_EditorSetting_Click(object? sender, RoutedEventArgs e)
    {
        var esp = new EditorSettingPanel
        {
            Owner = this
        };
        esp.ShowDialog();
    }

    private void Menu_ResetViewWindow(object? sender, RoutedEventArgs e)
    {
        if (CheckAndStartView()) return;
        InternalSwitchWindow();
    }

    private void MenuFind_Click(object? sender, RoutedEventArgs e)
    {
        if (FindGrid.Visibility == Visibility.Collapsed)
        {
            FindGrid.Visibility = Visibility.Visible;
            InputText.Focus();
        }
        else
        {
            FindGrid.Visibility = Visibility.Collapsed;
        }
    }

    private void CheckUpdate_Click(object? sender, RoutedEventArgs e)
    {
        CheckUpdate();
    }

    private void Menu_AutosaveRecover_Click(object? sender, RoutedEventArgs e)
    {
        var asr = new AutoSaveRecover
        {
            Owner = this
        };
        asr.ShowDialog();
    }

    #endregion

    #region 快捷键

    private void PlayAndPause_CanExecute(object? sender, CanExecuteRoutedEventArgs e) //快捷键
    {
        TogglePlayAndStop();
    }

    private void StopPlaying_CanExecute(object? sender, CanExecuteRoutedEventArgs e) //快捷键
    {
        TogglePlayAndPause();
    }

    private void SaveFile_Command_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        SaveFumen(true);
        SystemSounds.Beep.Play();
    }

    private void SendToView_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        TogglePlayAndStop(PlayMethod.Op);
    }

    private void IncreasePlaybackSpeed_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        if (Bass.BASS_ChannelIsActive(bgmStream) == BASSActive.BASS_ACTIVE_PLAYING) return;
        var speed = GetPlaybackSpeed();
        Console.WriteLine(speed);
        speed += 0.25f;
        PlbSpdLabel.Content = speed * 100 + "%";
        SetPlaybackSpeed(speed);
        PlbSpdAdjGrid.Visibility = Visibility.Visible;
        playbackSpeedHideTimer.Stop();
        playbackSpeedHideTimer.Start();
    }

    private void DecreasePlaybackSpeed_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        if (Bass.BASS_ChannelIsActive(bgmStream) == BASSActive.BASS_ACTIVE_PLAYING) return;
        var speed = GetPlaybackSpeed();
        Console.WriteLine(speed);
        speed -= 0.25f;
        if (speed < 1e-6) return; // Interrupt if it's an epsilon or lower.
        PlbSpdLabel.Content = speed * 100 + "%";
        SetPlaybackSpeed(speed);
        PlbSpdAdjGrid.Visibility = Visibility.Visible;
        playbackSpeedHideTimer.Stop();
        playbackSpeedHideTimer.Start();
    }

    private readonly Timer playbackSpeedHideTimer = new(1000);

    private void PlbHideTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        Dispatcher.Invoke(() => { PlbSpdAdjGrid.Visibility = Visibility.Collapsed; });
        ((Timer)sender!).Stop();
    }

    private void FindCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        if (FindGrid.Visibility == Visibility.Collapsed)
        {
            FindGrid.Visibility = Visibility.Visible;
            InputText.Focus();
        }
        else
        {
            FindGrid.Visibility = Visibility.Collapsed;
        }
    }

    private void MirrorLRCommand_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        MirrorLeftRight_MenuItem_Click(sender, null);
    }

    private void MirrorUDCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        MirrorUpDown_MenuItem_Click(sender, null);
    }

    private void Mirror180Command_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        Mirror180_MenuItem_Click(sender, null);
    }

    private void Mirror45Command_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        Mirror45_MenuItem_Click(sender, null);
    }

    private void MirrorCcw45Command_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        MirrorCcw45_MenuItem_Click(sender, null);
    }

    #endregion

    #region Left componients

    private void PlayAndPauseButton_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayAndPause();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleStop();
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var i = LevelSelector.SelectedIndex;
        if (i == -1) i = 0;
        Console.WriteLine(i);
        SetRawFumenText(SimaiProcess.fumens[i]);
        selectedDifficulty = i;
        LevelTextBox.Text = SimaiProcess.levels[selectedDifficulty];
        SetSavedState(true);
        SimaiProcess.Serialize(GetRawFumenText());
        DrawWave();
        SyntaxCheck();
    }

    private void LevelTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SetSavedState(false);
        if (selectedDifficulty == -1) return;
        SimaiProcess.levels[selectedDifficulty] = LevelTextBox.Text;
    }

    private void OffsetTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SetSavedState(false);
        try
        {
            SimaiProcess.first = float.Parse(OffsetTextBox.Text);
            SimaiProcess.Serialize(GetRawFumenText());
            DrawWave();
        }
        catch
        {
            SimaiProcess.first = 0f;
        }
    }

    private void OffsetTextBox_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var offset = float.Parse(OffsetTextBox.Text);
        offset += e.Delta > 0 ? 0.01f : -0.01f;
        OffsetTextBox.Text = offset.ToString();
    }

    private void FollowPlayCheck_Click(object sender, RoutedEventArgs e)
    {
        FumenContent.Focus();
    }

    private void Op_Button_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayAndStop(PlayMethod.Op);
    }

    private void SettingLabel_MouseUp(object sender, MouseButtonEventArgs e)
    {
        // 单击设置的时候也可以进入设置界面
        var esp = new EditorSettingPanel();
        esp.Owner = this;
        esp.ShowDialog();
    }
    #endregion

    #region RichTextbox events

    private void FumenContent_SelectionChanged(object sender, RoutedEventArgs e)
    {
        NoteNowText.Content = "" + (
            new TextRange(FumenContent.Document.ContentStart, FumenContent.CaretPosition).Text.Replace("\r", "")
                .Count(o => o == '\n') + 1) + " 行";
        if (Bass.BASS_ChannelIsActive(bgmStream) == BASSActive.BASS_ACTIVE_PLAYING && (bool)FollowPlayCheck.IsChecked!)
            return;
        //TODO:这个应该换成用fumen text position来在已经serialized的timinglist里面找。。 然后直接去掉这个double的返回和position的入参。。。
        var time = SimaiProcess.Serialize(GetRawFumenText(), GetRawFumenPosition());

        //按住Ctrl，同时按下鼠标左键/上下左右方向键时，才改变进度，其他包含Ctrl的组合键不影响进度。
        if (Keyboard.Modifiers == ModifierKeys.Control && (
                Mouse.LeftButton == MouseButtonState.Pressed ||
                Keyboard.IsKeyDown(Key.Left) ||
                Keyboard.IsKeyDown(Key.Right) ||
                Keyboard.IsKeyDown(Key.Up) ||
                Keyboard.IsKeyDown(Key.Down)
            ))
        {
            if (Bass.BASS_ChannelIsActive(bgmStream) == BASSActive.BASS_ACTIVE_PLAYING)
                TogglePause();
            SetBgmPosition(time);
        }

        //Console.WriteLine("SelectionChanged");
        SimaiProcess.ClearNoteListPlayedState();
        ghostCusorPositionTime = (float)time;
        if (!isPlaying) DrawWave();
    }

    private void FumenContent_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (GetRawFumenText() == "" || isLoading) return;
        SetSavedState(false);
        if (chartChangeTimer.Interval < 33)
        {
            SimaiProcess.Serialize(GetRawFumenText(), GetRawFumenPosition());
            DrawWave();
        }
        else
        {
            chartChangeTimer.Stop();
            chartChangeTimer.Start();
        }
    }

    private void FumenContent_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 按下Insert键，同时未按下任何组合键，切换覆盖模式
        if (e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.None)
        {
            SwitchFumenOverwriteMode();
            e.Handled = true;
        }
    }

    #endregion

    #region Wave displayer

    private void WaveViewZoomIn_Click(object sender, RoutedEventArgs e)
    {
        if (deltatime > 1)
            deltatime -= 1;
        DrawWave();
        FumenContent.Focus();
    }

    private void WaveViewZoomOut_Click(object sender, RoutedEventArgs e)
    {
        if (deltatime < 10)
            deltatime += 1;
        DrawWave();
        FumenContent.Focus();
    }

    private void MusicWave_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollWave(-e.Delta);
    }

    private void MusicWave_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        lastMousePointX = e.GetPosition(this).X;
    }

    private void MusicWave_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var delta = e.GetPosition(this).X - lastMousePointX;
            lastMousePointX = e.GetPosition(this).X;
            ScrollWave(-delta);
        }

        lastMousePointX = e.GetPosition(this).X;
    }

    private void MusicWave_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        InitWave();
        DrawWave();
    }


    #endregion

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {

    }
}