using HtmlAgilityPack;
using Marvin.Tmthfh91.Crawling.Crawler;
using Marvin.Tmthfh91.Crawling.Model;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Marvin.Tmthfh91.Crawling.Crawler
{
    public class FMKoreaCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;
            string? url = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[0];
            string? no = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[1];
            string setUrlVal = string.IsNullOrEmpty(url) ? Site.FMKorea.url : url;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FM코리아{(string.IsNullOrEmpty(url) ? "" : " 상세")} 크롤링 시작: {setUrlVal}");

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

                htmlFileName = $"{Define.HtmlPath}temp_{Site.FMKorea.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
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
                Console.WriteLine($"FM코리아 크롤링 오류: {ex.Message}");
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
                Console.WriteLine($"FM코리아 HTML 파일 파싱 중...");

                // 파일에서 HTML 읽기
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // HtmlAgilityPack으로 파싱
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var rows = doc.DocumentNode.SelectNodes("//div[@class='fm_best_widget _bd_pc']//ul//li");

                foreach (var row in rows)
                {
                    try
                    {
                        var post = new PostInfo();

                        if (rows == null || rows.Count == 0)
                        {
                            Console.WriteLine("게시글을 찾을 수 없습니다.");
                            return posts;
                        }

                        var linkNode = row.SelectSingleNode(".//h3[@class='title']/a");
                        if (linkNode != null)
                        {
                            var href = linkNode.GetAttributeValue("href", "");
                            post.Url = $"https://www.fmkorea.com/{href.Replace("amp;", "")}";

                            var title = linkNode.InnerText;

                            var commentSpan = linkNode.SelectSingleNode(".//span[@class='comment_count']");
                            if (commentSpan != null)
                            {
                                var s_reply_num = commentSpan.InnerText.Trim().CleanText();
                                post.ReplyNum = Regex.Replace(s_reply_num, @"[^\d]", "");
                            }

                            // &nbsp; 제거 및 공백 정리
                            title = title.Replace("&nbsp;", "").Trim();
                            title = Regex.Replace(title, @"\s*\[\d+\]\s*$", "").Trim();
                            post.Title = title.CleanText();

                            var date = row.SelectSingleNode(".//span[@class='regdate']");
                            if (date != null)
                            {
                                var strDate = date.InnerText.Trim().CleanText();
                                bool isDateFormat = Regex.IsMatch(strDate, @"^\d{4}\.\d{2}\.\d{2}$");
                                if (isDateFormat)
                                    post.Date = strDate.Replace(".", "-");
                                else
                                {
                                    string t_m_numbers = Regex.Replace(strDate, @"[^\d]", "");
                                    if (strDate.Contains("시간"))
                                    {
                                        var hoursToSubtract = -(int.TryParse(t_m_numbers, out int value) ? value : 0);
                                        DateTime calDt = DateTime.Now.AddHours(hoursToSubtract);
                                        post.Date = $"{DateTime.Now:yyyy-MM-dd} {calDt:HH:mm}";
                                    }
                                    else if (strDate.Contains('분'))
                                    {
                                        var minutesToSubtract = -(int.TryParse(t_m_numbers, out int value) ? value : 0);
                                        DateTime calDt = DateTime.Now.AddHours(minutesToSubtract);
                                        post.Date = $"{DateTime.Now:yyyy-MM-dd} {calDt:HH:mm}";
                                    }
                                    else
                                    {
                                        post.Date = $"{DateTime.Now:yyyy-MM-dd}";
                                    }
                                }
                            }

                            var author = row.SelectSingleNode(".//span[@class='author']");
                            if (author != null) post.Author = author.InnerText.Trim().CleanText().Replace("/", "");

                            var likes = row.SelectSingleNode(".//span[@class='count']");
                            if (likes != null)
                            {
                                var s_n_likes = Regex.Replace(likes.InnerText.Trim().CleanText().Replace("/", ""), @"[^\d]", "");
                                post.Likes = s_n_likes;
                            }
                        }

                        // 썸네일 주석 처리
                        // if (await new DatabaseManager().GetExistsLink(post) == false)
                        // {
                        //     var img1Node = row.SelectSingleNode(".//img[@class='thumb entered loaded']");
                        //     if (img1Node != null)
                        //     {
                        //         var src = img1Node.GetAttributeValue("src", "");
                        //         if (src != null && !string.IsNullOrEmpty(src))
                        //         {
                        //             //src = src.StartsWith("//") ? src[2..] : src;
                        //             var img_id = await new List<CrawledImage> { new() { ImageUrl = src, Title = post.Title, SourceSite = "fmkorea.com" } }.UploadImageAndReturnId();
                        //             if (img_id != null)
                        //             {
                        //                 post.img1 = img_id;
                        //             }
                        //         }
                        //     }
                        // }

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

                Console.WriteLine($"총 {posts.Count}개의 FM코리아 게시글을 파싱했습니다.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FM코리아 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }

        public async Task<List<PostInfo>?> ParseHtmlFileForDetail(string filePath, string url, string no)
        {
            var posts = new List<PostInfo>();

            try
            {
                int.TryParse(no, out int orgNo);

                Console.WriteLine($"FM코리아 상세 HTML 파일 파싱 중...");

                // 파일에서 HTML 읽기
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // HtmlAgilityPack으로 파싱
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // ✅ 조회/추천/댓글 영역 찾기
                var spans = doc.DocumentNode.SelectNodes("//div[@class='btm_area clear']//div[contains(@class,'side fr')]//span");

                int views = 0, likes = 0, comments = 0;

                if (spans != null)
                {
                    foreach (var span in spans)
                    {
                        string text = span.InnerText.Trim();

                        // 조회 수
                        if (text.Contains("조회"))
                            int.TryParse(span.SelectSingleNode(".//b")?.InnerText.Replace(",", ""), out views);

                        // 추천 수
                        else if (text.Contains("추천"))
                            int.TryParse(span.SelectSingleNode(".//b")?.InnerText.Replace(",", ""), out likes);

                        // 댓글 수
                        else if (text.Contains("댓글"))
                            int.TryParse(span.SelectSingleNode(".//b")?.InnerText.Replace(",", ""), out comments);
                    }
                }

                // Console.WriteLine($"조회수: {views}");
                // Console.WriteLine($"추천수: {likes}");
                // Console.WriteLine($"댓글수: {comments}");

                var contentDiv = doc.DocumentNode.SelectSingleNode("//div[@class='rd_body clear']//article//div");

                // p 또는 div 태그들 순회
                var elements = contentDiv.SelectNodes("./p | ./div");

                var cleanContent = new StringBuilder();
                var result = await CreateOptimizedHtmlForFM(elements, orgNo);
                if (!string.IsNullOrWhiteSpace(result.Item1))
                {
                    cleanContent.AppendLine("<article class='shooq-content'>");
                    cleanContent.Append(result.Item1);
                    cleanContent.AppendLine("</article>");
                }

                // 상세 컨텐츠 에서 얻은 정보 및 슉라이브 컨텐츠 업데이트
                if (!string.IsNullOrWhiteSpace(cleanContent.ToString()))
                {
                    await new DatabaseManager().EditDetailPostCount(new PostInfo() { Url = url, Content = cleanContent.ToString(), img2 = result.Item2, Views = $"{views}", ReplyNum = $"{comments}", Likes = $"{likes}" });
                }
                else
                {
                    await new DatabaseManager().EditDetailPostCount(new PostInfo() { Url = url, Views = $"{views}", ReplyNum = $"{comments}", Likes = $"{likes}" });
                }

                // List<string> imageSources = doc.DocumentNode
                //     .SelectNodes(".//article//img[@src]")?
                //     .Select(img =>
                //     {
                //         var src = img.GetAttributeValue("src", "");
                //         if (string.IsNullOrEmpty(src)) return "";

                //         // https:가 없으면 추가
                //         if (src.StartsWith("//"))
                //             src = "https:" + src;
                //         else if (!src.StartsWith("http"))
                //             src = "https://" + src;

                //         return src;
                //     })
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
                Console.WriteLine($"FM코리아 상세 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }
    }
}