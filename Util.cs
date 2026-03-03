using HtmlAgilityPack;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Marvin.Tmthfh91.Crawling.Crawler;

public static class Util
{
    /// <summary>
    /// 
    /// </summary>
    private static Random rng = new Random();

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    /// <summary>
    /// 텍스트 정리 메서드
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string CleanText(this string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // \r, \n, \t 제거 및 연속 공백 정리
        return text.Replace("\r", "")
                  .Replace("\n", " ")
                  .Replace("\t", " ")
                  .Trim()
                  .Replace("  ", " "); // 연속 공백을 하나로
    }
}

// 실용적인 확장 메서드
public static class HtmlNodeExtensions
{
    /// <summary>
    /// 자식 요소의 텍스트를 제외하고 해당 노드의 직접적인 텍스트만 반환
    /// </summary>
    public static string GetDirectText(this HtmlNode node)
    {
        if (node == null) return string.Empty;

        return string.Join("",
            node.ChildNodes
                .Where(n => n.NodeType == HtmlNodeType.Text)
                .Select(n => n.InnerText))
            .Trim();
    }

    /// <summary>
    /// 특정 자식 요소들을 제외하고 텍스트 추출
    /// </summary>
    public static string GetTextExcluding(this HtmlNode node, params string[] excludeSelectors)
    {
        if (node == null) return string.Empty;

        var clone = node.CloneNode(true);

        foreach (var selector in excludeSelectors)
        {
            var nodesToRemove = clone.SelectNodes(selector);
            if (nodesToRemove != null)
            {
                foreach (var nodeToRemove in nodesToRemove.ToList())
                {
                    nodeToRemove.Remove();
                }
            }
        }

        return clone.InnerText.Trim();
    }

    /// <summary>
    /// 포스트 결과 출력
    /// </summary>
    /// <param name="post"></param>
    public static void PostPrint(this Marvin.Tmthfh91.Crawling.Model.PostInfo post)
    {
        Console.WriteLine($"  [{post.Date}] reply[{post.ReplyNum}], views[{post.Views}], likes[{post.Likes}], {post.Title} - {post.Author}");
    }

    /// <summary>
    /// 숫자에 K 있으면 *1000
    /// </summary>
    /// <param name="countText"></param>
    /// <returns></returns>
    public static int? ConvertToIntFromK(this string countText)
    {
        int? intValue = null;
        if (countText.EndsWith("k", StringComparison.OrdinalIgnoreCase))
        {
            var numPart = countText.Substring(0, countText.Length - 1);
            if (double.TryParse(numPart, out double result))
            {
                intValue = (int)(result * 1000);
            }
        }
        else if (int.TryParse(countText, out int directValue))
        {
            intValue = directValue;
        }

        return intValue == 0 ? null : intValue;
    }

