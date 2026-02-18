using JavaTranslator;
using JavaTranslator.Tokens;
using JavaTranslator.Utils;
using System.Text;

var (inputFileName, inputText) = FileReader.Read();
var outputFileName = inputFileName + ".out.txt";

var lexer = new Lexer(inputText);
List<Token> tokens = new();
Token token;

do
{
    token = lexer.NextToken();
    tokens.Add(token);
} while (token.Kind != TokenKind.EOF);

var processor = new LexerResultProcessor(writeToConsole: true);
var finalResult = processor.Process(tokens);

File.WriteAllText(outputFileName, finalResult, Encoding.UTF8);