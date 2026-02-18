using System.Text;
using JavaTranslator.Tokens;

namespace JavaTranslator.Utils;

public class LexerResultProcessor(bool writeToConsole = true)
{
    private readonly StringBuilder _sb = new();
    private const string SeparatorLine = "============================================================";

    public string Process(List<Token> tokens)
    {
        _sb.Clear();

        if (tokens.Count == 0) return string.Empty;

        ProcessTokenList(tokens);

        ProcessStatistics(tokens);

        ProcessLexemes(tokens);

        return _sb.ToString();
    }

    private void ProcessTokenList(List<Token> tokens)
    {
        var lineCnt = 1;
        foreach (var t in tokens)
        {
            if (t.IsFromNewLine)
            {
                Append($"\n{lineCnt++}\t");
            }

            if (t.Kind == TokenKind.ERROR)
            {
                WriteColor(t.ToString(), ConsoleColor.Red);
            }
            else
            {
                Append(t.ToString());
            }
        }
        AppendLine();
        AppendLine();
    }

    private void ProcessStatistics(List<Token> tokens)
    {
        AppendLine();
        AppendLine("Stats");
        AppendLine();
        AppendLine();

        var groups = tokens
            .OrderBy(t => t.Kind)
            .GroupBy(t => t.Kind);

        foreach (var group in groups)
        {
            var kind = group.Key;
            var kindTokens = group.ToList();
            
            AppendLine(kind.ToString());
            AppendLine(SeparatorLine);
            AppendLine("i\tcnt\tval");
            AppendLine(SeparatorLine);

            var counts = CountDictFor(kindTokens, kind);
            var i = 1;
            foreach (var p in counts)
            {
                AppendLine($"{i++}\t{p.Value}\t{p.Key}");
            }

            AppendLine(SeparatorLine);
            AppendLine();
        }
    }

    private void ProcessLexemes(List<Token> tokens)
    {
        AppendLine("Lexemes");
        AppendLine(SeparatorLine);

        var idMap = new Dictionary<string, int>();
        int idCounter = 1;
        bool isFirstToken = true;

        foreach (var t in tokens.Where(t => t.Kind != TokenKind.EOF))
        {
            if (t.IsFromNewLine && !isFirstToken)
            {
                AppendLine();
            }
            isFirstToken = false;

            if (t.Kind is TokenKind.IDENTIFIER or TokenKind.LITERAL)
            {
                if (!idMap.TryGetValue(t.Value, out int id))
                {
                    id = idCounter++;
                    idMap[t.Value] = id;
                }
                Append($"<ИД{id}> ");
            }
            else
            {
                Append(t.Value + " ");
            }
        }

        AppendLine();
        AppendLine(SeparatorLine);
    }

    private IEnumerable<KeyValuePair<string, int>> CountDictFor(List<Token> list, TokenKind kind)
    {
        if (kind == TokenKind.LITERAL)
        {
            return list.CountBy(t => t.ToString()[3..]);
        }
        return list.CountBy(t => t.Value);
    }

    private void Append(string text)
    {
        _sb.Append(text);
        if (writeToConsole) Console.Write(text);
    }

    private void AppendLine(string text = "")
    {
        _sb.AppendLine(text);
        if (writeToConsole) Console.WriteLine(text);
    }

    private void WriteColor(string text, ConsoleColor color)
    {
        _sb.Append(text);
        if (writeToConsole)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = oldColor;
        }
    }
}