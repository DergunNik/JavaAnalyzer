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

            int start = _position;
            bool isFromNewLine = _atLineStart;

            if (IsEOF)
            {
                return new Token(TokenKind.EOF, string.Empty, _position, isFromNewLine);
            }

            char c = Peek();

            if (IsIdentifierStart(c))
            {
                var sb = new StringBuilder();
                sb.Append(Next());
                while (!IsEOF && IsIdentifierPart(Peek()))
                    sb.Append(Next());

                string val = sb.ToString();
                if (Keywords.IsKeyword(val))
                    return new Token(TokenKind.KEYWORD, val, start, isFromNewLine);
                if (Keywords.IsLiteral(val))
                    return new Token(TokenKind.LITERAL, val, start, isFromNewLine);
                return new Token(TokenKind.IDENTIFIER, val, start, isFromNewLine);
            }

            if (char.IsDigit(c))
            {
                string num = ReadNumberLiteral();
                return new Token(TokenKind.LITERAL, num, start, isFromNewLine);
            }

            if (c == '"')
            {
                string str = ReadStringLiteral();
                if (str == null)
                    return new Token(TokenKind.ERROR, "Unterminated string literal", start, isFromNewLine);
                return new Token(TokenKind.LITERAL, str, start, isFromNewLine);
            }

            if (c == '\'')
            {
                string ch = ReadCharLiteral();
                if (ch == null)
                    return new Token(TokenKind.ERROR, "Unterminated char literal", start, isFromNewLine);
                return new Token(TokenKind.LITERAL, ch, start, isFromNewLine);
            }

            int remain = _inputText.Length - _position;
            int maxTry = Math.Min(MaxOperatorLen, remain);
            for (int len = maxTry; len > 0; len--)
            {
                string candidate = _inputText.Substring(_position, len);
                if (Keywords.IsOperator(candidate))
                {
                    _position += len;
                    _atLineStart = false;
                    return new Token(TokenKind.OPERATOR, candidate, start, isFromNewLine);
                }
            }

            maxTry = Math.Min(MaxSeparatorLen, remain);
            for (int len = maxTry; len > 0; len--)
            {
                string candidate = _inputText.Substring(_position, len);
                if (Keywords.IsSeparator(candidate))
                {
                    _position += len;
                    _atLineStart = false;
                    return new Token(TokenKind.SEPARATOR, candidate, start, isFromNewLine);
                }
            }

            char bad = Next();
            _atLineStart = false;
            return new Token(TokenKind.ERROR, bad.ToString(), start, isFromNewLine);
        }

        #region Helpers

        private bool IsEOF => _position >= _inputText.Length;

        private char Peek(int lookahead = 0)
        {
            int pos = _position + lookahead;
            return pos >= _inputText.Length ? '\0' : _inputText[pos];
        }

        private char Next()
        {
            if (IsEOF) return '\0';
            return _inputText[_position++];
        }

        private void SkipWhitespaceAndComments()
        {
            bool sawNewline = false;
            while (!IsEOF)
            {
                char c = Peek();
                if (c == ' ' || c == '\t' || c == '\f' || c == '\v')
                {
                    Next();
                    continue;
                }

                if (c == '\r' || c == '\n')
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
            if (c == '_' || c == '$') return true;
            return char.IsLetter(c);
        }

        private static bool IsIdentifierPart(char c)
        {
            if (c == '_' || c == '$') return true;
            return char.IsLetterOrDigit(c);
        }

        private string ReadNumberLiteral()
        {
            int start = _position;

            bool ConsumeDigitsAllowUnderscore(Func<char, bool> isDigit)
            {
                bool sawDigit = false;
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
                bool hasFraction = false;
                if (Peek() == '.')
                {
                    if (IsHexDigit(Peek(1)) || Peek(1) == '_')
                    {
                        hasFraction = true;
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
                ConsumeDigitsAllowUnderscore(ch => ch == '0' || ch == '1');
                if (!IsEOF && (Peek() == 'l' || Peek() == 'L')) Next();
                return _inputText.Substring(start, _position - start);
            }

            ConsumeDigitsAllowUnderscore(char.IsDigit);

            bool seenDecimalPoint = false;
            if (Peek() == '.')
            {
                if (char.IsDigit(Peek(1)))
                {
                    seenDecimalPoint = true;
                    Next();
                    ConsumeDigitsAllowUnderscore(char.IsDigit);
                }
            }

            if (Peek() == 'e' || Peek() == 'E')
            {
                int save = _position;
                Next(); // e/E
                if (Peek() == '+' || Peek() == '-') Next();
                bool expDigits = ConsumeDigitsAllowUnderscore(char.IsDigit);
                if (!expDigits)
                {
                    _position = save;
                }
                else
                {
                    // exponent ok
                }
            }

            if (!IsEOF)
            {
                char s = Peek();
                if (s == 'f' || s == 'F' || s == 'd' || s == 'D' || s == 'l' || s == 'L')
                    Next();
            }

            return _inputText.Substring(start, _position - start);
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') ||
                   (c >= 'a' && c <= 'f') ||
                   (c >= 'A' && c <= 'F');
        }

        private string ReadStringLiteral()
        {
            var sb = new StringBuilder();
            char open = Next();
            sb.Append(open);
            while (!IsEOF)
            {
                char c = Next();
                sb.Append(c);
                if (c == '\\')
                {
                    if (!IsEOF)
                    {
                        if (Peek() == 'u')
                        {
                            while (Peek() == 'u') sb.Append(Next());
                            for (int i = 0; i < 4 && !IsEOF; i++)
                            {
                                char hx = Next();
                                sb.Append(hx);
                            }
                        }
                        else
                        {
                            char esc = Next();
                            sb.Append(esc);
                        }
                    }
                    continue;
                }
                if (c == '"')
                    return sb.ToString();
                if (c == '\r' || c == '\n')
                    return null;
            }
            return null;
        }

        private string ReadCharLiteral()
        {
            var sb = new StringBuilder();
            char open = Next();
            sb.Append(open);
            if (IsEOF) return null;

            char first = Next();
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
                    for (int i = 0; i < 4; i++)
                    {
                        if (IsEOF) return null;
                        char hx = Next();
                        sb.Append(hx);
                    }
                }
                else
                {
                    char esc = Next();
                    sb.Append(esc);
                }
            }

            if (IsEOF) return null;
            char closing = Next();
            sb.Append(closing);
            if (closing != '\'') return null;
            return sb.ToString();
        }

        #endregion
    }
}
