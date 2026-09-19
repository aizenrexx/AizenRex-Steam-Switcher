using System.IO;
using System.Text;

namespace SteamSwitcher.Core;

/// <summary>
/// A minimal reader/writer for Valve's KeyValues (.vdf) text format.
/// <para>
/// This exists because editing a VDF with regular expressions is unsafe: a
/// pattern like "MostRecent" matches EVERY account in loginusers.vdf, so a
/// single replace silently flags all of them, and Steam then picks an
/// arbitrary account to sign in as. It also cannot add a key that is absent.
/// Parsing the real structure lets us change exactly one account and insert
/// keys that are missing.
/// </para>
/// </summary>
public sealed class VdfNode
{
    public string Key { get; set; } = "";

    /// <summary>Value for a leaf node. Null when this node has children.</summary>
    public string? Value { get; set; }

    public List<VdfNode> Children { get; } = new();

    public bool IsSection => Value is null;

    public VdfNode? Child(string key) =>
        Children.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

    public string? ChildValue(string key) => Child(key)?.Value;

    /// <summary>Sets a leaf value, creating the key when it does not exist yet.</summary>
    public void SetChild(string key, string value)
    {
        var existing = Child(key);
        if (existing is not null)
        {
            existing.Value = value;
            existing.Children.Clear();
        }
        else
        {
            Children.Add(new VdfNode { Key = key, Value = value });
        }
    }
}

public static class VdfDocument
{
    /// <summary>Parses VDF text into a synthetic root whose children are the top-level nodes.</summary>
    public static VdfNode Parse(string text)
    {
        var root = new VdfNode { Key = "__root__" };
        var stack = new Stack<VdfNode>();
        stack.Push(root);

        int i = 0;
        string? pendingKey = null;

        while (i < text.Length)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            // Comments run to end of line.
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }

            if (c == '{')
            {
                // The key read just before the brace names this section.
                var section = new VdfNode { Key = pendingKey ?? "" };
                stack.Peek().Children.Add(section);
                stack.Push(section);
                pendingKey = null;
                i++;
                continue;
            }

            if (c == '}')
            {
                if (stack.Count > 1) stack.Pop();
                i++;
                continue;
            }

            if (c == '"')
            {
                var token = ReadQuoted(text, ref i);
                if (pendingKey is null)
                {
                    pendingKey = token;
                }
                else
                {
                    stack.Peek().Children.Add(new VdfNode { Key = pendingKey, Value = token });
                    pendingKey = null;
                }
                continue;
            }

            // Unquoted token (rare, but valid in some Valve files).
            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '"' && text[i] != '{' && text[i] != '}') i++;
            var raw = text.Substring(start, i - start);
            if (pendingKey is null) pendingKey = raw;
            else { stack.Peek().Children.Add(new VdfNode { Key = pendingKey, Value = raw }); pendingKey = null; }
        }

        return root;
    }

    private static string ReadQuoted(string text, ref int i)
    {
        i++; // opening quote
        var sb = new StringBuilder();
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length)
            {
                char n = text[i + 1];
                sb.Append(n switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '\\' => '\\',
                    '"' => '"',
                    _ => n
                });
                i += 2;
                continue;
            }
            if (c == '"') { i++; break; }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>Writes the tree back out in Valve's tab-indented layout.</summary>
    public static string Write(VdfNode root)
    {
        var sb = new StringBuilder();
        foreach (var child in root.Children) WriteNode(sb, child, 0);
        return sb.ToString();
    }

    private static void WriteNode(StringBuilder sb, VdfNode node, int depth)
    {
        var indent = new string('\t', depth);

        if (node.IsSection)
        {
            sb.Append(indent).Append('"').Append(Escape(node.Key)).Append("\"\n");
            sb.Append(indent).Append("{\n");
            foreach (var child in node.Children) WriteNode(sb, child, depth + 1);
            sb.Append(indent).Append("}\n");
        }
        else
        {
            sb.Append(indent)
              .Append('"').Append(Escape(node.Key)).Append('"')
              .Append("\t\t")
              .Append('"').Append(Escape(node.Value ?? "")).Append("\"\n");
        }
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>Reads and parses a file, returning null when it cannot be read.</summary>
    public static VdfNode? Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not parse {Path.GetFileName(path)}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Writes through a temp file then replaces the original, so an
    /// interrupted write can never leave a truncated VDF behind.
    /// </summary>
    public static bool Save(string path, VdfNode root)
    {
        try
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, Write(root));

            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Could not write {Path.GetFileName(path)}", ex);
            return false;
        }
    }
}
