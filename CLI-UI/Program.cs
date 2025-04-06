using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

class Program
{
    #region Win32 API
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    private const uint FO_DELETE = 0x0003;
    private const uint FOF_ALLOWUNDO = 0x0040;
    #endregion

    #region 全局状态
    private static string _currentPath = "Desktop";
    private static int _selection;
    private static int _page;
    private static bool _refreshFlag;
    #endregion

    static void Main(string[] args)
    {
        Console.Title = "CLI 文件管理器";
        Console.CursorVisible = false;

        while (true)
        {
            try
            {
                if (_refreshFlag)
                {
                    _refreshFlag = false;
                    continue;
                }

                Console.Clear();
                switch (_currentPath)
                {
                    case "Desktop":
                        ShowDesktop();
                        break;
                    case "ThisPC":
                        ShowDrives();
                        break;
                    default:
                        BrowseDirectory();
                        break;
                }
            }
            catch (Exception ex)
            {
                ShowError("运行时错误", ex);
                ResetToDesktop();
            }
        }
    }

    #region 核心功能

    static void ShowError(string title, Exception ex, string path = null)
    {
        var content = new List<string>
        {
            $"路径: {path ?? "N/A"}",
            $"错误代码: 0x{ex.HResult:X8}",
            $"类型: {ex.GetType().Name}",
            $"消息: {ex.Message}"
        };
        ShowMenu(title, string.Join("\n", content), "确定");
    }
    #endregion

    #region 统一菜单系统
    static int ShowMenu(string title, string content, params string[] options)
    {
        int selection = 0;
        ConsoleKey key;

        do
        {
            Console.Clear();
            Console.WriteLine(title);
            Console.WriteLine(new string('═', 60));
            Console.WriteLine(content);
            Console.WriteLine(new string('═', 60));

            for (int i = 0; i < options.Length; i++)
            {
                Console.Write(i == selection ? "> " : "  ");
                Console.WriteLine($"{options[i]}");
            }

            key = Console.ReadKey(true).Key;

            switch (key)
            {
                case ConsoleKey.UpArrow:
                    selection = Math.Max(selection - 1, 0);
                    break;
                case ConsoleKey.DownArrow:
                    selection = Math.Min(selection + 1, options.Length - 1);
                    break;
            }
        } while (key != ConsoleKey.Enter && key != ConsoleKey.Escape);

        return key == ConsoleKey.Escape ? -1 : selection;
    }
    #endregion

    #region 桌面界面
    static void ResetToDesktop()
    {
        _currentPath = "Desktop";
        _selection = 0;
        _page = 0;
        _refreshFlag = true;
    }
    static void ShowDesktop()
    {
        var items = new List<FileItem>
        {
            new FileItem("[系统] 此电脑", "ThisPC"),
            new FileItem("[系统] 回收站", "RecycleBin"),
            new FileItem($"[用户] {Environment.UserName}",
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        };

        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            items.AddRange(GetFileItems(desktopPath));
        }
        catch (Exception ex)
        {
            ShowError("桌面加载失败", ex);
        }

        HandleNavigation(items, "桌面");
    }
    #endregion

    #region 文件浏览器
    static void BrowseDirectory()
    {
        var parentPath = Directory.GetParent(_currentPath)?.FullName;

        var items = new List<FileItem>
        {
            new FileItem("[返回上级]",
                string.IsNullOrEmpty(parentPath) ? "ThisPC" : parentPath)
        };

        try { items.AddRange(GetFileItems(_currentPath)); }
        catch (Exception ex)
        {
            ShowError("目录访问失败", ex, _currentPath);
            ResetToDesktop();
            return;
        }

        HandleNavigation(items, _currentPath);
    }

    static IEnumerable<FileItem> GetFileItems(string path)
    {
        return Directory.GetDirectories(path).Select(d => new FileItem($"[目录] {Path.GetFileName(d)}", d))
            .Concat(Directory.GetFiles(path).Select(f => new FileItem($"[文件] {Path.GetFileName(f)}", f)));
    }
    #endregion

    #region 导航系统
    static void HandleNavigation(List<FileItem> items, string title)
    {
        const int pageSize = 20;
        int pageCount = (int)Math.Ceiling(items.Count / (double)pageSize);
        var pageItems = items.Skip(_page * pageSize).Take(pageSize).ToList();

        ConsoleKey key;
        bool shouldExit = false;

        do
        {
            Console.Clear();
            Console.WriteLine(title);
            Console.WriteLine(new string('═', 60));
            Console.WriteLine($"页码: {_page + 1}/{pageCount}");
            Console.WriteLine(new string('═', 60));

            for (int i = 0; i < pageItems.Count; i++)
            {
                Console.Write(i == _selection ? "> " : "  ");
                Console.WriteLine(pageItems[i].Display);
            }

            Console.WriteLine(new string('═', 60));
            Console.WriteLine("方向键导航  D桌面  P此电脑  Enter打开  Del删除  N新建");

            key = Console.ReadKey(true).Key;
            shouldExit = HandleNavKey(key, pageItems, pageCount);

        } while (!shouldExit && key != ConsoleKey.Escape);
    }

