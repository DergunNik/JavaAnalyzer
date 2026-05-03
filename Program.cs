using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JavaTranslator;
using JavaTranslator.Tokens;
using JavaTranslator.Utils;
using JavaTranslator.Ast;

var (inputFileName, inputText) = FileReader.Read();
var lexerOutput = inputFileName + ".lexer.txt";
var astOutput = inputFileName + ".ast.txt";

var lexer = new Lexer(inputText);
List<Token> tokens = new();
Token token;
bool hasLexerError = false;

do
{
    token = lexer.NextToken();
    tokens.Add(token);
    hasLexerError = hasLexerError || token.Kind == TokenKind.ERROR;
} while (token.Kind != TokenKind.EOF);

var processor = new LexerResultProcessor(writeToConsole: true);
var finalLexerResult = processor.Process(tokens);
File.WriteAllText(lexerOutput, finalLexerResult, Encoding.UTF8);

if (hasLexerError)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\nЗавершение работы в связи с лексическими ошибками");
    Console.ResetColor();
    return;
}

Console.WriteLine("\n[Запуск синтаксического анализатора...]");

try
{
    var parser = new Parser(tokens);
    CompilationUnitNode ast = parser.Parse();

    if (parser.Errors.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Синтаксический анализ успешно завершен!");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\nСинтаксический анализ завершен с ошибками:");
        foreach (var error in parser.Errors)
            Console.WriteLine(error);
        Console.ResetColor();
    }

    var visualizer = new AstVisualizer();
    string astText = visualizer.Visualize(ast);

    Console.WriteLine("\n--- Abstract Syntax Tree ---");
    Console.WriteLine(astText);

    File.WriteAllText(astOutput, astText, Encoding.UTF8);
    Console.WriteLine($"\nAST сохранен в файл: {astOutput}");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nФАТАЛЬНАЯ ОШИБКА: {ex.Message}");
    Console.ResetColor();
}