    // 단일 HTML에서 조회수 추출 (일반 숫자 + k/m/b 단위 지원)
    public static int? ExtractViewCount(this HtmlNode divElement)
    {
        try
        {
            // 텍스트 노드에서 조회수 찾기
            foreach (var node in divElement.ChildNodes)
            {
                if (node.NodeType == HtmlNodeType.Text)
                {
                    string text = node.InnerText.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        int viewCount = ParseViewCount(text);
                        if (viewCount > 0) return viewCount;
                    }
                }
            }

            // 대안: InnerText에서 전체 검색
            return ParseViewCount(divElement.InnerText);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"조회수 추출 중 오류: {ex.Message}");
        }

        return null;
    }

    // 텍스트에서 조회수 파싱 (k, m, b 단위 지원)
    private static int ParseViewCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        // 숫자와 단위 패턴 찾기 (예: 1.7k, 748, 2.5m)
        var match = Regex.Match(text, @"[\d.,]+[kmb]?", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return ConvertToNumber(match.Value);
        }

        return 0;
    }

    private static int ConvertToNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;

        value = value.Trim().ToLower();

        try
        {
            // k, m, b 단위 확인
            char lastChar = value[value.Length - 1];
            double multiplier = 1;
            string numberPart = value;

            switch (lastChar)
            {
                case 'k':
                    multiplier = 1000;
                    numberPart = value.Substring(0, value.Length - 1);
                    break;
                case 'm':
                    multiplier = 1000000;
                    numberPart = value.Substring(0, value.Length - 1);
                    break;
                case 'b':
                    multiplier = 1000000000;
                    numberPart = value.Substring(0, value.Length - 1);
                    break;
            }

            // 숫자 부분 파싱 (쉼표 제거)
            numberPart = numberPart.Replace(",", "");
            if (double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                return (int)Math.Round(number * multiplier);
            }
        }
        catch (Exception)
        {
            // 에러 무시하고 0 반환
        }

        return 0;
    }

    /// <summary>
    /// 날짜변환
    /// </summary>
    /// <param name="dateText">텍스트</param>
    /// </summary>
    /// <returns>(성공여부, 결과)</returns>
    public static (bool Success, string Result) ConvertToStandardDateTime(this string dateText)
    {
        var now = DateTime.Now;
        var yesterday = now.AddDays(-1);

        try
        {
            dateText = Regex.Replace(dateText, @"[^0-9:.어제\s-]", "").Trim().CleanText();

            // 1. "07.09 15:06" 형식 (올해 월.일 시:분)
            if (Regex.IsMatch(dateText, @"^\d{2}\.\d{2} \d{2}:\d{2}$"))
            {
                var parts = dateText.Split(' ');
                var datePart = parts[0]; // "07.09"
                var timePart = parts[1]; // "15:06"

                var dateOnly = $"{now.Year}.{datePart}";
                var fullDateTime = DateTime.ParseExact($"{dateOnly} {timePart}", "yyyy.MM.dd HH:mm", null);

                return (true, fullDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            // 2. "2024.12.04" 형식 (년.월.일만)
            if (Regex.IsMatch(dateText, @"^\d{4}\.\d{2}\.\d{2}$"))
            {
                var fullDateTime = DateTime.ParseExact(dateText, "yyyy.MM.dd", null);
                return (true, fullDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            // 3. "어제 15:35" 형식
            if (dateText.StartsWith("어제 ") && Regex.IsMatch(dateText, @"어제 \d{2}:\d{2}$"))
            {
                var timePart = dateText.Substring(3); // "15:35"
                var timeOnly = TimeSpan.ParseExact(timePart, @"hh\:mm", null);
                var fullDateTime = yesterday.Date.Add(timeOnly);

                return (true, fullDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            // 4. "15:35" 형식
            if (Regex.IsMatch(dateText, @"\d{2}:\d{2}$"))
            {
                var timeOnly = TimeSpan.ParseExact(dateText, @"hh\:mm", null);

                return (true, $"{DateTime.Now:yyyy-MM-dd} {dateText}:00");
            }

            return (false, "알 수 없는 형식");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// 끝에 괄호 숫자 부분 제거
    /// 예시) 어쩌고 저쩌고 (17)
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string RemoveBracketNumber(this string input)
    {
        // 문자열 끝에서 공백을 포함한 괄호와 숫자 패턴을 제거
        // \s* : 0개 이상의 공백문자
        // \( : 여는 괄호 (이스케이프 필요)
        // \d+ : 1개 이상의 숫자
        // \) : 닫는 괄호 (이스케이프 필요)
        // \s* : 0개 이상의 공백문자
        // $ : 문자열의 끝
        string pattern = @"\s*\(\d+\)\s*$";
        return Regex.Replace(input, pattern, "").TrimEnd();
    }

    /// <summary>
    /// 괄호 숫자 부분에서 숫자만 추출
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string ExtractNumberFromBrackets(this string input)
    {
        // 괄호 안의 숫자를 찾는 패턴
        // \( : 여는 괄호
        // (\d+) : 숫자를 그룹으로 캡처
        // \) : 닫는 괄호
        string pattern = @"\((\d+)\)";
        Match match = Regex.Match(input, pattern);

        if (match.Success)
        {
            return match.Groups[1].Value; // 첫 번째 그룹(숫자)을 반환
        }

        return ""; // 숫자가 없으면 빈 문자열 반환
    }

    /// <summary>
    /// 문자열 앞에 특정 문자열 제거하고 trim
    /// </summary>
    /// <param name="input"></param>
    /// <param name="prefix"></param>
    /// <returns></returns>
    public static string RemovePrefixAndTrim(this string input, string prefix)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(prefix))
            return input?.Trim() ?? "";

        // 정규식으로 문자열 시작 부분의 특정 패턴 제거
        // Regex.Escape()로 특수문자를 이스케이프 처리
        // ^ : 문자열의 시작
        // \s* : 0개 이상의 공백문자
        string escapedPrefix = Regex.Escape(prefix);
        string pattern = $@"^\s*{escapedPrefix}\s*";

        string result = Regex.Replace(input, pattern, "");
        return result.Trim();
    }

    public static async Task<int?> UploadImageAndReturnId(this List<CrawledImage> crawledImages)
    {
        var uploader = new SimpleCloudinaryUploader();
        var repository = new ImageRepository();
        var processor = new ImageProcessingService(uploader, repository);

        try
        {
            var result = await processor.ProcessCrawledImages(crawledImages);

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"처리 중 오류: {ex.Message}");

            return null;
        }
    }
}