    static bool HandleNavKey(ConsoleKey key, List<FileItem> items, int pageCount)
    {
        switch (key)
        {
            case ConsoleKey.UpArrow:
                _selection = Math.Max(_selection - 1, 0);
                return false;
            case ConsoleKey.DownArrow:
                _selection = Math.Min(_selection + 1, items.Count - 1);
                return false;
            case ConsoleKey.LeftArrow:
                _page = Math.Max(_page - 1, 0);
                _selection = 0;
                _refreshFlag = true;
                return false;
            case ConsoleKey.RightArrow:
                _page = Math.Min(_page + 1, pageCount - 1);
                _selection = 0;
                _refreshFlag = true;
                return false;
            case ConsoleKey.Enter:
                var oldPath = _currentPath;
                ProcessSelection(items[_selection]);
                return _currentPath != oldPath;
            case ConsoleKey.Delete:
                HandleDelete(items[_selection].Path);
                return false;
            case ConsoleKey.N:
                ShowCreateMenu();
                return false;
            case ConsoleKey.D:
                ResetToDesktop();
                return true;
            case ConsoleKey.P:
                ResetToThisPC();
                return true;
            default:
                return false;
        }
    }
    #endregion

    #region 文件操作
    static void ProcessSelection(FileItem item)
    {
        if (item.Path == "RecycleBin")
        {
            Process.Start("explorer.exe", "shell:RecycleBinFolder");
            return;
        }

        if (Directory.Exists(item.Path))
        {
            // 设置刷新标记并更新路径
            _currentPath = item.Path;
            _selection = 0;
            _page = 0;
            _refreshFlag = true; // 新增刷新标记
        }
        else if (File.Exists(item.Path))
        {
            ShowFileMenu(item.Path);
        }
    }

    static void ShowFileMenu(string path)
    {
        int choice = ShowMenu($"文件操作: {Path.GetFileName(path)}",
            "选择要执行的操作：",
            "查看内容",
            "编辑文件",
            "用默认程序打开");

        switch (choice)
        {
            case 0:
                ShowFileContent(path);
                break;
            case 1:
                EditFile(path);
                break;
            case 2:
                OpenWithDefaultProgram(path);
                break;
        }
    }
    #endregion

    #region 文件内容操作
    static void ShowFileContent(string path)
    {
        try
        {
            var content = File.ReadAllText(path);
            if (string.IsNullOrEmpty(content)) content = "<空文件>";
            ShowMenu("文件内容", content, "返回");
        }
        catch (Exception ex)
        {
            ShowError("读取失败", ex, path);
        }
    }

