using System.Globalization;
using System.Text;
using JavaTranslator.Tokens;

namespace JavaTranslator
{
    public class Lexer
    {
        private readonly string _inputText;
        private int _position;
        private bool _atLineStart;

        private static readonly List<string> OperatorsSorted;
        private static readonly List<string> SeparatorsSorted;
        private static readonly int MaxOperatorLen;
        private static readonly int MaxSeparatorLen;

        static Lexer()
        {
            OperatorsSorted = new List<string>(Keywords.OperatorSet);
            SeparatorsSorted = new List<string>(Keywords.SeparatorSet);

            OperatorsSorted.Sort((a, b) => b.Length.CompareTo(a.Length));
            SeparatorsSorted.Sort((a, b) => b.Length.CompareTo(a.Length));

            MaxOperatorLen = OperatorsSorted.Count > 0 ? OperatorsSorted[0].Length : 0;
            MaxSeparatorLen = SeparatorsSorted.Count > 0 ? SeparatorsSorted[0].Length : 0;
        }

        public Lexer(string inputText)
        {
            _inputText = inputText ?? string.Empty;
            _position = 0;
            _atLineStart = true;
        }

        public Token NextToken()
        {
            SkipWhitespaceAndComments();

            var start = _position;
            var isFromNewLine = _atLineStart;

            if (IsEOF)
            {
                return new Token(TokenKind.EOF, string.Empty, _position, isFromNewLine);
            }

            var c = Peek();

            // идентификаторы и ключевые слова (true, false, null)
            if (IsIdentifierStartAt(_position))
            {
                var sb = new StringBuilder();
                sb.Append(ReadRuneAsString());
                while (!IsEOF && IsIdentifierPartAt(_position))
                {
                    sb.Append(ReadRuneAsString());
                }

                var val = sb.ToString();

                if (Keywords.IsKeyword(val))
                    return new Token(TokenKind.KEYWORD, val, start, isFromNewLine);

                if (Keywords.IsLiteral(val))
                {
                    Type literalType = val switch
                    {
                        "true" or "false" => typeof(bool),
                        "null" => typeof(JavaNullType),
                        _ => typeof(object)
                    };
                    return new LiteralToken(TokenKind.LITERAL, val, start, isFromNewLine, literalType);
                }

                return new Token(TokenKind.IDENTIFIER, val, start, isFromNewLine);
            }

            // числовые литералы
            if (char.IsDigit(c))
            {
                var numStr = ReadNumberLiteral();
                var numType = DetermineNumberType(numStr);
                return new LiteralToken(TokenKind.LITERAL, numStr, start, isFromNewLine, numType);
            }
            
            // строковые литералы и текстовые блоки
            if (c == '"')
            {
                if (Peek(1) == '"' && Peek(2) == '"')
                {
                    var tb = ReadTextBlock();
                    if (tb == null)
                        return new Token(TokenKind.ERROR, "Unterminated text block", start, isFromNewLine);

                    return new LiteralToken(TokenKind.LITERAL, tb, start, isFromNewLine, typeof(string));
                }

                var str = ReadStringLiteral();
                if (str == null)
                    return new Token(TokenKind.ERROR, "Unterminated string literal", start, isFromNewLine);

                return new LiteralToken(TokenKind.LITERAL, str, start, isFromNewLine, typeof(string));
            }


            // cимвольные литералы
            if (c == '\'')
            {
                var chStr = ReadCharLiteral();
                if (chStr == null)
                    return new Token(TokenKind.ERROR, "Unterminated char literal", start, isFromNewLine);

                return new LiteralToken(TokenKind.LITERAL, chStr, start, isFromNewLine, typeof(char));
            }

            // операторы
            var remain = _inputText.Length - _position;
            var maxTry = Math.Min(MaxOperatorLen, remain);
            for (var len = maxTry; len > 0; len--)
            {
                var candidate = _inputText.Substring(_position, len);
                if (Keywords.IsOperator(candidate))
                {
                    _position += len;
                    _atLineStart = false;
                    return new Token(TokenKind.OPERATOR, candidate, start, isFromNewLine);
                }
            }

            // разделители
            maxTry = Math.Min(MaxSeparatorLen, remain);
            for (var len = maxTry; len > 0; len--)
            {
                var candidate = _inputText.Substring(_position, len);
                if (Keywords.IsSeparator(candidate))
                {
                    _position += len;
                    _atLineStart = false;
                    return new Token(TokenKind.SEPARATOR, candidate, start, isFromNewLine);
                }
            }

            var bad = Next();
            _atLineStart = false;
            return new Token(TokenKind.ERROR, bad.ToString(), start, isFromNewLine);
        }

