using HtmlAgilityPack;
using Marvin.Tmthfh91.Crawling.Crawler;
using Marvin.Tmthfh91.Crawling.Model;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Marvin.Tmthfh91.Crawling.Crawler
{
    public class EtolandCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  Etoland 크롤링 시작: {Site.Etoland.url}");

            var options = GetChromeOptions();
            using var driver = new ChromeDriver(options);

            // 타임아웃 설정
            SetupDriverTimeouts(driver);

            try
            {
                driver.Navigate().GoToUrl(Site.Etoland.url);

                // webdriver 속성 숨기기
                ((IJavaScriptExecutor)driver).ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

                // 페이지 로딩 대기
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                wait.Until(d => d.FindElement(By.TagName("body")));

                var html = driver.PageSource;
                Console.WriteLine($"HTML 길이: {html.Length}");

                htmlFileName = $"{Define.HtmlPath}temp_{Site.Etoland.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                await File.WriteAllTextAsync(htmlFileName, html, Encoding.UTF8);
                Console.WriteLine($"임시 파일 생성: {htmlFileName}");

                // 파일에서 파싱
                if (htmlFileName != null) posts = await ParseHtmlFile(htmlFileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Etoland 크롤링 오류: {ex.Message}");
            }
            finally
            {
                // HTML 파일 삭제 (성공적으로 파싱된 경우만)
                if (!string.IsNullOrEmpty(htmlFileName) && File.Exists(htmlFileName))
                {
                    try
                    {
                        // if (posts != null && posts.Count > 0)
                        // {
                        File.Delete(htmlFileName);
                        Console.WriteLine($"임시 파일 삭제: {htmlFileName}");
                        // }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"파일 삭제 실패: {ex.Message}");
                    }
                }
            }

            return posts;
        }

        public async Task<List<PostInfo>> ParseHtmlFile(string filePath)
        {
            var posts = new List<PostInfo>();

            try
            {
                Console.WriteLine($"HTML 파일 파싱 중...");

                // 파일에서 HTML 읽기
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // HtmlAgilityPack으로 파싱
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Etoland 게시글 파싱 - 리스트 기반 구조
                var postItems = doc.DocumentNode.SelectNodes("//div[contains(@class, 'power_link')] | //li[contains(@class, 'power_link')] | //div[contains(@class, 'hit_list')]");

                if (postItems == null || postItems.Count == 0)
                {
                    // 테이블 형태도 시도
                    var rows = doc.DocumentNode.SelectNodes("//table//tbody//tr | //table//tr[td]");
                    if (rows != null)
                    {
                        return await ParseTableFormat(rows);
                    }
                }

                if (postItems == null || postItems.Count == 0)
                {
                    Console.WriteLine("게시글을 찾을 수 없습니다.");
                    return posts;
                }

                int postCounter = 1;
                foreach (var item in postItems)
                {
                    try
                    {
                        var post = new PostInfo();
                        post.Number = postCounter.ToString();

                        // 제목과 URL 추출
                        var titleNode = item.SelectSingleNode(".//a[@class='power_link_click'] | .//a[contains(@class, 'subject')] | .//div[contains(@class, 'subject')]/a");
                        if (titleNode != null)
                        {
                            post.Title = titleNode.InnerText.CleanText();
                            var href = titleNode.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                            {
                                post.Url = href.StartsWith("http") ? href : $"https://etoland.co.kr{href}";
                            }
                        }
                        else
                        {
                            // 제목만 있는 경우
                            var titleTextNode = item.SelectSingleNode(".//div[contains(@class, 'subject_txt')] | .//span[contains(@class, 'subject')]");
                            if (titleTextNode != null)
                            {
                                post.Title = titleTextNode.InnerText.CleanText();
                            }
                        }

                        // URL에서 게시글 번호 추출
                        if (!string.IsNullOrEmpty(post.Url))
                        {
                            var urlNumberMatch = Regex.Match(post.Url, @"wr_id=(\d+)|no=(\d+)|idx=(\d+)");
                            if (urlNumberMatch.Success)
                            {
                                for (int i = 1; i <= 3; i++)
                                {
                                    if (urlNumberMatch.Groups[i].Success)
                                    {
                                        post.Number = urlNumberMatch.Groups[i].Value;
                                        break;
                                    }
                                }
                            }
                        }

                        // 작성자 추출
                        var authorNode = item.SelectSingleNode(".//span[contains(@class, 'user')] | .//div[contains(@class, 'author')] | .//span[contains(@class, 'name')]");
                        if (authorNode != null)
                        {
                            post.Author = authorNode.InnerText.CleanText();
                        }

                        // 날짜 추출
                        var dateNode = item.SelectSingleNode(".//span[contains(@class, 'date')] | .//div[contains(@class, 'date')] | .//time");
                        if (dateNode != null)
                        {
                            var dateText = dateNode.InnerText.CleanText();
                            if (Regex.IsMatch(dateText, @"\d{2}:\d{2}"))
                            {
                                post.Date = $"{DateTime.Now:yyyy-MM-dd} {dateText}";
                            }
                            else
                            {
                                post.Date = dateText;
                            }
                        }

                        // 조회수 추출
                        var viewsNode = item.SelectSingleNode(".//span[contains(text(), '조회')] | .//div[contains(text(), '조회')] | .//span[contains(@class, 'view')]");
                        if (viewsNode != null)
                        {
                            var viewsText = viewsNode.InnerText.CleanText();
                            post.Views = Regex.Replace(viewsText, @"[^\d,]", "").Replace(",", "");
                        }

                        // 추천수 추출
                        var likesNode = item.SelectSingleNode(".//span[contains(text(), '추천')] | .//div[contains(text(), '추천')] | .//span[contains(@class, 'like')]");
                        if (likesNode != null)
                        {
                            var likesText = likesNode.InnerText.CleanText();
                            post.Likes = Regex.Replace(likesText, @"[^\d]", "");
                        }

                        // 댓글 수 추출
                        var replyMatch = Regex.Match(item.InnerText, @"댓글\s*(\d+)|\[(\d+)\]|\((\d+)\)");
                        if (replyMatch.Success)
                        {
                            for (int i = 1; i <= 3; i++)
                            {
                                if (replyMatch.Groups[i].Success)
                                {
                                    post.ReplyNum = replyMatch.Groups[i].Value;
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(post.Title))
                        {
                            posts.Add(post);
                            post.PostPrint();
                            postCounter++;
                        }
                    }
                    catch
                    {
                        // 개별 아이템 파싱 오류는 무시하고 계속
                        continue;
                    }
                }

                Console.WriteLine($"총 {posts.Count}개의 게시글을 파싱했습니다.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }

        private Task<List<PostInfo>> ParseTableFormat(HtmlNodeCollection rows)
        {
            var posts = new List<PostInfo>();

            foreach (var row in rows)
            {
                try
                {
                    var tds = row.SelectNodes("td");
                    if (tds == null || tds.Count < 3) continue;

                    var post = new PostInfo();

                    // 헤더나 공지사항 건너뛰기
                    var firstCellText = tds[0].InnerText.Trim().CleanText() ?? "";
                    if (string.IsNullOrEmpty(firstCellText) || firstCellText.Contains("번호") ||
                        firstCellText.Contains("공지"))
                    {
                        continue;
                    }

                    // 번호 추출
                    var numberMatch = Regex.Match(firstCellText, @"\d+");
                    if (numberMatch.Success)
                    {
                        post.Number = numberMatch.Value;
                    }

                    // 제목과 URL 찾기
                    HtmlNode? titleTd = null;
                    for (int i = 1; i < tds.Count; i++)
                    {
                        var linkNode = tds[i].SelectSingleNode(".//a");
                        if (linkNode != null && !string.IsNullOrEmpty(linkNode.InnerText.Trim()))
                        {
                            titleTd = tds[i];
                            break;
                        }
                    }

                    if (titleTd != null)
                    {
                        var titleLink = titleTd.SelectSingleNode(".//a");
                        if (titleLink != null)
                        {
                            post.Title = titleLink.InnerText.CleanText();
                            var href = titleLink.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                            {
                                post.Url = href.StartsWith("http") ? href : $"https://etoland.co.kr{href}";
                            }
                        }
                    }

                    // 작성자, 날짜, 조회수 등 추가 정보 추출
                    for (int i = 2; i < tds.Count; i++)
                    {
                        var cellText = tds[i].InnerText.Trim().CleanText();

                        // 작성자
                        if (string.IsNullOrEmpty(post.Author) && !string.IsNullOrEmpty(cellText) &&
                            !Regex.IsMatch(cellText, @"^\d+$") && !cellText.Contains(":") && cellText.Length < 20)
                        {
                            post.Author = cellText;
                        }
                        // 날짜
                        else if (string.IsNullOrEmpty(post.Date) && Regex.IsMatch(cellText, @"\d{2}:\d{2}"))
                        {
                            post.Date = $"{DateTime.Now:yyyy-MM-dd} {cellText}";
                        }
                        // 조회수
                        else if (string.IsNullOrEmpty(post.Views) && Regex.IsMatch(cellText, @"^\d+$"))
                        {
                            post.Views = cellText;
                        }
                    }

                    if (!string.IsNullOrEmpty(post.Title) && !string.IsNullOrEmpty(post.Number))
                    {
                        posts.Add(post);
                        Console.WriteLine($"  [{post.Number}] {post.Title} - {post.Author}");
                    }
                }
                catch
                {
                    continue;
                }
            }

            return Task.FromResult(posts);
        }
    }
}