using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TTS_Company.Components.Server.Components
{
    internal static class JSONHelper
    {
        internal static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        internal static Dictionary<string, object> ParseFlatObject(string json)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrEmpty(json))
            {
                return result;
            }

            int i = 0;
            SkipWhitespace(json, ref i);
            Expect(json, ref i, '{');
            SkipWhitespace(json, ref i);

            if (Peek(json, i) == '}')
            {
                i++;
                return result;
            }

            while (true)
            {
                SkipWhitespace(json, ref i);
                string key = ParseString(json, ref i);
                SkipWhitespace(json, ref i);
                Expect(json, ref i, ':');
                SkipWhitespace(json, ref i);
                object value = ParseValue(json, ref i);
                result[key] = value;
                SkipWhitespace(json, ref i);

                char c = Peek(json, i);

                if (c == ',')
                {
                    i++;
                    continue;
                }

                if (c == '}')
                {
                    i++;
                    break;
                }

                throw new FormatException($"Unexpected character at position {i} in JSON: {json}");
            }

            return result;
        }

        private static object ParseValue(string json, ref int i)
        {
            switch (Peek(json, i))
            {
                case '"':
                    return ParseString(json, ref i);
                case 't':
                    Expect(json, ref i, "true");
                    return true;
                case 'f':
                    Expect(json, ref i, "false");
                    return false;
                case 'n':
                    Expect(json, ref i, "null");
                    return null;
                default:
                    return ParseNumber(json, ref i);
            }
        }

        private static string ParseString(string json, ref int i)
        {
            Expect(json, ref i, '"');
            var sb = new StringBuilder();

            while (true)
            {
                char c = json[i++];
                if (c == '"')
                {
                    break;
                }

                if (c == '\\')
                {
                    char esc = json[i++];
                    switch (esc)
                    {
                        case '"':
                            sb.Append('"');
                            break;
                        case '\\':
                            sb.Append('\\');
                            break;
                        case '/':
                            sb.Append('/');
                            break;
                        case 'b':
                            sb.Append('\b');
                            break;
                        case 'f':
                            sb.Append('\f');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'u':
                            int code = Convert.ToInt32(json.Substring(i, 4), 16);
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static object ParseNumber(string json, ref int i)
        {
            int start = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '-' || json[i] == '+' || json[i] == '.' || json[i] == 'e' || json[i] == 'E'))
            {
                i++;
            }

            string numStr = json.Substring(start, i - start);
            if (numStr.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0)
            {
                return double.Parse(numStr, CultureInfo.InvariantCulture);
            }

            return long.Parse(numStr, CultureInfo.InvariantCulture);
        }

        private static void SkipWhitespace(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
            {
                i++;
            }
        }

        private static char Peek(string json, int i) => i < json.Length ? json[i] : '\0';

        private static void Expect(string json, ref int i, char c)
        {
            if (i >= json.Length || json[i] != c)
            {
                throw new FormatException($"Expected '{c}' at position {i} in JSON: {json}");
            }
            i++;
        }

        private static void Expect(string json, ref int i, string token)
        {
            if (i + token.Length > json.Length || string.CompareOrdinal(json, i, token, 0, token.Length) != 0)
            {
                throw new FormatException($"Expected '{token}' at position {i} in JSON: {json}");
            }
            i += token.Length;
        }
    }
}
