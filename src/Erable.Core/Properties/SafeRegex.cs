using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Erable.Core.Properties;

/// <summary>
/// The regular expressions accepted by the <c>match</c> rule: a subset with the same meaning in .NET and JavaScript,
/// immune to catastrophic backtracking on the server.
/// <para>
/// Accepted: literals, <c>.</c>, <c>^</c>, <c>$</c>, groups <c>( )</c> and <c>(?: )</c>, <c>|</c>, quantifiers
/// <c>* + ? {n} {n,} {n,m}</c> (greedy or lazy), classes <c>[…]</c> / <c>[^…]</c> with ranges, the escapes
/// <c>\d \D \w \W \s \S \t \n \r</c> and the escaped metacharacters <c>\ ^ $ . | ? * + ( ) [ ] { } / -</c>. Refused:
/// repeated groups containing a quantifier or an alternation (<c>(a+)+</c>, exponential in a backtracking engine),
/// back-references, look-arounds, named groups, inline options, Unicode classes, unescaped <c>] } {</c> outside of
/// their role, empty classes.
/// </para>
/// <para>
/// Both engines receive a <b>canonical</b> pattern without engine-specific meaning: literals as <c>\uXXXX</c>,
/// <c>\d \w \s</c> and <c>.</c> as explicit ASCII classes, capturing groups as non-capturing ones, <c>$</c> as the
/// true end of input (<c>\z</c> in .NET, where <c>$</c> also matches before a final line feed). The .NET side runs
/// with <see cref="RegexOptions.NonBacktracking"/> (linear time) and a timeout.
/// </para>
/// </summary>
public sealed class SafeRegex : IEquatable<SafeRegex>
{
    public const int MaxPatternLength = 500;
    public const int MaxRepeat = 1_000;

    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);
    private static readonly ConcurrentDictionary<string, Regex> Cache = new(StringComparer.Ordinal);

    private SafeRegex(string source, string dotNetPattern, string javaScriptPattern)
    {
        Source = source;
        DotNetPattern = dotNetPattern;
        JavaScriptPattern = javaScriptPattern;
    }

    /// <summary>The pattern as written by the user.</summary>
    public string Source { get; }

    /// <summary>Canonical pattern for .NET.</summary>
    public string DotNetPattern { get; }

    /// <summary>Canonical pattern for JavaScript (no flags); must equal what the SPA computes.</summary>
    public string JavaScriptPattern { get; }

    public static Result<SafeRegex> Parse(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length > MaxPatternLength)
        {
            return new DomainError(ErrorCodes.PropertyRuleInvalid, $"A pattern has at most {MaxPatternLength} characters.");
        }

        var parser = new Parser(pattern);
        var error = parser.Run();
        return error is null
            ? new SafeRegex(pattern, parser.Emit(dotNet: true), parser.Emit(dotNet: false))
            : new DomainError(ErrorCodes.PropertyRuleInvalid, $"Invalid pattern at {error.Value.Position}: {error.Value.Message}.");
    }

    /// <summary>True when the pattern is found in <paramref name="value"/>; a timeout counts as no match.</summary>
    public bool IsMatch(string value)
    {
        var regex = Cache.GetOrAdd(DotNetPattern, p => new Regex(p, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, Timeout));
        try
        {
            return regex.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public bool Equals(SafeRegex? other) => other is not null && Source == other.Source;

    public override bool Equals(object? obj) => Equals(obj as SafeRegex);

    public override int GetHashCode() => Source.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Source;

    /// <summary>Recursive descent over the subset; collects tokens of the canonical form.</summary>
    private sealed class Parser(string pattern)
    {
        private const string Metacharacters = "\\^$.|?*+()[]{}/-";
        private const string WhiteSpaceClass = @"\u0009\u000A\u000B\u000C\u000D\u0020";

        private readonly List<(string DotNet, string JavaScript)> _tokens = [];
        private int _position;

        // Quantifiers and alternations seen so far: a group that contains one cannot be quantified (see Sequence).
        private int _complexity;

        public (int Position, string Message)? Run()
        {
            try
            {
                Alternation();
                if (_position < pattern.Length)
                {
                    throw Fail(pattern[_position] == ')' ? "unbalanced ')'" : $"unexpected '{pattern[_position]}'");
                }

                return null;
            }
            catch (FormatException ex)
            {
                return (_position, ex.Message);
            }
        }

        public string Emit(bool dotNet) => string.Concat(_tokens.Select(t => dotNet ? t.DotNet : t.JavaScript));

        private void Alternation()
        {
            Sequence();
            while (Peek('|'))
            {
                _position++;
                _complexity++;
                Emit("|");
                Sequence();
            }
        }

        private void Sequence()
        {
            while (_position < pattern.Length && pattern[_position] is not ('|' or ')'))
            {
                var term = Term();
                if (_position < pattern.Length && pattern[_position] is '*' or '+' or '?' or '{')
                {
                    if (term == TermKind.Anchor)
                    {
                        throw Fail("nothing to repeat");
                    }

                    // (a+)+, (a|aa)*: exponential in a backtracking engine (the browser's). Refused on both sides.
                    if (term == TermKind.ComplexGroup)
                    {
                        throw Fail("a repeated group cannot contain a quantifier or an alternation");
                    }

                    Quantifier();
                    _complexity++;
                }
            }
        }

        private enum TermKind
        {
            Atom,
            Anchor,
            ComplexGroup,
        }

        /// <summary>One atom: anchors cannot be quantified, nor groups containing a quantifier or an alternation.</summary>
        private TermKind Term()
        {
            var c = pattern[_position];
            switch (c)
            {
                case '^':
                    _position++;
                    Emit("^");
                    return TermKind.Anchor;
                case '$':
                    _position++;
                    _tokens.Add((@"\z", "$"));
                    return TermKind.Anchor;
                case '.':
                    _position++;
                    Emit(@"[^\u000A\u000D\u2028\u2029]");
                    return TermKind.Atom;
                case '(':
                    _position++;
                    if (Peek('?'))
                    {
                        if (_position + 1 < pattern.Length && pattern[_position + 1] == ':')
                        {
                            _position += 2;
                        }
                        else
                        {
                            throw Fail("only (?: ) groups are supported");
                        }
                    }

                    Emit("(?:");
                    var before = _complexity;
                    Alternation();
                    if (!Peek(')'))
                    {
                        throw Fail("missing ')'");
                    }

                    _position++;
                    Emit(")");
                    return _complexity == before ? TermKind.Atom : TermKind.ComplexGroup;
                case '[':
                    Class();
                    return TermKind.Atom;
                case '\\':
                    Emit(Escape());
                    return TermKind.Atom;
                case '*' or '+' or '?' or '{':
                    throw Fail("nothing to repeat");
                case ']' or '}':
                    throw Fail($"'{c}' must be escaped");
                default:
                    _position++;
                    Emit(Literal(c));
                    return TermKind.Atom;
            }
        }

        private void Quantifier()
        {
            var c = pattern[_position++];
            string token;
            if (c == '{')
            {
                var min = Integer() ?? throw Fail("expected a number in { }");
                int? max = min;
                if (Peek(','))
                {
                    _position++;
                    max = Integer();
                }

                if (!Peek('}'))
                {
                    throw Fail("missing '}'");
                }

                _position++;
                if (min > MaxRepeat || max > MaxRepeat)
                {
                    throw Fail($"repetitions are limited to {MaxRepeat}");
                }

                if (max < min)
                {
                    throw Fail("{n,m} with m < n");
                }

                token = max == min ? $"{{{min}}}" : max is null ? $"{{{min},}}" : $"{{{min},{max}}}";
            }
            else
            {
                token = c.ToString();
            }

            if (Peek('?'))
            {
                _position++;
                token += "?";
            }

            Emit(token);
            if (_position < pattern.Length && pattern[_position] is '*' or '+' or '{' or '?')
            {
                throw Fail("nested quantifier");
            }
        }

        private int? Integer()
        {
            var start = _position;
            while (_position < pattern.Length && char.IsAsciiDigit(pattern[_position]) && _position - start < 5)
            {
                _position++;
            }

            return _position == start ? null : int.Parse(pattern.AsSpan(start, _position - start), CultureInfo.InvariantCulture);
        }

        private void Class()
        {
            _position++;
            var builder = new StringBuilder("[");
            if (Peek('^'))
            {
                _position++;
                builder.Append('^');
            }

            var count = 0;
            while (!Peek(']'))
            {
                if (_position >= pattern.Length)
                {
                    throw Fail("missing ']'");
                }

                var (low, set) = ClassAtom();
                if (set is not null)
                {
                    builder.Append(set);
                }
                else if (Peek('-') && _position + 1 < pattern.Length && pattern[_position + 1] != ']')
                {
                    _position++;
                    var (high, highSet) = ClassAtom();
                    if (highSet is not null || high < low)
                    {
                        throw Fail("invalid range");
                    }

                    builder.Append(Literal(low)).Append('-').Append(Literal(high));
                }
                else
                {
                    builder.Append(Literal(low));
                }

                count++;
            }

            if (count == 0)
            {
                throw Fail("empty class");
            }

            _position++;
            Emit(builder.Append(']').ToString());
        }

        /// <summary>A character of a class, or a predefined set (<c>\d \w \s</c>, expanded).</summary>
        private (char Char, string? Set) ClassAtom()
        {
            var c = pattern[_position];
            if (c == '[')
            {
                throw Fail("'[' must be escaped in a class");
            }

            if (c != '\\')
            {
                _position++;
                return (c, null);
            }

            if (_position + 1 >= pattern.Length)
            {
                throw Fail("incomplete escape");
            }

            var next = pattern[_position + 1];
            switch (next)
            {
                case 'd':
                    _position += 2;
                    return ('\0', @"\u0030-\u0039");
                case 'w':
                    _position += 2;
                    return ('\0', @"\u0041-\u005A\u0061-\u007A\u0030-\u0039\u005F");
                case 's':
                    _position += 2;
                    return ('\0', WhiteSpaceClass);
                case 'D' or 'W' or 'S':
                    throw Fail($"\\{next} is not supported in a class");
                default:
                    return (EscapedChar(), null);
            }
        }

        private string Escape()
        {
            if (_position + 1 >= pattern.Length)
            {
                throw Fail("incomplete escape");
            }

            switch (pattern[_position + 1])
            {
                case 'd':
                    _position += 2;
                    return @"[\u0030-\u0039]";
                case 'D':
                    _position += 2;
                    return @"[^\u0030-\u0039]";
                case 'w':
                    _position += 2;
                    return @"[\u0041-\u005A\u0061-\u007A\u0030-\u0039\u005F]";
                case 'W':
                    _position += 2;
                    return @"[^\u0041-\u005A\u0061-\u007A\u0030-\u0039\u005F]";
                case 's':
                    _position += 2;
                    return "[" + WhiteSpaceClass + "]";
                case 'S':
                    _position += 2;
                    return "[^" + WhiteSpaceClass + "]";
                default:
                    return Literal(EscapedChar());
            }
        }

        private char EscapedChar()
        {
            var next = pattern[_position + 1];
            var value = next switch
            {
                't' => '\t',
                'n' => '\n',
                'r' => '\r',
                _ when Metacharacters.Contains(next, StringComparison.Ordinal) => next,
                _ => throw Fail($"unsupported escape \\{next}"),
            };
            _position += 2;
            return value;
        }

        private static string Literal(char c) => "\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);

        private bool Peek(char c) => _position < pattern.Length && pattern[_position] == c;

        private void Emit(string token) => _tokens.Add((token, token));

        private static FormatException Fail(string message) => new(message);
    }
}
