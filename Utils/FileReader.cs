using System;
using System.Collections.Generic;
using System.Text;

namespace JavaTranslator.Utils;

internal static class FileReader
{
    private const string DirPath = """C:\Projects\C#\JavaTranslator\Examples\""";

    public static (string fileName, string fileContent) Read()
    {
        do
        {
            Console.WriteLine("Enter a file's name");
            var input = Console.ReadLine();
            var fileName = Path.Combine(DirPath, input ?? "");
            if (File.Exists(fileName))
            {
                return (fileName, File.ReadAllText(fileName, Encoding.UTF8));
            }
            else
            {
                Console.WriteLine($"File {fileName} not found");
            }
        } while (true);
    }
}
