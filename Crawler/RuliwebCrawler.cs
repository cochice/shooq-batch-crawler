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
    public class RuliwebCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;
            string? url = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[0];
            string? no = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[1];
            string setUrlVal = string.IsNullOrEmpty(url) ? Site.Ruliweb.url : url;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Ruliweb{(string.IsNullOrEmpty(url) ? "" : " 상세")} 크롤링 시작: {setUrlVal}");

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

                htmlFileName = $"{Define.HtmlPath}temp_{Site.Ruliweb.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
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
                Console.WriteLine($"Ruliweb 크롤링 오류: {ex.Message}");
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

                // Ruliweb 게시글 테이블 파싱
                var rows = doc.DocumentNode.SelectNodes("//table[@class='board_list_table']//tr | //table[@class='board_main']//tbody//tr | //table[contains(@class, 'table_body')]//tr | //div[@class='best_list']//tr");

                if (rows == null || rows.Count == 0)
                {
                    // 다른 패턴도 시도
                    rows = doc.DocumentNode.SelectNodes("//table//tbody//tr | //table//tr[td]");
                }

                if (rows == null || rows.Count == 0)
                {
                    Console.WriteLine("게시글을 찾을 수 없습니다.");
                    return posts;
                }

                foreach (var row in rows)
                {
                    try
                    {
                        var tds = row.SelectNodes("td");
                        if (tds == null || tds.Count < 4) continue;

                        var post = new PostInfo();

                        post.Author = tds[2].InnerText.Trim().CleanText();
                        post.Likes = tds[3].InnerText.Trim().CleanText();
                        post.Views = tds[4].InnerText.Trim().CleanText();

                        // 제목과 URL 찾기 (보통 두 번째나 세 번째 td에 위치)
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
                                // 제목에서 추천수 패턴 제거 [숫자] 또는 (숫자) 형태
                                var rawTitle = titleLink.InnerText.CleanText();
                                rawTitle = Regex.Replace(rawTitle, @"^\d+", "");
                                post.Title = rawTitle.RemoveBracketNumber();

                                // 댓글 저장
                                post.ReplyNum = rawTitle.ExtractNumberFromBrackets();

                                var href = titleLink.GetAttributeValue("href", "");
                                if (!string.IsNullOrEmpty(href))
                                {
                                    post.Url = href.StartsWith("http") ? href : $"https://bbs.ruliweb.com{href}";

                                    // URL에서 게시글 번호 추출
                                    var urlNumberMatch = Regex.Match(href, @"/read/(\d+)");
                                    if (urlNumberMatch.Success && string.IsNullOrEmpty(post.Number))
                                    {
                                        post.Number = urlNumberMatch.Groups[1].Value;
                                    }
                                }
                            }
                        }

                        // 날짜 찾기 (마지막 몇 개 td 중에서)
                        for (int i = Math.Max(2, tds.Count - 3); i < tds.Count; i++)
                        {
                            var dateText = tds[i].InnerText.Trim().CleanText();
                            if (!string.IsNullOrEmpty(dateText))
                            {
                                // 시간 형식 (HH:mm)
                                if (Regex.IsMatch(dateText, @"^\d{2}:\d{2}$"))
                                {
                                    var dateTime = DateTime.ParseExact(dateText, "HH:mm", CultureInfo.InvariantCulture);
                                    TimeSpan difference = dateTime - DateTime.Now;
                                    if (difference.TotalSeconds > 0) // 미래
                                        dateTime.AddDays(-1);

                                    post.Date = $"{dateTime:yyyy-MM-dd} {dateText}";

                                    //post.Date = $"{DateTime.Now:yyyy-MM-dd} {dateText}";
                                    break;
                                }
                                // 날짜 형식 (yy.MM.dd)
                                else if (Regex.IsMatch(dateText, @"^\d{2}\.\d{2}\.\d{2}$"))
                                {
                                    try
                                    {
                                        var dateTime = DateTime.ParseExact(dateText, "yy.MM.dd", CultureInfo.InvariantCulture);
                                        post.Date = dateTime.ToString("yyyy-MM-dd");
                                        break;
                                    }
                                    catch { }
                                }
                                // 기타 날짜 형식
                                else if (Regex.IsMatch(dateText, @"\d{4}-\d{2}-\d{2}"))
                                {
                                    post.Date = dateText;
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(post.Title) && !string.IsNullOrEmpty(post.Number))
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

                Console.WriteLine($"Ruliweb 상세 HTML 파일 파싱 중...");

                // 파일에서 HTML 읽기
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var contentDiv = doc.DocumentNode.SelectSingleNode("//div[@class='view_content autolink']//article//div");

                // p 또는 div 태그들 순회
                var elements = contentDiv.SelectNodes("./p | ./div");

                var result = await CreateOptimizedHtml(elements, orgNo);
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
                Console.WriteLine($"Ruliweb 상세 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }


    }
}