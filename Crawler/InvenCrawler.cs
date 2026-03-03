using HtmlAgilityPack;
using Marvin.Tmthfh91.Crawling.Crawler;
using Marvin.Tmthfh91.Crawling.Model;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Marvin.Tmthfh91.Crawling.Crawler
{
    public class InvenCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;
            string? url = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[0];
            string? no = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[1];
            string setUrlVal = string.IsNullOrEmpty(url) ? Site.Inven.url : url;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Inven{(string.IsNullOrEmpty(url) ? "" : " 상세")} 크롤링 시작: {setUrlVal}");

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

                htmlFileName = $"{Define.HtmlPath}temp_{Site.Inven.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
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
                Console.WriteLine($"Inven 크롤링 오류: {ex.Message}");
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

                // Inven 게시글 리스트 파싱 - 리스트 형태의 구조
                var listItems = doc.DocumentNode.SelectNodes("//div[@id='hotven-list']//div[@class='list-common con']");

                if (listItems == null || listItems.Count == 0)
                {
                    Console.WriteLine("게시글을 찾을 수 없습니다.");
                    return posts;
                }

                foreach (var listItem in listItems)
                {
                    try
                    {
                        var post = new PostInfo();

                        // 제목, URL, Reply 
                        var titleNode = listItem.SelectSingleNode(".//div[contains(@class, 'title')]");
                        if (titleNode != null)
                        {
                            var linkNode = titleNode.SelectSingleNode(".//a");

                            // Title
                            var titleHtmlNode = linkNode.SelectSingleNode(".//div[@class='name']");
                            post.Title = titleHtmlNode.GetDirectText()?.Trim()?.CleanText();

                            // URL
                            var href = linkNode.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                            {
                                href.Replace("amp;", "");
                                post.Url = href.StartsWith("http") ? href : $"https://www.inven.co.kr{href}";
                            }

                            // Reply
                            var replyText = linkNode.SelectSingleNode(".//div[@class='comment']").InnerText?.Trim().CleanText();
                            if (replyText != null && !string.IsNullOrEmpty(replyText))
                            {
                                post.ReplyNum = Regex.Replace(replyText, @"[^\d]", "");
                            }
                        }

                        // 댓글 수 추출
                        var replyNode = listItem.SelectSingleNode(".//span[contains(@class, 'comment') or contains(@class, 'reply')]");
                        if (replyNode != null)
                        {
                            post.ReplyNum = Regex.Replace(replyNode.InnerText.CleanText(), @"[^\d]", "");
                        }

                        // 작성자 추출
                        var authorNode = listItem.SelectSingleNode(".//div[contains(@class, 'writer')]");
                        if (authorNode != null)
                        {
                            post.Author = authorNode.InnerText.CleanText();
                        }

                        // 날짜 추출
                        var dateText = listItem.SelectSingleNode(".//div[contains(@class, 'date')]").InnerText?.Trim().CleanText();
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

                        // 조회수 추출
                        var viewsNode = listItem.SelectSingleNode(".//div[contains(@class, 'hits')]");
                        if (viewsNode != null)
                        {
                            post.Views = Regex.Replace(viewsNode.InnerText.CleanText(), @"[^\d,]", "").Replace(",", "");
                        }

                        // 추천수 추출
                        var likesNode = listItem.SelectSingleNode(".//div[contains(@class, 'reco')]");
                        if (likesNode != null)
                        {
                            post.Likes = Regex.Replace(likesNode.InnerText.CleanText(), @"[^\d,]", "").Replace(",", "");
                        }

                        if (!string.IsNullOrEmpty(post.Title))
                        {
                            posts.Add(post);
                            post.PostPrint();
                        }
                    }
                    catch
                    {
                        // 개별 리스트 아이템 파싱 오류는 무시하고 계속
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

                Console.WriteLine($"Inven 상세 HTML 파일 파싱 중...");

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
                //     .SelectNodes(".//div[@id='powerbbsContent']//img[@src]")?
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

                var contentDiv = doc.DocumentNode.SelectSingleNode(".//div[@id='powerbbsContent']");

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Inven 상세 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }
    }
}