        #region TypeHelpers
        
        private static Type DetermineNumberType(string literal)
        {
            if (string.IsNullOrEmpty(literal)) return typeof(int);

            char last = char.ToLowerInvariant(literal[^1]);

            bool isHex = literal.Length > 2 && literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            
            if (isHex && (literal.Contains('p') || literal.Contains('P')))
            {
                if (last == 'f') return typeof(float);
                return typeof(double);
            }

            if (last == 'l') return typeof(long);
            if (last == 'f') return typeof(float);
            if (last == 'd') return typeof(double);

            if (literal.Contains('.') || literal.Contains('e') || literal.Contains('E'))
            {
                return typeof(double);
            }

            return typeof(int);
        }

        #endregion

        #region Helpers

        private bool IsEOF => _position >= _inputText.Length;

        private char Peek(int lookahead = 0)
        {
            var pos = _position + lookahead;
            return pos >= _inputText.Length ? '\0' : _inputText[pos];
        }

        private char Next()
        {
            if (IsEOF) return '\0';
            return _inputText[_position++];
        }

        private void SkipWhitespaceAndComments()
        {
            var sawNewline = false;
            while (!IsEOF)
            {
                var c = Peek();
                if (c is ' ' or '\t' or '\f' or '\v')
                {
                    Next();
                    continue;
                }

                if (c is '\r' or '\n')
                {
                    sawNewline = true;
                    if (c == '\r')
                    {
                        Next();
                        if (Peek() == '\n') Next();
                    }
                    else
                    {
                        Next();
                    }
                    continue;
                }

                // //
                if (c == '/' && Peek(1) == '/')
                {
                    Next(); Next();
                    while (!IsEOF && Peek() != '\n' && Peek() != '\r')
                        Next();
                    continue;
                }

                // /* ... */
                if (c == '/' && Peek(1) == '*')
                {
                    Next(); Next();
                    while (!IsEOF)
                    {
                        if (Peek() == '*' && Peek(1) == '/')
                        {
                            Next(); Next();
                            break;
                        }

                        if (Peek() == '\r' || Peek() == '\n')
                        {
                            if (Peek() == '\r')
                            {
                                Next();
                                if (Peek() == '\n') Next();
                            }
                            else
                            {
                                Next();
                            }
                            sawNewline = true;
                        }
                        else
                        {
                            Next();
                        }
                    }
                    continue;
                }

                break;
            }

            _atLineStart = sawNewline || (_atLineStart && _position == 0);
        }

        private static bool IsIdentifierStart(char c)
        {
            if (c is '_' or '$') return true;
            return char.IsLetter(c);
        }

        private static bool IsIdentifierPart(char c)
        {
            if (c is '_' or '$') return true;
            return char.IsLetterOrDigit(c);
        }

