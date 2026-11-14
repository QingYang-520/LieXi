using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 剧情文本读取器（人名词典 + 嵌套拆分）。
///
/// 文件位置：Assets/Plot/*.txt
/// 格式约定：
///   - 一行一句，格式 "人名:正文"
///   - 冒号前是人名，冒号后是正文（半角 ":" 和全角 "：" 都认）
///   - 一行里如果嵌套了多句（如 "安勒:…是谁????:我叫米勒…安勒:那…"），会自动拆成多句
///   - 没有冒号的行 = 旁白
///
/// 拆分原理：
///   1. 先扫全文件，把每行第一个冒号前的部分收集成「人名词典」
///   2. 再逐行在「已知人名 + 冒号」处断开
///   这样正文里偶尔出现的冒号（如"想不起来:呢"）不会被误判成人名分隔。
/// </summary>
public static class PlotReader
{
    /// <summary>一句剧情</summary>
    public struct PlotLine
    {
        /// <summary>说话的人；旁白为空字符串</summary>
        public string speaker;
        /// <summary>说的内容</summary>
        public string content;

        public PlotLine(string speaker, string content)
        {
            this.speaker = speaker ?? "";
            this.content = content ?? "";
        }

        public bool IsNarration
        {
            get { return string.IsNullOrEmpty(speaker); }
        }

        public override string ToString()
        {
            return IsNarration ? content : (speaker + ":" + content);
        }
    }

    // ------------------------------------------------------------------ 对外接口

    /// <summary>
    /// 读取并解析剧情文件（不含扩展名），例如 Read("序章")。
    /// 结果已按句子拆好，可直接交给播放器。
    /// </summary>
    public static PlotLine[] Read(string plotName)
    {
        string text = LoadText(plotName);
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("PlotReader: 找不到剧情文件 Assets/Plot/" + plotName + ".txt");
            return new PlotLine[0];
        }
        return Parse(text);
    }

    /// <summary>读取原始文本（自动识别编码）</summary>
    public static string LoadText(string plotName)
    {
        if (string.IsNullOrEmpty(plotName))
            return null;

        string dir = System.IO.Path.Combine(Application.dataPath, "Plot");
        string path = System.IO.Path.Combine(dir, plotName + ".txt");
        if (!System.IO.File.Exists(path))
        {
            if (plotName.EndsWith(".txt"))
            {
                path = System.IO.Path.Combine(dir, plotName);
                if (!System.IO.File.Exists(path))
                    return null;
            }
            else
            {
                return null;
            }
        }

        return DecodeBytes(System.IO.File.ReadAllBytes(path));
    }

    // ------------------------------------------------------------------ 解析

    /// <summary>把整段文本解析成一句句台词</summary>
    public static PlotLine[] Parse(string text)
    {
        var result = new List<PlotLine>();
        if (string.IsNullOrEmpty(text))
            return result.ToArray();

        List<string> rawLines = SplitToLines(text);

        // ---- 第一步：收集人名词典 ----
        var names = new HashSet<string>();
        foreach (string raw in rawLines)
        {
            string line = raw.Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith("#") || line.StartsWith("//"))
                continue;

            int firstColon = IndexOfColon(line, 0);
            if (firstColon > 0)
            {
                string head = line.Substring(0, firstColon).Trim();
                if (LooksLikePersonName(head))
                    names.Add(head);
            }
        }

        // ---- 第二步：逐行拆分 ----
        foreach (string raw in rawLines)
        {
            string line = raw.Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith("#") || line.StartsWith("//"))
                continue;

            SplitLine(line, names, result);
        }

        return result.ToArray();
    }

    /// <summary>
    /// 拆分一行。在「已知人名 + 冒号」处断开；找不到人名就当旁白。
    /// </summary>
    private static void SplitLine(string line, HashSet<string> names, List<PlotLine> output)
    {
        // 找出所有 "已知人名:" 的位置
        var cuts = new List<int>();   // 记录每个分隔点：名字开始处
        var cutNames = new List<string>();

        int i = 0;
        while (i < line.Length)
        {
            if (line[i] == ':' || line[i] == '：')
            {
                // 从冒号往前匹配最长的人名
                string matched = null;
                int matchStart = -1;
                foreach (string n in names)
                {
                    if (n.Length == 0 || n.Length > i)
                        continue;
                    int start = i - n.Length;
                    if (start < 0)
                        continue;
                    if (string.CompareOrdinal(line, start, n, 0, n.Length) != 0)
                        continue;
                    // 前面必须是行首 / 上一句结尾 / 非名字字符
                    if (start > 0 && matched != null && n.Length <= matched.Length)
                        continue;
                    if (matched == null || n.Length > matched.Length)
                    {
                        matched = n;
                        matchStart = start;
                    }
                }

                if (matched != null)
                {
                    cuts.Add(matchStart);
                    cutNames.Add(matched);
                    i += 1;
                    continue;
                }
            }
            i++;
        }

        if (cuts.Count == 0)
        {
            // 没有任何已知人名：整行当旁白
            output.Add(new PlotLine("", line));
            return;
        }

        // 第一个分隔点前面若有零散文字，也当旁白
        if (cuts[0] > 0)
        {
            string lead = line.Substring(0, cuts[0]).Trim();
            if (lead.Length > 0)
                output.Add(new PlotLine("", lead));
        }

        // 按分隔点切分正文
        for (int k = 0; k < cuts.Count; k++)
        {
            string name = cutNames[k];
            int bodyStart = cuts[k] + name.Length + 1;   // 跳过名字 + 一个冒号
            int bodyEnd = (k + 1 < cuts.Count) ? cuts[k + 1] : line.Length;
            if (bodyStart > line.Length)
                bodyStart = line.Length;
            if (bodyEnd < bodyStart)
                bodyEnd = bodyStart;

            string body = line.Substring(bodyStart, bodyEnd - bodyStart).Trim();
            output.Add(new PlotLine(name, body));
        }
    }

    // ------------------------------------------------------------------ 工具

    /// <summary>按换行拆行，兼容 \r\n / \n / \r</summary>
    private static List<string> SplitToLines(string text)
    {
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] arr = normalized.Split('\n');
        var list = new List<string>(arr.Length);
        foreach (string s in arr)
            list.Add(s);
        return list;
    }

    private static int IndexOfColon(string s, int from)
    {
        for (int i = from; i < s.Length; i++)
        {
            if (s[i] == ':' || s[i] == '：')
                return i;
        }
        return -1;
    }

    /// <summary>判断是否像人名：1~12 字符，中文/字母/数字/问号/感叹号</summary>
    private static bool LooksLikePersonName(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length > 12)
            return false;
        foreach (char ch in s)
        {
            if (ch == '?' || ch == '？' || ch == '!' || ch == '！')
                continue;
            if (ch >= 0x4E00 && ch <= 0x9FFF)
                continue;
            if (ch >= 'a' && ch <= 'z') continue;
            if (ch >= 'A' && ch <= 'Z') continue;
            if (ch >= '0' && ch <= '9') continue;
            if (ch == ' ' || ch == '　') continue;
            return false;
        }
        return true;
    }

    /// <summary>自动识别编码：BOM 优先，其次严格 UTF-8，失败退回 GB18030</summary>
    private static string DecodeBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return "";

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        try
        {
            var strict = new UTF8Encoding(false, true);
            return strict.GetString(bytes);
        }
        catch (System.Exception)
        {
            try
            {
                return Encoding.GetEncoding("GB18030").GetString(bytes);
            }
            catch (System.Exception)
            {
                return Encoding.UTF8.GetString(bytes);
            }
        }
    }
}
