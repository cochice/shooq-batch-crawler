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
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Marvin.Tmthfh91.Crawling.Crawler
{
    public class BobaeDreamCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;
            string? url = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[0];
            string? no = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[1];
            string setUrlVal = string.IsNullOrEmpty(url) ? Site.BobaeDream.url : url;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] BobaeDream{(string.IsNullOrEmpty(url) ? "" : " 상세")} 크롤링 시작: {setUrlVal}");

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

                htmlFileName = $"{Define.HtmlPath}temp_{Site.BobaeDream.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
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
                Console.WriteLine($"Bobae Dream 크롤링 오류: {ex.Message}");
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

                // Bobae Dream 게시글 테이블 파싱 - 일반적인 게시판 테이블 구조
                var rows = doc.DocumentNode.SelectNodes("//table[@id='boardlist']//tbody//tr | //table//tr[td[contains(@class, 'board')]] | //table//tr[td[@class='tit']]");

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

                        // 헤더나 공지사항 건너뛰기
                        var firstCellText = tds[0].InnerText.Trim().CleanText() ?? "";
                        if (string.IsNullOrEmpty(firstCellText) || firstCellText.Contains("번호") ||
                            firstCellText.Contains("공지") || firstCellText.Contains("No."))
                        {
                            continue;
                        }

                        // Title, URL, Reply
                        var titleTd = row.SelectSingleNode(".//td[@class='pl14']");
                        if (titleTd != null)
                        {
                            // Title
                            var titleNode = titleTd.SelectSingleNode("./a[@class='bsubject']");
                            post.Title = titleNode.InnerText?.Trim().CleanText();

                            // URL
                            var urlText = titleNode.GetAttributeValue("href", "");
                            if (urlText != null && !string.IsNullOrEmpty(urlText))
                            {
                                urlText = urlText.Replace("amp;", "");
                                post.Url = urlText.StartsWith("http") ? urlText : $"https://bobaedream.co.kr{urlText}";
                            }

                            // reply
                            var replyCount = titleNode.SelectSingleNode(".//strong[@class='totreply']")?.InnerText?.Trim();
                            if (replyCount != null)
                            {
                                post.ReplyNum = Regex.Replace(replyCount, @"[^\d]", "");
                            }
                        }

                        // author
                        var authorTd = row.SelectSingleNode(".//td[@class='author02']");
                        if (authorTd != null)
                        {
                            post.Author = authorTd.SelectSingleNode(".//span[@class='author']").InnerText?.Trim()?.CleanText();
                        }

                        // date
                        var dateText = row.SelectSingleNode(".//td[@class='date']").InnerText?.Trim().CleanText();
                        if (dateText != null && !string.IsNullOrEmpty(dateText))
                        {
                            if (dateText.IndexOf(':') > -1)
                            {
                                var dateDt = DateTime.ParseExact(dateText, "HH:mm", DateTimeFormatInfo.InvariantInfo);
                                post.Date = $"{dateDt:yyyy-MM-dd HH:mm}";
                            }
                            else if (dateText.IndexOf('/') > -1)
                            {
                                var dateDt = DateTime.ParseExact(dateText, "MM:dd", DateTimeFormatInfo.InvariantInfo);
                                post.Date = $"{dateDt:yyyy-MM-dd HH:mm}";
                            }
                            else
                            {
                                post.Date = $"{DateTime.Now:yyyy-MM-dd HH:mm}";
                            }
                        }

                        // likes
                        var likesText = row.SelectSingleNode(".//td[@class='recomm']").InnerText?.Trim().CleanText() ?? "";
                        if (likesText != null && !string.IsNullOrEmpty(likesText))
                        {
                            post.Likes = Regex.Replace(likesText, @"[^\d]", "");
                        }

                        // views
                        var viewsText = row.SelectSingleNode(".//td[@class='count']").InnerText?.Trim().CleanText() ?? "";
                        if (viewsText != null && !string.IsNullOrEmpty(viewsText))
                        {
                            post.Views = Regex.Replace(viewsText, @"[^\d]", "");
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

        public async Task<List<PostInfo>?> ParseHtmlFileForDetail(string filePath, string url, string no)
        {
            var posts = new List<PostInfo>();

            try
            {
                int.TryParse(no, out int orgNo);

                Console.WriteLine($"BobaeDream 상세 HTML 파일 파싱 중...");

                // 파일에서 HTML 읽기
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // HtmlAgilityPack으로 파싱
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // ✅ writerProfile 블록 찾기
                var profileDiv = doc.DocumentNode.SelectSingleNode("//div[@class='writerProfile']");
                if (profileDiv != null)
                {
                    // profileDiv.InnerHtml 또는 InnerText를 사용해서 내부만 재파싱 가능
                    var innerHtml = profileDiv.InnerHtml;
                    var subDoc = new HtmlDocument();
                    subDoc.LoadHtml(innerHtml);

                    // =========================
                    // 댓글수 추출
                    // =========================
                    var commentNode = subDoc.DocumentNode.SelectSingleNode("//em[@class='detailTxtDeco01']");
                    int commentCount = 0;
                    if (commentNode != null)
                    {
                        string commentText = commentNode.InnerText.Trim();
                        commentText = commentText.Replace("[", "").Replace("]", "");
                        int.TryParse(commentText, out commentCount);
                    }

                    // =========================
                    // 조회수, 추천수 추출
                    // =========================
                    var spanNode = subDoc.DocumentNode.SelectSingleNode("//span[@class='countGroup']");
                    int viewCount = 0;
                    int likeCount = 0;

                    if (spanNode != null)
                    {
                        var emNodes = spanNode.SelectNodes(".//em[@class='txtType']");
                        if (emNodes != null && emNodes.Count >= 2)
                        {
                            string viewText = emNodes[0].InnerText.Replace(",", "").Trim();
                            int.TryParse(viewText, out viewCount);

                            string likeText = emNodes[1].InnerText.Replace(",", "").Trim();
                            int.TryParse(likeText, out likeCount);
                        }
                    }

                    // Console.WriteLine($"조회수: {viewCount}");
                    // Console.WriteLine($"추천수: {likeCount}");
                    // Console.WriteLine($"댓글수: {commentCount}");

                    // 상세 컨텐츠 에서 얻은 정보 업데이트
                    await new DatabaseManager().EditDetailPostCount(new PostInfo() { Url = url, Views = $"{viewCount}", ReplyNum = $"{commentCount}", Likes = $"{likeCount}" });
                }

                var contentDiv = doc.DocumentNode.SelectSingleNode(".//div[@class='bodyCont' and @itemprop='articleBody']");

                // p 또는 div 태그들 순회
                var elements = contentDiv.SelectNodes("./p | ./div");

                var cleanContent = new StringBuilder();
                var result = await CreateOptimizedHtml(elements, orgNo);
                if (!string.IsNullOrWhiteSpace(result.Item1))
                {
                    cleanContent.AppendLine("<article class='shooq-content'>");
                    cleanContent.Append(result.Item1);
                    cleanContent.AppendLine("</article>");
                    await new DatabaseManager().EditDetailPostCount(new PostInfo() { Url = url, Content = cleanContent.ToString(), img2 = result.Item2 });
                }

                // List<string> imageSources = doc.DocumentNode
                //     .SelectNodes(".//div[@class='bodyCont' and @itemprop='articleBody']//img[@src]")?
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BobaeDream 상세 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }
    }
}