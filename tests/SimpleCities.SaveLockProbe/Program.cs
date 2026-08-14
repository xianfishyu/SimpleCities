if (args.Length != 1)
    return 2;

string lockPath = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
using var owner = new FileStream(
    lockPath,
    FileMode.OpenOrCreate,
    FileAccess.ReadWrite,
    FileShare.None,
    bufferSize: 1,
    FileOptions.None);
Console.WriteLine("READY");
Console.Out.Flush();
return Console.ReadLine() == "RELEASE" ? 0 : 3;
