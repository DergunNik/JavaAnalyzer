using System;
using System.Collections.Generic;
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
            if (IsIdentifierStart(c))
            {
                var sb = new StringBuilder();
                sb.Append(Next());
                while (!IsEOF && IsIdentifierPart(Peek()))
                    sb.Append(Next());

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

            // cтроковые литералы
            if (c == '"')
            {
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

        #region Type Helpers
        
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
            var open = Next();
            sb.Append(open);
            while (!IsEOF)
            {
                var c = Next();
                sb.Append(c);
                if (c == '\\')
                {
                    if (!IsEOF)
                    {
                        if (Peek() == 'u')
                        {
                            while (Peek() == 'u') sb.Append(Next());
                            for (var i = 0; i < 4 && !IsEOF; i++)
                            {
                                var hx = Next();
                                sb.Append(hx);
                            }
                        }
                        else
                        {
                            var esc = Next();
                            sb.Append(esc);
                        }
                    }
                    continue;
                }
                if (c == '"')
                    return sb.ToString();
                if (c is '\r' or '\n')
                    return null;
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
            sb.Append(first);
            if (first == '\\')
            {
                if (IsEOF) return null;
                if (Peek() == 'u')
                {
                    while (Peek() == 'u')
                    {
                        sb.Append(Next());
                    }
                    for (var i = 0; i < 4; i++)
                    {
                        if (IsEOF) return null;
                        var hx = Next();
                        sb.Append(hx);
                    }
                }
                else
                {
                    var esc = Next();
                    sb.Append(esc);
                }
            }
            else if (first == '\'') 
            {
                 return null; 
            }

            if (IsEOF) return null;
            
            if (Peek() == '\'')
            {
                sb.Append(Next());
                return sb.ToString();
            }
            
            return null;
        }

        #endregion
    }
}