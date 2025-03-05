///    CLI-UI - A simple command-line interface for browsing files and directories
///    Copyright(C) 2025 Luke Zhang
///
///    This program is free software: you can redistribute it and/or modify
///    it under the terms of the GNU General Public License as published by
///    the Free Software Foundation, either version 3 of the License, or
///    (at your option) any later version.
///
///    This program is distributed in the hope that it will be useful,
///    but WITHOUT ANY WARRANTY; without even the implied warranty of
///    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
///    GNU General Public License for more details.
///
///    You should have received a copy of the GNU General Public License
///    along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Diagnostics;

class Program
{
    static void Main(string[] args)
    {
        const int itemsPerPage = 22;
        string currentDirectory = null;
        int selectedIndex = 0;
        int currentPage = 0;

        while (true)
        {
            Console.Clear();
            if (currentDirectory == null)
            {
                Console.WriteLine("此电脑");
                Console.WriteLine("====================================");

                var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();

                for (int i = 0; i < drives.Count; i++)
                {
                    if (i == selectedIndex)
                    {
                        Console.BackgroundColor = ConsoleColor.Gray;
                        Console.ForegroundColor = ConsoleColor.Black;
                    }

                    Console.WriteLine($"[D] {drives[i].Name} ({drives[i].VolumeLabel})");

                    Console.ResetColor();
                }

                Console.WriteLine("====================================");
                Console.WriteLine("使用方向键选择驱动器，空格或 Enter 打开，Esc 退出");

                var key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex = (selectedIndex - 1 + drives.Count) % drives.Count;
                        break;
                    case ConsoleKey.DownArrow:
                        selectedIndex = (selectedIndex + 1) % drives.Count;
                        break;
                    case ConsoleKey.Spacebar:
                    case ConsoleKey.Enter:
                        currentDirectory = drives[selectedIndex].RootDirectory.FullName;
                        selectedIndex = 0;
                        currentPage = 0;
                        break;
                    case ConsoleKey.Escape:
                        return;
                }
            }
            else
            {
                Console.WriteLine($"当前目录: {currentDirectory}");
                Console.WriteLine("====================================");

                var directories = Enumerable.Empty<DirectoryInfo>().ToList();
                var files = Enumerable.Empty<FileInfo>().ToList();

                try
                {
                    directories = Directory.GetDirectories(currentDirectory).Select(d => new DirectoryInfo(d)).ToList();
                    files = Directory.GetFiles(currentDirectory).Select(f => new FileInfo(f)).ToList();
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.WriteLine("发生错误:");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("按任意键返回...");
                    Console.ReadKey();
                    // 返回到上一个目录
                    currentDirectory = Directory.GetParent(currentDirectory)?.FullName ?? null;
                    selectedIndex = 0;
                    currentPage = 0;
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("发生错误:");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("按任意键返回...");
                    Console.ReadKey();
                    // 返回到上一个目录
                    currentDirectory = Directory.GetParent(currentDirectory)?.FullName ?? null;
                    selectedIndex = 0;
                    currentPage = 0;
                    continue;
                }

                var items = directories.Cast<FileSystemInfo>().Concat(files).ToList();

                int totalPages = (int)Math.Ceiling((double)items.Count / itemsPerPage);
                var pagedItems = items.Skip(currentPage * itemsPerPage).Take(itemsPerPage).ToList();

                for (int i = 0; i < pagedItems.Count; i++)
                {
                    if (i == selectedIndex)
                    {
                        Console.BackgroundColor = ConsoleColor.Gray;
                        Console.ForegroundColor = ConsoleColor.Black;
                    }

                    Console.WriteLine($"{(pagedItems[i] is DirectoryInfo ? "[D]" : "[F]")} {pagedItems[i].Name}");

                    Console.ResetColor();
                }

                Console.WriteLine("====================================");
                Console.WriteLine($"页码: {currentPage + 1}/{totalPages}");
                Console.WriteLine("使用方向键选择文件/文件夹，空格或 Enter 打开，A 到上一个目录，D 到下一个目录，S 询问是否关机，数字键选择页码，Esc 返回到驱动器选择");

                var key = Console.ReadKey(true).Key;

                try
                {
                    switch (key)
                    {
                        case ConsoleKey.UpArrow:
                            selectedIndex = (selectedIndex - 1 + pagedItems.Count) % pagedItems.Count;
                            break;
                        case ConsoleKey.DownArrow:
                            selectedIndex = (selectedIndex + 1) % pagedItems.Count;
                            break;
                        case ConsoleKey.Spacebar:
                        case ConsoleKey.Enter:
                            if (pagedItems[selectedIndex] is DirectoryInfo)
                            {
                                currentDirectory = pagedItems[selectedIndex].FullName;
                                selectedIndex = 0;
                                currentPage = 0;
                            }
                            else if (pagedItems[selectedIndex] is FileInfo)
                            {
                                Console.Clear();
                                Console.WriteLine("选择操作: (R) 读取文件内容, (O) 使用外部应用打开/执行EXE文件");
                                var choice = Console.ReadKey(true).Key;
                                if (choice == ConsoleKey.R)
                                {
                                    EditFile(pagedItems[selectedIndex].FullName);
                                }
                                else if (choice == ConsoleKey.O)
                                {
                                    Process.Start(new ProcessStartInfo(pagedItems[selectedIndex].FullName) { UseShellExecute = true });
                                }
                            }
                            break;
                        case ConsoleKey.A:
                            currentDirectory = Directory.GetParent(currentDirectory)?.FullName ?? currentDirectory;
                            selectedIndex = 0;
                            currentPage = 0;
                            break;
                        case ConsoleKey.D:
                            if (pagedItems[selectedIndex] is DirectoryInfo)
                            {
                                currentDirectory = pagedItems[selectedIndex].FullName;
                                selectedIndex = 0;
                                currentPage = 0;
                            }
                            break;
                        case ConsoleKey.S:
                            Console.Clear();
                            Console.WriteLine("你确定要关机吗？(Y/N)");
                            if (Console.ReadKey(true).Key == ConsoleKey.Y)
                            {
                                return;
                            }
                            break;
                        case ConsoleKey.Escape:
                            currentDirectory = null;
                            selectedIndex = 0;
                            currentPage = 0;
                            break;
                        default:
                            if (key >= ConsoleKey.D1 && key <= ConsoleKey.D9)
                            {
                                int page = key - ConsoleKey.D1;
                                if (page < totalPages)
                                {
                                    currentPage = page;
                                    selectedIndex = 0;
                                }
                            }
                            break;
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.Clear();
                    Console.WriteLine("发生错误:");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("按任意键返回...");
                    Console.ReadKey();
                    // 返回到上一个目录
                    currentDirectory = Directory.GetParent(currentDirectory)?.FullName ?? null;
                    selectedIndex = 0;
                    currentPage = 0;
                }
                catch (Exception ex)
                {
                    Console.Clear();
                    Console.WriteLine("发生错误:");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("按任意键返回...");
                    Console.ReadKey();
                    // 返回到上一个目录
                    currentDirectory = Directory.GetParent(currentDirectory)?.FullName ?? null;
                    selectedIndex = 0;
                    currentPage = 0;
                }
            }
        }
    }

