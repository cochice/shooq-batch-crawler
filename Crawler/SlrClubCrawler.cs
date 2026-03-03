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
    public class SlrClubCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;
            string? url = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[0];
            string? no = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[1];
            string setUrlVal = string.IsNullOrEmpty(url) ? Site.SlrClub.url : url;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SLR클럽{(string.IsNullOrEmpty(url) ? "" : " 상세")} 크롤링 시작: {setUrlVal}");

            var options = GetChromeOptions();
            using var driver = new ChromeDriver(options);

            // 타임아웃 설정
            SetupDriverTimeouts(driver);

            try
            {
                driver.Navigate().GoToUrl(setUrlVal);

                // webdriver 속성 숨기기
                ((IJavaScriptExecutor)driver).ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

                // 페이지 로딩 대기
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                wait.Until(d => d.FindElement(By.TagName("body")));

                var html = driver.PageSource;
                Console.WriteLine($"HTML 길이: {html.Length}");

                htmlFileName = $"{Define.HtmlPath}temp_{Site.SlrClub.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                await File.WriteAllTextAsync(htmlFileName, html, Encoding.UTF8);
                Console.WriteLine($"임시 파일 생성: {htmlFileName}");

                // 파일에서 파싱
                if (string.IsNullOrEmpty(url))
                    posts = await ParseHtmlFile(htmlFileName);
                else
                    posts = await ParseHtmlFileForDetail(htmlFileName, url, no);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SLR클럽 크롤링 오류: {ex.Message}");
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

                // SLR클럽 게시글 테이블 파싱
                var rows = doc.DocumentNode.SelectNodes("//table[@class='bbs_tbl_layout']//tbody//tr");

                if (rows == null || rows.Count == 0)
                {
                    Console.WriteLine("게시글을 찾을 수 없습니다.");
                    return posts;
                }

                foreach (var row in rows)
                {
                    try
                    {
                        // 헤더 행 건너뛰기
                        if (row.SelectNodes("th") != null) continue;

                        var tds = row.SelectNodes("td");
                        if (tds == null || tds.Count < 5) continue;

                        var post = new PostInfo();

                        // 제목, URL, 댓글
                        var titleLink = row.SelectSingleNode(".//td[@class='sbj']");
                        if (titleLink != null)
                        {
                            var urlNode = titleLink.SelectSingleNode("./a");

                            // URL
                            var href = urlNode.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                            {
                                post.Url = href.StartsWith("http") ? href.Replace("amp;", "") : $"https://www.slrclub.com{href.Replace("amp;", "")}";
                            }

                            // 제목
                            post.Title = urlNode.InnerText.CleanText();

                            Func<string, string?> funcExtraReplyCount = (text) =>
                            {
                                var match = Regex.Match(text, @"\[(\d+)\]");
                                if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                                    return count.ToString();

                                return null;
                            };

                            // 댓글
                            post.ReplyNum = funcExtraReplyCount(titleLink.InnerText.CleanText());
                        }

                        // 작성자
                        post.Author = row.SelectSingleNode(".//td[@class='list_name']/span[@class='lop']").InnerText.Trim().CleanText() ?? "";

                        // 날짜
                        var dateText = row.SelectSingleNode(".//td[@class='list_date no_att']").InnerText.Trim().CleanText() ?? "";
                        if (!string.IsNullOrEmpty(dateText))
                        {
                            // 22:26:01 :: 25/08/29
                            if (dateText.IndexOf(':') > -1)
                            {
                                var dateTime = DateTime.ParseExact(dateText, "HH:mm:ss", CultureInfo.InvariantCulture);
                                TimeSpan difference = dateTime - DateTime.Now;
                                if (difference.TotalSeconds > 0) // 미래 시간인 경우
                                {
                                    dateTime.AddDays(-1);
                                }

                                post.Date = $"{dateTime:yyyy-MM-dd} {dateText}";
                            }
                            else if (dateText.IndexOf('/') > -1)
                            {
                                var dateTime = DateTime.ParseExact(dateText, "yy/MM/dd", CultureInfo.InvariantCulture);
                                TimeSpan difference = dateTime - DateTime.Now;
                                if (difference.TotalSeconds > 0) // 미래 시간인 경우
                                {
                                    dateTime.AddDays(-1);
                                }

                                post.Date = $"{dateTime:yyyy-MM-dd}";
                            }
                        }

                        // 조회수
                        var viewsText = row.SelectSingleNode(".//td[@class='list_click no_att']").InnerText.Trim().CleanText() ?? "";
                        post.Views = Regex.Replace(viewsText, @"[^\d]", "");

                        // 추천수
                        var likesText = row.SelectSingleNode(".//td[@class='list_vote no_att']").InnerText.Trim().CleanText() ?? "";
                        post.Likes = Regex.Replace(likesText, @"[^\d]", "");

                        if (!string.IsNullOrEmpty(post.Title))
                        {
                            posts.Add(post);
                            post.PostPrint();
                        }
                    }
                    catch
                    {
                        // 개별 행 파싱 오류는 무시하고 계속
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

        public async Task<List<PostInfo>?> ParseHtmlFileForDetail(string filePath, string url, string no)
        {
            var posts = new List<PostInfo>();

            try
            {
                int.TryParse(no, out int orgNo);

                Console.WriteLine($"SLR클럽 상세 HTML 파일 파싱 중...");

                // 파일에서 HTML 읽기
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // HtmlAgilityPack으로 파싱
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var contentDiv = doc.DocumentNode.SelectSingleNode(".//div[@id='userct']");

                var result = await ProcessChildNodes(contentDiv, orgNo);

                if (!string.IsNullOrWhiteSpace(result.Item1))
                {
                    var cleanContent = new StringBuilder();
                    cleanContent.AppendLine("<article class='shooq-content'>");
                    cleanContent.Append(result.Item1);
                    cleanContent.AppendLine("</article>");
                    await new DatabaseManager().EditDetailPostCount(new PostInfo() { Url = url, Content = cleanContent.ToString(), img2 = result.Item2 });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SLR클럽 상세 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }
    }
}