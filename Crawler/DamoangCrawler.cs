using HtmlAgilityPack;
using Marvin.Tmthfh91.Crawling.Crawler;
using Marvin.Tmthfh91.Crawling.Model;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Marvin.Tmthfh91.Crawling.Crawler
{
    public class DamoangCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;
            string? url = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[0];
            string? no = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[1];
            string setUrlVal = string.IsNullOrEmpty(url) ? Site.Damoang.url : url;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Damoang{(string.IsNullOrEmpty(url) ? "" : " 상세")} 크롤링 시작: {setUrlVal}");

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

                htmlFileName = $"{Define.HtmlPath}temp_{Site.Damoang.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
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
                Console.WriteLine($"Damoang 크롤링 오류: {ex.Message}");
            }
            finally
            {
                //HTML 파일 삭제 (성공적으로 파싱된 경우만)
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

                // Damoang 게시글 테이블 파싱
                var rows = doc.DocumentNode.SelectNodes("//section[@id='bo_list']//ul[@class='list-group list-group-flush border-bottom']//li");

                if (rows == null || rows.Count == 0)
                {
                    Console.WriteLine("게시글을 찾을 수 없습니다.");
                    return posts;
                }

                foreach (var row in rows)
                {
                    var post = new PostInfo();

                    try
                    {
                        var classAttr = row.GetAttributeValue("class", "");
                        if (classAttr != null && classAttr.Contains("list-group-item da-link-block bg-light-subtle da-atricle-row--notice")) continue;

                        if (classAttr != null && classAttr.Contains("list-group-item da-link-block"))
                        {
                            var ad_el = row.SelectSingleNode(".//div[@class='rcmd-box step-pai']/span");
                            if (ad_el != null)
                            {
                                var ad_str = ad_el.InnerText.Trim().CleanText();
                                if (ad_str.Length > 0 && ad_str.Contains("홍보")) continue; // 광고글
                            }

                            // Likes -------------------------------------------------------------------------------
                            var likes_el = row.SelectSingleNode(".//div[contains(@class, 'rcmd-box')]");
                            if (likes_el != null)
                            {
                                var likes_str = likes_el.InnerText.Trim().CleanText();
                                var intValue = likes_str.ConvertToIntFromK();
                                post.Likes = intValue == null ? null : $"{intValue}";
                            }

                            // Title, Url  -------------------------------------------------------------------------------
                            var title_el = row.SelectSingleNode(".//a[@class='da-link-block da-article-link subject-ellipsis']");
                            if (title_el != null)
                            {
                                post.Title = title_el.InnerText.Trim().CleanText();

                                var href = title_el.GetAttributeValue("href", "");
                                if (!string.IsNullOrEmpty(href))
                                {
                                    post.Url = href.Replace("amp;", "");
                                }
                            }

                            // ReplyNumber  -------------------------------------------------------------------------------
                            var reply_num_el = row.SelectSingleNode(".//span[@class='count-plus orangered']");
                            if (reply_num_el != null)
                            {
                                post.ReplyNum = reply_num_el.InnerText.Trim().CleanText();
                            }

                            // Author  -------------------------------------------------------------------------------
                            var author_el = row.SelectSingleNode(".//span[@class='sv_name text-truncate']");
                            if (author_el != null)
                            {
                                post.Author = author_el.InnerText.Trim().CleanText();
                            }

                            if (post.Author != null && !string.IsNullOrEmpty(post.Author) && post.Author.Equals("SDK")) continue; // 공지글

                            // Date  -------------------------------------------------------------------------------
                            var date_el = row.SelectSingleNode(".//div[@class='wr-date text-nowrap order-5 order-md-2'] | .//span[@class='da-list-date'] | .//span[@class='orangered da-list-date']");
                            if (date_el != null)
                            {
                                var date_str = date_el.InnerText.Trim().CleanText();
                                var (success, result) = date_str.ConvertToStandardDateTime();
                                post.Date = success ? result : null;
                            }

                            // Views --------------------------------------------------------------------------------
                            var views_el = row.SelectSingleNode(".//div[@class='wr-num text-nowrap order-4']");
                            if (views_el != null)
                            {
                                //ar views_str = views_el.InnerText.Trim().CleanText();
                                var intValue = views_el.ExtractViewCount();
                                post.Views = intValue == null ? null : $"{intValue}";
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

                Console.WriteLine($"Damoang 상세 HTML 파일 파싱 중...");

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
                //     .SelectNodes(".//div[@class='economy-user-text na-convert']//img[@src]")?
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

                var contentDiv = doc.DocumentNode.SelectSingleNode("//div[@id='bo_v_con']//div[@class='economy-user-text na-convert']");
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
                Console.WriteLine($"Damoang 상세 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }
    }
}