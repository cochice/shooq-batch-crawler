using HtmlAgilityPack;
using Marvin.Tmthfh91.Crawling.Crawler;
using Marvin.Tmthfh91.Crawling.Model;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Marvin.Tmthfh91.Crawling.Crawler
{
    public class ClienCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;
            string? url = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[0];
            string? no = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[1];
            string setUrlVal = string.IsNullOrEmpty(url) ? Site.Clien.url : url;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 클리앙{(string.IsNullOrEmpty(url) ? "" : " 상세")} 크롤링 시작: {setUrlVal}");

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

                htmlFileName = $"{Define.HtmlPath}temp_{Site.Clien.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
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
                Console.WriteLine($"클리앙 크롤링 오류: {ex.Message}");
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
                Console.WriteLine($"클리앙 HTML 파일 파싱 중...");

                // 파일에서 HTML 읽기
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // HtmlAgilityPack으로 파싱
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // 클리앙 추천 게시글 선택
                var rows = doc.DocumentNode.SelectNodes("//div[contains(@class,'recommend_underList')]//div[contains(@class,'list_item')]");

                if (rows == null || rows.Count == 0)
                {
                    Console.WriteLine("게시글을 찾을 수 없습니다.");
                    return posts;
                }

                foreach (var row in rows)
                {
                    try
                    {
                        var post = new PostInfo();

                        // 제목과 URL 추출
                        var linkNode = row.SelectSingleNode(".//span[@class='subject_fixed']//a") ??
                                      row.SelectSingleNode(".//a[contains(@href,'/service/board')]");

                        if (linkNode != null)
                        {
                            var href = linkNode.GetAttributeValue("href", "");
                            if (!href.StartsWith("http"))
                            {
                                post.Url = $"https://www.clien.net{href}";
                            }
                            else
                            {
                                post.Url = href;
                            }

                            var title = linkNode.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(title))
                            {
                                post.Title = title.CleanText().RemovePrefixAndTrim("모공");
                            }
                        }

                        // 작성자 추출
                        var authorNode = row.SelectSingleNode(".//div[@class='list_author']") ??
                                        row.SelectSingleNode(".//span[@class='nickname']");
                        if (authorNode != null)
                        {
                            post.Author = authorNode.InnerText?.Trim().CleanText();
                        }

                        // 날짜 추출
                        var dateNode = row.SelectSingleNode(".//span[contains(@class,'timestamp')]");
                        if (dateNode != null)
                        {
                            var dateText = dateNode.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(dateText))
                            {
                                post.Date = dateText;
                            }
                        }

                        // 조회수 추출
                        var viewsNode = row.SelectSingleNode(".//div[@class='list_hit']/span[@class='hit']");
                        if (viewsNode != null)
                        {
                            var viewsText = viewsNode.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(viewsText))
                            {
                                // "조회 1.2k" 형식에서 숫자만 추출
                                var viewsMatch = Regex.Match(viewsText, @"([\d.]+)([k|K]?)");
                                if (viewsMatch.Success)
                                {
                                    var number = viewsMatch.Groups[1].Value;
                                    var suffix = viewsMatch.Groups[2].Value.ToLower();

                                    if (suffix == "k")
                                    {
                                        if (double.TryParse(number, out double kValue))
                                        {
                                            post.Views = ((int)(kValue * 1000)).ToString();
                                        }
                                    }
                                    else
                                    {
                                        post.Views = Regex.Replace(number, @"[^\d]", "");
                                    }
                                }
                            }
                        }

                        // 추천수 추출
                        var likesNode = row.SelectSingleNode(".//div[@data-role='list-like-count']/span");
                        if (likesNode != null)
                        {
                            var likesText = likesNode.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(likesText))
                            {
                                post.Likes = Regex.Replace(likesText, @"[^\d]", "");
                            }
                        }

                        // 댓글수 추출
                        var comment_count = row.GetAttributeValue("data-comment-count", "0");
                        if (comment_count != null && !string.IsNullOrEmpty(comment_count)) post.ReplyNum = comment_count.Trim().CleanText();

                        // 게시글 번호 추출 (URL에서)
                        if (!string.IsNullOrEmpty(post.Url))
                        {
                            var numberMatch = Regex.Match(post.Url, @"wr_id=(\d+)");
                            if (numberMatch.Success)
                            {
                                post.Number = numberMatch.Groups[1].Value;
                            }
                        }

                        if (!string.IsNullOrEmpty(post.Title))
                        {
                            posts.Add(post);
                            post.PostPrint();
                        }
                    }
                    catch
                    {
                        continue; // 개별 게시글 파싱 오류는 무시하고 계속
                    }
                }

                Console.WriteLine($"총 {posts.Count}개의 클리앙 게시글을 파싱했습니다.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"클리앙 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }

        public async Task<List<PostInfo>?> ParseHtmlFileForDetail(string filePath, string url, string no)
        {
            var posts = new List<PostInfo>();

            try
            {
                int.TryParse(no, out int orgNo);

                Console.WriteLine($"클리앙 상세 HTML 파일 파싱 중...");

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
                //     .SelectNodes(".//div[@class='post_article']//img[@src]")?
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

                var contentDiv = doc.DocumentNode.SelectSingleNode(".//div[@class='post_article']");

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
                Console.WriteLine($"클리앙 상세 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }
    }
}