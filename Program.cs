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

do
{
    token = lexer.NextToken();
    tokens.Add(token);
} while (token.Kind != TokenKind.EOF);

var processor = new LexerResultProcessor(writeToConsole: true);
var finalLexerResult = processor.Process(tokens);
File.WriteAllText(lexerOutput, finalLexerResult, Encoding.UTF8);

Console.WriteLine("\n[Запуск синтаксического анализатора...]");

try
{
    var parser = new Parser(tokens);
    CompilationUnitNode ast = parser.Parse();

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Синтаксический анализ успешно завершен!");
    Console.ResetColor();

    var visualizer = new AstVisualizer();
    string astText = visualizer.Visualize(ast);

    Console.WriteLine("\n--- Abstract Syntax Tree ---");
    Console.WriteLine(astText);
    
    File.WriteAllText(astOutput, astText, Encoding.UTF8);
    Console.WriteLine($"\nAST сохранен в файл: {astOutput}");
}
catch (SyntaxException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nОШИБКА СИНТАКСИСА: {ex.Message}");
    Console.ResetColor();
}