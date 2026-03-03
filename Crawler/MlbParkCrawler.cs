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
    public class MlbParkCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;
            string? url = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[0];
            string? no = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[1];
            string setUrlVal = string.IsNullOrEmpty(url) ? Site.MlbPark.url : url;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MLB Park{(string.IsNullOrEmpty(url) ? "" : " 상세")} 크롤링 시작: {setUrlVal}");

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

                htmlFileName = $"{Define.HtmlPath}temp_{Site.MlbPark.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
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
                Console.WriteLine($"MLB Park 크롤링 오류: {ex.Message}");
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

                // MLB Park 게시글 테이블 파싱 - 일반적인 게시판 테이블 구조
                var rows = doc.DocumentNode.SelectNodes("//table[@class='tbl_type01']//tr");

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
                        if (tds[0].InnerText.Trim().CleanText().Contains("번호") && tds[1].InnerText.Trim().CleanText().Contains("제목"))
                            continue;
                        if (tds[0].InnerText.Trim().CleanText().Contains("공지"))
                            continue;

                        // 글번호
                        post.Number = Regex.Replace(tds[0].InnerText.Trim().CleanText(), @"[^\d]", "");

                        // 제목, URL, 댓글
                        var titleNode = tds[1].SelectSingleNode("./div[ @class='tit']");
                        if (titleNode != null)
                        {
                            var linkNode = titleNode.SelectSingleNode("./a[@class='txt']");
                            if (linkNode != null)
                            {
                                // title
                                post.Title = linkNode.InnerText.Trim().CleanText();

                                // url
                                var href = linkNode.GetAttributeValue("href", "").Trim().Replace("amp;", "");
                                post.Url = post.Url = href.StartsWith("http") ? href : $"https://mlbpark.donga.com{href}";
                            }

                            // reply
                            var replyCount = titleNode.SelectSingleNode(".//span[@class='replycnt']")?.InnerText?.Trim();
                            if (replyCount != null)
                            {
                                post.ReplyNum = Regex.Replace(replyCount, @"[^\d]", "");
                            }
                        }

                        // author
                        post.Author = tds[2].SelectSingleNode(".//span[@class='nick']")?.InnerText?.Trim()?.CleanText();

                        // 날짜
                        var dateText = tds[3].SelectSingleNode(".//span[@class='date']").InnerText.Trim().CleanText() ?? "";
                        if (!string.IsNullOrEmpty(dateText))
                        {
                            // 22:26:01 :: 25/08/29
                            if (dateText.IndexOf(':') > -1)
                            {
                                var writeDate = DateTime.ParseExact($"{DateTime.Now:yyyy-MM-dd} {dateText}", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                                TimeSpan difference = writeDate - DateTime.Now;
                                if (difference.TotalSeconds > 0) // 미래
                                {
                                    writeDate.AddDays(-1);
                                }

                                post.Date = $"{writeDate:yyyy-MM-dd HH:mm:ss}";
                            }
                            else if (dateText.IndexOf('-') > -1)
                            {
                                var dateTime = DateTime.ParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                                TimeSpan difference = dateTime - DateTime.Now;
                                if (difference.TotalSeconds > 0) // 미래
                                {
                                    dateTime.AddDays(-1);
                                }

                                post.Date = $"{dateTime:yyyy-MM-dd}";
                            }
                        }

                        // views
                        var viewsText = tds[4].SelectSingleNode(".//span[@class='viewV']").InnerText.Trim().CleanText() ?? "";
                        post.Views = Regex.Replace(viewsText, @"[^\d]", "");

                        if (!string.IsNullOrEmpty(post.Title) && !string.IsNullOrEmpty(post.Number))
                        {
                            posts.Add(post);
                            post.PostPrint();
                        }
                    }
                    catch
                    {
                        continue; // 개별 행 파싱 오류는 무시하고 계속
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

                Console.WriteLine($"MLB Park 상세 HTML 파일 파싱 중...");

                // 파일에서 HTML 읽기
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // HtmlAgilityPack으로 파싱
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // var eyeIcon = doc.DocumentNode.SelectSingleNode(".//i[contains(@class, 'fa-eye')]");
                // var commentIcon = doc.DocumentNode.SelectSingleNode(".//i[contains(@class, 'fa-comment-dots')]");

                // string? viewCount = eyeIcon?.NextSibling?.InnerText.Trim().CleanText();
                // string? commentCount = commentIcon?.NextSibling?.InnerText.Trim().CleanText();

                // Console.WriteLine($"조회수: {viewCount}");
                // Console.WriteLine($"댓글수: {commentCount}");

                // // 상세 컨텐츠 에서 얻은 정보 업데이트
                // new DatabaseManager().EditDetailPostCount(new PostInfo() { Url = url, Views = viewCount, ReplyNum = commentCount });

                // List<string> imageSources = doc.DocumentNode
                //     .SelectNodes(".//div[@id='contentDetail']//img[@src]")?
                //     .Select(img => img.GetAttributeValue("src", ""))
                //     .Where(src => !string.IsNullOrEmpty(src))
                //     .ToList() ?? [];

                // if (imageSources != null && imageSources.Count() > 0)
                // {
                //     // var results = await new ImageKitUploader().UploadMultipleSequential(imageSources);
                //     var results = await new CloudflareR2Uploader().UploadMultipleSequential(imageSources);

                //     foreach (var result in results)
                //     {
                //         await new DatabaseManager().InsertOptimizedImagesAsync(
                //         new OptimizedImageData
                //         {
                //             CloudinaryUrl = result.UploadedUrl,
                //             CloudinaryPublicId = "shooq_pub",
                //             Title = string.Empty,
                //             UploadedAt = DateTime.UtcNow,
                //             IsActive = true,
                //             No = orgNo
                //         });
                //     }
                // }
                var contentDiv = doc.DocumentNode.SelectSingleNode("//div[@id='contentDetail']");
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
                Console.WriteLine($"MLB Park 상세 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }
    }
}