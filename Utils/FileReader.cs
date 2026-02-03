using System;
using System.Collections.Generic;
using System.Text;

namespace JavaTranslator.Utils;

internal static class FileReader
{
    public const string DirPath = """C:\Projects\C#\JavaTranslator\Input\""";

    public static string Read()
    {
        do
        {
            Console.WriteLine("Enter a file's name");
            var input = Console.ReadLine();
            var fileName = Path.Combine(DirPath, input ?? "");
            if (File.Exists(fileName))
            {
                return File.ReadAllText(fileName, Encoding.UTF8);
            }
            else
            {
                Console.WriteLine($"File {fileName} not found");
            }
        } while (true);
    }
}