    static void OpenWithDefaultProgram(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError("打开失败", ex, path);
        }
    }
    #endregion

    #region 编辑器模块（新增HandleTextInput）
    static void EditFile(string path)
    {
        List<string> lines;
        try
        {
            lines = File.ReadAllLines(path).ToList();
            if (lines.Count == 0) lines.Add("");
        }
        catch
        {
            lines = new List<string> { "" };
        }

        int cursorLine = 0;
        int cursorCol = 0;
        bool modified = false;

        while (true)
        {
            Console.Clear();
            Console.WriteLine($"编辑文件: {Path.GetFileName(path)} {(modified ? "*" : "")}");
            Console.WriteLine(new string('═', 60));

            for (int i = 0; i < lines.Count; i++)
            {
                if (i == cursorLine)
                {
                    Console.BackgroundColor = ConsoleColor.DarkBlue;
                    Console.ForegroundColor = ConsoleColor.White;
                }
                Console.WriteLine($"{i + 1,4} │ {lines[i]}");
                Console.ResetColor();
            }

            Console.WriteLine(new string('═', 60));
            Console.WriteLine("方向键移动  Alt+S保存  Esc退出");

            Console.SetCursorPosition(cursorCol + 7, cursorLine + 2);
            var key = Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow when cursorLine > 0:
                    cursorLine--;
                    break;
                case ConsoleKey.DownArrow when cursorLine < lines.Count - 1:
                    cursorLine++;
                    break;
                case ConsoleKey.LeftArrow when cursorCol > 0:
                    cursorCol--;
                    break;
                case ConsoleKey.RightArrow when cursorCol < lines[cursorLine].Length:
                    cursorCol++;
                    break;
                case ConsoleKey.S when key.Modifiers == ConsoleModifiers.Alt:
                    File.WriteAllLines(path, lines);
                    modified = false;
                    ShowMenu("保存成功", "文件已保存", "确定");
                    break;
                case ConsoleKey.Escape:
                    if (modified && ShowMenu("未保存更改", "要保存修改吗？", "保存并退出", "放弃修改") == 0)
                    {
                        File.WriteAllLines(path, lines);
                    }
                    return;
                default:
                    HandleTextInput(key, ref lines, ref cursorLine, ref cursorCol, ref modified);
                    break;
            }
        }
    }

    static void HandleTextInput(ConsoleKeyInfo key, ref List<string> lines,
        ref int cursorLine, ref int cursorCol, ref bool modified)
    {
        if (char.IsControl(key.KeyChar)) return;

        // 处理普通字符输入
        if (cursorLine >= lines.Count) lines.Add("");
        lines[cursorLine] = lines[cursorLine].Insert(cursorCol, key.KeyChar.ToString());
        cursorCol++;
        modified = true;

        // 自动换行处理
        if (cursorCol >= Console.WindowWidth - 8)
        {
            lines.Insert(cursorLine + 1, lines[cursorLine].Substring(Console.WindowWidth - 8));
            lines[cursorLine] = lines[cursorLine].Substring(0, Console.WindowWidth - 8);
            cursorLine++;
            cursorCol = 0;
        }
    }
    #endregion

    #region 删除系统
    static void HandleDelete(string path)
    {
        int choice = ShowMenu("删除确认",
            Directory.Exists(path)
                ? $"{path}\n包含 {CountItems(path)} 个项目"
                : path,
            "移动到回收站",
            "永久删除",
            "取消");

        if (choice < 0 || choice == 2) return;

        try
        {
            if (choice == 0) MoveToRecycleBin(path);
            else if (choice == 1) PermanentDelete(path);
            _refreshFlag = true;
        }
        catch (Exception ex)
        {
            ShowError("删除失败", ex, path);
        }
    }

    static int CountItems(string path)
    {
        try
        {
            return Directory.GetFileSystemEntries(path, "*", SearchOption.AllDirectories).Length;
        }
        catch
        {
            return 0;
        }
    }

    static void MoveToRecycleBin(string path)
    {
        SHFILEOPSTRUCT fs = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + '\0',
            fFlags = (ushort)FOF_ALLOWUNDO
        };
        SHFileOperation(ref fs);
    }

    static void PermanentDelete(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
        else File.Delete(path);
    }
    #endregion

    #region 新建系统
    static void ShowCreateMenu()
    {
        int choice = ShowMenu("新建项目", "选择要创建的类型：", "文件", "目录", "取消");
        if (choice < 0 || choice == 2) return;

        string name = GetInput("新建名称", "请输入名称：");
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowMenu("错误", "名称不能为空", "确定");
            return;
        }

        try
        {
            string path = Path.Combine(_currentPath, name);
            if (choice == 0)
            {
                File.WriteAllText(path, "\n");
                ShowMenu("成功", $"文件 {name} 已创建", "确定");
            }
            else
            {
                Directory.CreateDirectory(path);
                ShowMenu("成功", $"目录 {name} 已创建", "确定");
            }
            _refreshFlag = true;
        }
        catch (Exception ex)
        {
            ShowError("创建失败", ex, _currentPath);
        }
    }

    static string GetInput(string title, string prompt)
    {
        Console.Clear();
        Console.WriteLine(title);
        Console.WriteLine(new string('═', 60));
        Console.Write(prompt);
        return Console.ReadLine();
    }
    #endregion

    #region 驱动器界面

    static void ResetToThisPC()
    {
        _currentPath = "ThisPC";
        _selection = 0;
        _page = 0;
        _refreshFlag = true;
    }
    static void ShowDrives()
    {
        var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
        var items = drives.Select(d => new FileItem(
            $"[驱动器] {d.Name} ({FormatSize(d.TotalFreeSpace)}/{FormatSize(d.TotalSize)} free)",
            d.RootDirectory.FullName)).ToList();

        HandleNavigation(items, "此电脑");
    }

    static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int unit = 0;
        double size = bytes;

        while (size >= 1024 && unit < sizes.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {sizes[unit]}";
    }
    #endregion

    #region 辅助类型
    class FileItem
    {
        public string Display { get; }
        public string Path { get; }

        public FileItem(string display, string path)
        {
            Display = display;
            Path = path;
        }
    }
    #endregion
}