    static void EditFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath).ToList();
        int cursorTop = 0;
        int cursorLeft = 0;

        while (true)
        {
            Console.Clear();
            Console.WriteLine($"编辑文件: {filePath}");
            Console.WriteLine("====================================");

            for (int i = 0; i < lines.Count; i++)
            {
                Console.WriteLine(lines[i]);
            }

            Console.SetCursorPosition(cursorLeft, cursorTop + 2); // +2 to account for the header lines
            var key = Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    cursorTop = Math.Max(cursorTop - 1, 0);
                    cursorLeft = Math.Min(cursorLeft, lines[cursorTop].Length);
                    break;
                case ConsoleKey.DownArrow:
                    cursorTop = Math.Min(cursorTop + 1, lines.Count - 1);
                    cursorLeft = Math.Min(cursorLeft, lines[cursorTop].Length);
                    break;
                case ConsoleKey.LeftArrow:
                    cursorLeft = Math.Max(cursorLeft - 1, 0);
                    break;
                case ConsoleKey.RightArrow:
                    cursorLeft = Math.Min(cursorLeft + 1, lines[cursorTop].Length);
                    break;
                case ConsoleKey.Escape:
                    return;
                case ConsoleKey.F11:
                    return;
                case ConsoleKey.Enter:
                    lines.Insert(cursorTop + 1, string.Empty);
                    cursorTop++;
                    cursorLeft = 0;
                    break;
                case ConsoleKey.Spacebar:
                    lines[cursorTop] = lines[cursorTop].Insert(cursorLeft, " ");
                    cursorLeft++;
                    break;
                case ConsoleKey.Backspace:
                    if (cursorLeft > 0)
                    {
                        lines[cursorTop] = lines[cursorTop].Remove(cursorLeft - 1, 1);
                        cursorLeft--;
                    }
                    else if (cursorTop > 0)
                    {
                        cursorLeft = lines[cursorTop - 1].Length;
                        lines[cursorTop - 1] += lines[cursorTop];
                        lines.RemoveAt(cursorTop);
                        cursorTop--;
                    }
                    break;
                case ConsoleKey.Delete:
                    if (cursorLeft < lines[cursorTop].Length)
                    {
                        lines[cursorTop] = lines[cursorTop].Remove(cursorLeft, 1);
                    }
                    else if (cursorTop < lines.Count - 1)
                    {
                        lines[cursorTop] += lines[cursorTop + 1];
                        lines.RemoveAt(cursorTop + 1);
                    }
                    break;
                case ConsoleKey.F10:
                    File.WriteAllLines(filePath, lines);
                    Console.SetCursorPosition(0, lines.Count + 3);
                    Console.WriteLine("文件已保存。按任意键继续...");
                    Console.ReadKey(true);
                    break;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        lines[cursorTop] = lines[cursorTop].Insert(cursorLeft, key.KeyChar.ToString());
                        cursorLeft++;
                    }
                    break;
            }
        }
    }
}
