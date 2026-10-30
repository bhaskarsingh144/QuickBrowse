using System;
using System.Collections.Generic;
using System.IO;

internal static class DirectoryModelTests
{
    private static int _failed;

    private static void Main()
    {
        FoldersSortBeforeFiles();
        NamesSortIgnoreCase();
        ParentOfDriveRootIsNull();
        ColumnSortDate();
        ColumnSortSize();
        FormatBytesAndMeta();
        HiddenSkipMatrix();
        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("DirectoryModelTests OK");
    }

    private static void HiddenSkipMatrix()
    {
        Expect(DirectoryModel.ShouldSkip(FileAttributes.Hidden, false), "hidden off skips Hidden");
        Expect(!DirectoryModel.ShouldSkip(FileAttributes.Hidden, true), "hidden on keeps Hidden");
        Expect(DirectoryModel.ShouldSkip(FileAttributes.System, false), "system always skip");
        Expect(DirectoryModel.ShouldSkip(FileAttributes.System, true), "system skip even when show hidden");
        Expect(DirectoryModel.ShouldSkip(FileAttributes.Hidden | FileAttributes.System, true), "hidden+system skip");
        Expect(!DirectoryModel.ShouldSkip(FileAttributes.Normal, false), "normal kept");
    }

    private static void FoldersSortBeforeFiles()
    {
        List<DirEntry> list = new List<DirEntry>();
        list.Add(File("z.txt"));
        list.Add(Dir("alpha"));
        list.Add(File("a.txt"));
        list.Add(Dir("beta"));
        DirectoryModel.SortEntries(list);
        Expect(list[0].Name == "alpha" && list[1].Name == "beta", "folders first");
        Expect(list[2].Name == "a.txt" && list[3].Name == "z.txt", "files after");
    }

    private static void NamesSortIgnoreCase()
    {
        List<DirEntry> list = new List<DirEntry>();
        list.Add(File("b.txt"));
        list.Add(File("A.txt"));
        DirectoryModel.SortEntries(list);
        Expect(list[0].Name == "A.txt" && list[1].Name == "b.txt", "case-insensitive name sort");
    }

    private static void ParentOfDriveRootIsNull()
    {
        string parent = DirectoryModel.ParentPath(@"C:\");
        Expect(parent == null, "drive root has no parent");
    }

    private static void ColumnSortDate()
    {
        DirEntry older = File("old.txt");
        older.LastWriteTime = new DateTime(2020, 1, 1);
        older.HasDate = true;
        DirEntry newer = File("new.txt");
        newer.LastWriteTime = new DateTime(2024, 6, 1);
        newer.HasDate = true;
        Expect(DirectoryModel.CompareByColumn(older, newer, 1, true) < 0, "date asc");
        Expect(DirectoryModel.CompareByColumn(older, newer, 1, false) > 0, "date desc");
    }

    private static void ColumnSortSize()
    {
        DirEntry small = File("s.bin");
        small.Length = 10;
        small.HasSize = true;
        DirEntry big = File("b.bin");
        big.Length = 1000;
        big.HasSize = true;
        Expect(DirectoryModel.CompareByColumn(small, big, 3, true) < 0, "size asc");
        DirEntry folder = Dir("f");
        Expect(DirectoryModel.CompareByColumn(folder, small, 3, true) < 0, "folder before file in size col");
    }

    private static void FormatBytesAndMeta()
    {
        Expect(DirectoryModel.FormatBytes(500) == "500 B", "bytes");
        Expect(DirectoryModel.FormatBytes(2048).IndexOf("KB") >= 0, "KB");
        DirEntry file = File("x.txt");
        file.Length = 2048;
        file.HasSize = true;
        file.LastWriteTime = new DateTime(2023, 3, 15, 10, 0, 0);
        file.HasDate = true;
        Expect(DirectoryModel.FormatSize(file).Length > 0, "format size");
        Expect(DirectoryModel.FormatDate(file).Length > 0, "format date");
        DirEntry folder = Dir("d");
        Expect(DirectoryModel.FormatSize(folder) == "", "folder size blank");
    }

    private static DirEntry Dir(string name)
    {
        return new DirEntry { Name = name, FullPath = @"C:\x\" + name, IsDirectory = true, TypeName = "File folder" };
    }

    private static DirEntry File(string name)
    {
        return new DirEntry { Name = name, FullPath = @"C:\x\" + name, IsDirectory = false, TypeName = "TXT File" };
    }

    private static void Expect(bool ok, string label)
    {
        if (ok)
            return;
        _failed++;
        Console.Error.WriteLine("FAIL: " + label);
    }
}
