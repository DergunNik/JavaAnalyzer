using JavaTranslator;
using JavaTranslator.Tokens;
using JavaTranslator.Utils;

var inputText = FileReader.Read();

var lexer = new Lexer(inputText);
List<Token> tokens = [];
Token token;
do
{
    token = lexer.NextToken();
    tokens.Add(token);
} while (token.Kind != TokenKind.EOF);

var lineCnt = 1;
foreach (var t in tokens)
{
    if (t.IsFromNewLine)
    {
        Console.Write($"\n{lineCnt++}\t");
    }

    if (t.Kind == TokenKind.ERROR)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(t.ToString());
        Console.ResetColor();
    } else
    {
        Console.Write(t.ToString());
    }
}

if (tokens.Count == 0) return;

const string SeparatorLine = "====================";

Console.WriteLine();
Console.WriteLine();
Console.WriteLine();
Console.WriteLine("Stats");
Console.WriteLine();
Console.WriteLine();

tokens.Sort((a, b) => a.Kind.CompareTo(b.Kind));
var curKind =  tokens[0].Kind;
List<Token> curList = [];

foreach (var t in tokens)
{
    if (curKind != t.Kind)
    {
        PrintTokenTypeList();
        curList = [t];
        curKind = t.Kind;
    } else
    {
        curList.Add(t);
    }
}
PrintTokenTypeList();

void PrintTokenTypeList()
{
    var cntd = curList.CountBy(t => t.Value);
    var i = 1;
    Console.WriteLine(curKind.ToString());
    Console.WriteLine(SeparatorLine);
    Console.WriteLine("i\tcnt\tval");
    Console.WriteLine(SeparatorLine);
    foreach (var p in cntd)
    {
        Console.WriteLine($"{i++}\t{p.Value}\t{p.Key}");
    }
    Console.WriteLine(SeparatorLine);
    Console.WriteLine();
}