        private string ReadNumberLiteral()
        {
            var start = _position;

            bool ConsumeDigitsAllowUnderscore(Func<char, bool> isDigit)
            {
                var sawDigit = false;
                while (!IsEOF)
                {
                    if (isDigit(Peek()))
                    {
                        sawDigit = true;
                        Next();
                        continue;
                    }
                    if (Peek() == '_' && isDigit(Peek(1)))
                    {
                        Next();
                        continue;
                    }
                    break;
                }
                return sawDigit;
            }

            if (Peek() == '0' && (Peek(1) == 'x' || Peek(1) == 'X'))
            {
                Next(); Next();
                ConsumeDigitsAllowUnderscore(IsHexDigit);
                
                if (Peek() == '.')
                {
                    if (IsHexDigit(Peek(1)) || Peek(1) == '_')
                    {
                        Next();
                        ConsumeDigitsAllowUnderscore(IsHexDigit);
                    }
                }

                if (Peek() == 'p' || Peek() == 'P')
                {
                    Next();
                    if (Peek() == '+' || Peek() == '-') Next();
                    ConsumeDigitsAllowUnderscore(char.IsDigit);
                    if (!IsEOF && (Peek() == 'f' || Peek() == 'F' || Peek() == 'd' || Peek() == 'D'))
                        Next();
                    return _inputText.Substring(start, _position - start);
                }
                else
                {
                    if (!IsEOF && (Peek() == 'l' || Peek() == 'L'))
                        Next();
                    return _inputText.Substring(start, _position - start);
                }
            }

            if (Peek() == '0' && (Peek(1) == 'b' || Peek(1) == 'B'))
            {
                Next(); Next();
                ConsumeDigitsAllowUnderscore(ch => ch is '0' or '1');
                if (!IsEOF && (Peek() == 'l' || Peek() == 'L')) Next();
                return _inputText.Substring(start, _position - start);
            }

            ConsumeDigitsAllowUnderscore(char.IsDigit);

            if (Peek() == '.')
            {
                if (char.IsDigit(Peek(1)))
                {
                    Next();
                    ConsumeDigitsAllowUnderscore(char.IsDigit);
                }
            }

            if (Peek() == 'e' || Peek() == 'E')
            {
                var save = _position;
                Next(); // e/E
                if (Peek() == '+' || Peek() == '-') Next();
                var expDigits = ConsumeDigitsAllowUnderscore(char.IsDigit);
                if (!expDigits)
                {
                    _position = save;
                }
            }

            if (!IsEOF)
            {
                var s = Peek();
                if (s is 'f' or 'F' or 'd' or 'D' or 'l' or 'L')
                    Next();
            }

            return _inputText.Substring(start, _position - start);
        }

        private static bool IsHexDigit(char c)
        {
            return c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
        }

        private string ReadStringLiteral()
        {
            var sb = new StringBuilder();
            var open = Next(); // '"'
            sb.Append(open);

            while (!IsEOF)
            {
                var c = Next();
                if (c == '"')
                {
                    sb.Append(c);
                    return sb.ToString();
                }

                if (c == '\\')
                {
                    if (IsEOF) { sb.Append('\\'); break; }

                    if (Peek() == 'u')
                    {
                        // java разрешает несколько u в \uXXXX 
                        while (Peek() == 'u') Next();

                        int value = 0;
                        for (int i = 0; i < 4 && !IsEOF; i++)
                        {
                            var hx = Next();
                            int hex;
                            if (hx >= '0' && hx <= '9') hex = hx - '0';
                            else if (hx >= 'a' && hx <= 'f') hex = hx - 'a' + 10;
                            else if (hx >= 'A' && hx <= 'F') hex = hx - 'A' + 10;
                            else { hex = 0; }
                            value = (value << 4) + hex;
                        }

                        if (value >= 0xD800 && value <= 0xDBFF)
                        {
                            var saved = _position;
                            if (Peek() == '\\' && Peek(1) == 'u')
                            {
                                Next();
                                Next();
                                int v2 = 0;
                                while (Peek() == 'u') Next();
                                bool ok = true;
                                for (int i = 0; i < 4 && !IsEOF; i++)
                                {
                                    var hx = Next();
                                    int hex;
                                    if (hx >= '0' && hx <= '9') hex = hx - '0';
                                    else if (hx >= 'a' && hx <= 'f') hex = hx - 'a' + 10;
                                    else if (hx >= 'A' && hx <= 'F') hex = hx - 'A' + 10;
                                    else { ok = false; break; }
                                    v2 = (v2 << 4) + hex;
                                }
                                if (ok && v2 >= 0xDC00 && v2 <= 0xDFFF)
                                {
                                    var combined = char.ConvertFromUtf32(((value - 0xD800) << 10) + (v2 - 0xDC00) + 0x10000);
                                    sb.Append(combined);
                                    continue;
                                }
                                else
                                {
                                    _position = saved;
                                }
                            }
                        }

                        try
                        {
                            sb.Append(char.ConvertFromUtf32(value));
                        }
                        catch
                        {
                            sb.Append((char)value);
                        }
                        continue;
                    }
                    else
                    {
                        var esc = Next();
                        switch (esc)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case '\\': sb.Append('\\'); break;
                            case '\'': sb.Append('\''); break;
                            case '"': sb.Append('"'); break;
                            default: sb.Append(esc); break;
                        }
                        continue;
                    }
                }

                if (c == '\r' || c == '\n')
                    return null;

                sb.Append(c);
            }

