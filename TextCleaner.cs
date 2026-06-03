using System.Net;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AccessTheObelisk
{
    /// <summary>
    /// Converts game rich text into compact speech-friendly plain text.
    /// </summary>
    public static class TextCleaner
    {
        private const int MaxCachedInputLength = 2048;
        private const int MaxSpeechCacheEntries = 1024;
        private const RegexOptions StandardRegexOptions = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        private const RegexOptions IgnoreCaseRegexOptions = StandardRegexOptions | RegexOptions.IgnoreCase;
        private const RegexOptions ColorRegexOptions = IgnoreCaseRegexOptions | RegexOptions.Singleline;

        private static readonly Regex ColorTagRegex = new Regex(@"<\s*color\s*=\s*[""']?#(?<color>[0-9a-f]{6})[""']?\s*>(?<content>.*?)</\s*color\s*>", ColorRegexOptions);
        private static readonly Regex SpriteRegex = new Regex(@"<\s*sprite\s+name\s*=\s*[""']?([^>""'\s]+)[""']?\s*>", IgnoreCaseRegexOptions);
        private static readonly Regex BrRegex = new Regex(@"<\s*br\d*\s*/?\s*>", IgnoreCaseRegexOptions);
        private static readonly Regex LineHeightEndRegex = new Regex(@"</\s*line-height\s*>", IgnoreCaseRegexOptions);
        private static readonly Regex TagRegex = new Regex(@"<[^>]+>", StandardRegexOptions);
        private static readonly Regex WhitespaceRegex = new Regex(@"\s+", StandardRegexOptions);
        private static readonly Regex SpriteMarkerRegex = new Regex(@"<\s*sprite\b", IgnoreCaseRegexOptions);
        private static readonly Regex BareQuantityRegex = new Regex(@"^[+-]?\d+$", StandardRegexOptions);
        private static readonly Dictionary<string, string> SpeechCache = new Dictionary<string, string>();
        private static string _cacheLanguage;

        /// <summary>
        /// Removes TextMeshPro tags and normalizes whitespace for speech.
        /// </summary>
        public static string ToSpeech(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string language = CurrentLanguageKey();
            string cached;
            if (TryGetCached(text, language, out cached))
            {
                return cached;
            }

            string normalized = text;
            if (HasRichTextMarkers(normalized))
            {
                normalized = ColorTagRegex.Replace(normalized, ReplaceColorMeaning);
                normalized = SpriteRegex.Replace(normalized, ReplaceSpriteName);
                normalized = BrRegex.Replace(normalized, ", ");
                normalized = LineHeightEndRegex.Replace(normalized, " ");
                normalized = TagRegex.Replace(normalized, " ");
                normalized = WebUtility.HtmlDecode(normalized);
            }
            else if (normalized.IndexOf('&') >= 0)
            {
                normalized = WebUtility.HtmlDecode(normalized);
            }

            if (NeedsWhitespaceNormalization(normalized))
            {
                normalized = WhitespaceRegex.Replace(normalized, " ").Trim();
            }
            else
            {
                normalized = normalized.Trim();
            }

            normalized = CollapseDuplicateHalves(normalized);
            StoreCached(text, normalized, language);
            return normalized;
        }

        private static bool TryGetCached(string text, string language, out string cached)
        {
            cached = null;
            if (!CanCache(text))
            {
                return false;
            }

            if (_cacheLanguage != language)
            {
                SpeechCache.Clear();
                _cacheLanguage = language;
                return false;
            }

            return SpeechCache.TryGetValue(text, out cached);
        }

        private static void StoreCached(string source, string cleaned, string language)
        {
            if (!CanCache(source))
            {
                return;
            }

            if (_cacheLanguage != language)
            {
                SpeechCache.Clear();
                _cacheLanguage = language;
            }

            if (SpeechCache.Count >= MaxSpeechCacheEntries)
            {
                SpeechCache.Clear();
            }

            SpeechCache[source] = cleaned;
        }

        private static bool CanCache(string text)
        {
            return !string.IsNullOrEmpty(text) && text.Length <= MaxCachedInputLength;
        }

        private static string CurrentLanguageKey()
        {
            try
            {
                if (Globals.Instance != null && !string.IsNullOrWhiteSpace(Globals.Instance.CurrentLang))
                {
                    return Globals.Instance.CurrentLang;
                }
            }
            catch
            {
            }

            return "";
        }

        private static string ReplaceColorMeaning(Match match)
        {
            if (match == null || !match.Success)
            {
                return " ";
            }

            string content = match.Groups["content"].Value;
            if (string.IsNullOrWhiteSpace(content) || SpriteMarkerRegex.IsMatch(content))
            {
                return content;
            }

            string speechContent = TagRegex.Replace(content, " ");
            speechContent = WebUtility.HtmlDecode(WhitespaceRegex.Replace(speechContent, " ").Trim());
            if (!IsBareQuantity(speechContent))
            {
                return content;
            }

            string unit = ColorUnit(match.Groups["color"].Value, speechContent);
            if (string.IsNullOrWhiteSpace(unit))
            {
                return content;
            }

            return content + " " + unit;
        }

        private static bool IsBareQuantity(string text)
        {
            return BareQuantityRegex.IsMatch(text) || string.Equals(text, "X", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string ColorUnit(string color, string quantity)
        {
            if (string.IsNullOrWhiteSpace(color))
            {
                return string.Empty;
            }

            string prefix;
            switch (color.ToUpperInvariant())
            {
            case "B00A00":
                prefix = "speech_unit_damage";
                break;
            case "1E650F":
                prefix = "speech_unit_heal";
                break;
            case "263ABC":
                prefix = "speech_unit_aura";
                break;
            case "720070":
                prefix = "speech_unit_curse";
                break;
            default:
                return string.Empty;
            }

            return Loc.Get(prefix + QuantityFormSuffix(quantity));
        }

        private static string QuantityFormSuffix(string quantity)
        {
            int number;
            if (!int.TryParse(quantity, out number))
            {
                return "_many";
            }

            int absolute = System.Math.Abs(number);
            int mod100 = absolute % 100;
            if (mod100 >= 11 && mod100 <= 14)
            {
                return "_many";
            }

            switch (absolute % 10)
            {
            case 1:
                return "_one";
            case 2:
            case 3:
            case 4:
                return "_few";
            default:
                return "_many";
            }
        }

        private static string ReplaceSpriteName(Match match)
        {
            if (match == null || match.Groups.Count < 2)
            {
                return " ";
            }

            return " " + GameText.SpriteName(match.Groups[1].Value) + " ";
        }

        private static bool HasRichTextMarkers(string text)
        {
            return text.IndexOf('<') >= 0;
        }

        private static bool NeedsWhitespaceNormalization(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r' || c == '\n' || c == '\t')
                {
                    return true;
                }

                if (c == ' ' && i + 1 < text.Length && text[i + 1] == ' ')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NeedsPunctuationNormalization(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            char previous = '\0';
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if ((current == ',' || current == '.' || current == ';' || current == ':' || current == '!' || current == '?' || current == '%') && previous == ' ')
                {
                    return true;
                }

                if ((current == ',' || current == '.' || current == ';' || current == ':' || current == '!' || current == '?') &&
                    (previous == ',' || previous == '.' || previous == ';' || previous == ':' || previous == '!' || previous == '?'))
                {
                    return true;
                }

                if (previous == '.' && current == ' ' && i + 1 < text.Length && text[i + 1] != ' ' && !char.IsDigit(text[i + 1]))
                {
                    return true;
                }

                previous = current;
            }

            return false;
        }

        private static string NormalizePunctuation(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(text.Length);
            bool pendingSpace = false;
            bool wroteNonPunctuation = false;
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (char.IsWhiteSpace(current))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (IsPausePunctuation(current))
                {
                    char collapsed = CollapsePunctuationAt(text, ref i);
                    if (!wroteNonPunctuation)
                    {
                        pendingSpace = false;
                        continue;
                    }

                    TrimTrailingSpace(builder);
                    if (collapsed == '.' && ShouldConvertMidSentencePeriod(text, i))
                    {
                        collapsed = ',';
                    }

                    if (builder.Length > 0 && builder[builder.Length - 1] == ',' && collapsed != ',')
                    {
                        builder.Length--;
                    }

                    builder.Append(collapsed);
                    pendingSpace = collapsed != '.';
                    continue;
                }

                if (current == '%')
                {
                    TrimTrailingSpace(builder);
                    builder.Append(current);
                    pendingSpace = false;
                    wroteNonPunctuation = true;
                    continue;
                }

                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(current);
                pendingSpace = false;
                wroteNonPunctuation = true;
            }

            return builder.ToString().Trim();
        }

        private static char CollapsePunctuationAt(string text, ref int index)
        {
            char best = text[index];
            while (index + 1 < text.Length)
            {
                int nextIndex = index + 1;
                while (nextIndex < text.Length && char.IsWhiteSpace(text[nextIndex]))
                {
                    nextIndex++;
                }

                if (nextIndex >= text.Length || !IsPausePunctuation(text[nextIndex]))
                {
                    break;
                }

                best = PreferredPunctuation(best, text[nextIndex]);
                index = nextIndex;
            }

            return best;
        }

        private static char PreferredPunctuation(char current, char candidate)
        {
            return PunctuationPriority(candidate) > PunctuationPriority(current) ? candidate : current;
        }

        private static int PunctuationPriority(char value)
        {
            switch (value)
            {
            case '!':
                return 6;
            case '?':
                return 5;
            case ':':
                return 4;
            case ';':
                return 3;
            case ',':
                return 2;
            default:
                return 1;
            }
        }

        private static bool ShouldConvertMidSentencePeriod(string text, int periodIndex)
        {
            if (periodIndex > 0 && char.IsDigit(text[periodIndex - 1]))
            {
                return false;
            }

            int next = periodIndex + 1;
            while (next < text.Length && char.IsWhiteSpace(text[next]))
            {
                next++;
            }

            return next < text.Length && !char.IsDigit(text[next]);
        }

        private static bool IsPausePunctuation(char value)
        {
            return value == ',' || value == '.' || value == ';' || value == ':' || value == '!' || value == '?';
        }

        private static void TrimTrailingSpace(System.Text.StringBuilder builder)
        {
            while (builder.Length > 0 && builder[builder.Length - 1] == ' ')
            {
                builder.Length--;
            }
        }

        private static string CollapseDuplicateHalves(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string[] words = text.Split(' ');
            if (words.Length < 2 || words.Length % 2 != 0)
            {
                return text;
            }

            int half = words.Length / 2;
            for (int i = 0; i < half; i++)
            {
                if (words[i] != words[i + half])
                {
                    return text;
                }
            }

            return string.Join(" ", words, 0, half);
        }
    }
}
