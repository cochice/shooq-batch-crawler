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
    public class _82CookCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  82Cook 크롤링 시작: {Site._82Cook.url}");

            var options = GetChromeOptions();
            using var driver = new ChromeDriver(options);

            // 타임아웃 설정
            SetupDriverTimeouts(driver);

            try
            {
                driver.Navigate().GoToUrl(Site._82Cook.url);

                // webdriver 속성 숨기기
                ((IJavaScriptExecutor)driver).ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

                // 페이지 로딩 대기
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                wait.Until(d => d.FindElement(By.TagName("body")));

                var html = driver.PageSource;
                Console.WriteLine($"HTML 길이: {html.Length}");

                htmlFileName = $"{Define.HtmlPath}temp_{Site._82Cook.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                await File.WriteAllTextAsync(htmlFileName, html, Encoding.UTF8);
                Console.WriteLine($"임시 파일 생성: {htmlFileName}");

                // 파일에서 파싱
                if (htmlFileName != null) posts = await ParseHtmlFile(htmlFileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"82Cook 크롤링 오류: {ex.Message}");
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

                // 82Cook 게시글 테이블 파싱 - 일반적인 게시판 테이블 구조
                var rows = doc.DocumentNode.SelectNodes("//div[@id='bbs']/table//tr");

                if (rows == null || rows.Count == 0)
                {
                    Console.WriteLine("게시글을 찾을 수 없습니다.");
                    return posts;
                }

                foreach (var row in rows)
                {
                    try
                    {
                        var rowNode = row.SelectSingleNode(".//td/i[@class='icon-bell']");
                        if (rowNode != null) continue;

                        var tds = row.SelectNodes("td");
                        if (tds == null || tds.Count < 4) continue;

                        var post = new PostInfo();

                        // 제목, URL, 댓글
                        var titleTd = row.SelectSingleNode(".//td[@class='title']");
                        var titleLink = titleTd.SelectSingleNode(".//a");
                        if (titleLink != null)
                        {
                            // 제목
                            post.Title = titleLink.InnerText.CleanText();

                            // URL
                            var href = titleLink.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                            {
                                href = href.Replace("amp;", "");
                                post.Url = href.StartsWith("http") ? href : $"https://www.82cook.com/entiz/{href}";
                            }

                            // 댓글
                            var replyNode = titleTd.SelectSingleNode("./em");
                            if (replyNode != null)
                            {
                                post.ReplyNum = Regex.Replace(replyNode.InnerText.CleanText(), @"[^\d]", "");
                            }
                        }
                        else
                        {
                            post.Title = titleTd.InnerText.CleanText();
                        }

                        // 작성자 (세 번째 td)
                        post.Author = tds[2].InnerText.Trim().CleanText() ?? "";

                        // 날짜 (네 번째 td)
                        post.Date = tds[3].GetAttributeValue("title", "");

                        // 조회수 (다섯 번째 td가 있는 경우)
                        var viewsNode = tds[4].InnerText.Trim().CleanText() ?? "";
                        if (viewsNode != null)
                        {
                            post.Views = Regex.Replace(viewsNode, @"[^\d]", "");
                        }

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
    }
}