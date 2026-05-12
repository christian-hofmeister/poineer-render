public static class TestFiles
{
    public static void WriteAllText(
        string path,
        string content)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(path)!);

        File.WriteAllText(path, content);
    }
}