            return null;
        }

        private string ReadCharLiteral()
        {
            var sb = new StringBuilder();
            var open = Next();
            sb.Append(open);
            if (IsEOF) return null;

            var first = Next();
            if (first == '\\')
            {
                if (IsEOF) return null;
                if (Peek() == 'u')
                {
                    while (Peek() == 'u') Next();
                    int value = 0;
                    for (var i = 0; i < 4 && !IsEOF; i++)
                    {
                        var hx = Next();
                        int hex;
                        if (hx >= '0' && hx <= '9') hex = hx - '0';
                        else if (hx >= 'a' && hx <= 'f') hex = hx - 'a' + 10;
                        else if (hx >= 'A' && hx <= 'F') hex = hx - 'A' + 10;
                        else { hex = 0; }
                        value = (value << 4) + hex;
                    }

                    try { sb.Append(char.ConvertFromUtf32(value)); }
                    catch { sb.Append((char)value); }
                }
                else
                {
                    var esc = Next();
                    switch (esc)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case '\\': sb.Append('\\'); break;
                        case '\'': sb.Append('\''); break;
                        case '"': sb.Append('"'); break;
                        default: sb.Append(esc); break;
                    }
                }
            }
            else if (first == '\'')
            {
                return null;
            }
            else
            {
                sb.Append(first);
            }

            if (IsEOF) return null;

            if (Peek() == '\'')
            {
                sb.Append(Next());
                return sb.ToString();
            }

            return null;
        }
        
        private string ReadTextBlock()
        {
            var sb = new StringBuilder();
            sb.Append(Next()); sb.Append(Next()); sb.Append(Next());

            while (!IsEOF)
            {
                var c = Next();
                sb.Append(c);

                if (c == '"')
                {
                    if (Peek() == '"' && Peek(1) == '"')
                    {
                        sb.Append(Next());
                        sb.Append(Next());
                        return sb.ToString();
                    }
                }
            }

            return null;
        }

        // для сурогатных пар Unicode
        private bool IsIdentifierStartAt(int pos)
        {
            if (pos >= _inputText.Length) return false;
            var ch = _inputText[pos];
            if (ch == '_' || ch == '$') return true;

            var cat = CharUnicodeInfo.GetUnicodeCategory(_inputText, pos);
            switch (cat)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.LetterNumber:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsIdentifierPartAt(int pos)
        {
            if (pos >= _inputText.Length) return false;
            var ch = _inputText[pos];
            if (ch is '_' or '$') return true;

            var cat = CharUnicodeInfo.GetUnicodeCategory(_inputText, pos);
            switch (cat)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.EnclosingMark:
                    return true;
                default:
                    return false;
            }
        }

        // считывает 1 или 2 чара и возвращает соответствующую им строку
        private string ReadRuneAsString()
        {
            if (IsEOF) return string.Empty;
            var c = _inputText[_position];
            if (char.IsHighSurrogate(c) && _position + 1 < _inputText.Length && char.IsLowSurrogate(_inputText[_position + 1]))
            {
                var s = new string(new[] { c, _inputText[_position + 1] });
                _position += 2;
                return s;
            }
            _position++;
            return c.ToString();
        }

        private string PeekRuneAsString(int lookahead = 0)
        {
            int pos = _position + lookahead;
            if (pos >= _inputText.Length) return "\0";
            var c = _inputText[pos];
            if (char.IsHighSurrogate(c) && pos + 1 < _inputText.Length && char.IsLowSurrogate(_inputText[pos + 1]))
                return new string(new[] { c, _inputText[pos + 1] });
            return c.ToString();
        }
        
        #endregion
    }
}