using HtmlAgilityPack;
using Marvin.Tmthfh91.Crawling.Model;
using Marvin.Tmthfh91.Crawling.Crawler;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace Marvin.Tmthfh91.Crawling.Crawler
{
    public class TheQooCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;
            string? url = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[0];
            string? no = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[1];
            string setUrlVal = string.IsNullOrEmpty(url) ? Site.TheQoo.url : url;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 더쿠{(string.IsNullOrEmpty(url) ? "" : " 상세")} 크롤링 시작: {setUrlVal}");

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

                htmlFileName = $"{Define.HtmlPath}temp_{Site.TheQoo.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
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
                Console.WriteLine($"더쿠 크롤링 오류: {ex.Message}");
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
                Console.WriteLine($"더쿠 HTML 파일 파싱 중...");

                // 파일에서 HTML 읽기
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // HtmlAgilityPack으로 파싱
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var rows = doc.DocumentNode.SelectNodes("//table[@class='bd_lst bd_tb_lst bd_tb theqoo_board_table']//tr");

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

                        // 헤더 행 건너뛰기
                        if (row.SelectNodes("th") != null) continue;

                        // 공지사항 건너뛰기
                        var noticeClass = row.GetAttributeValue("class", "");
                        if (noticeClass.Contains("notice")) continue;

                        var tds = row.SelectNodes("td");
                        var td2 = tds[2];
                        if (td2 != null)
                        {
                            var a_el = td2.SelectNodes(".//a");
                            if (a_el != null)
                            {
                                var href = a_el[0].GetAttributeValue("href", "");
                                if (!string.IsNullOrEmpty(href))
                                {
                                    post.Url = $"https://theqoo.net{href}";
                                }
                                var title = a_el[0].InnerText.CleanText();
                                if (!string.IsNullOrEmpty(title))
                                {
                                    post.Title = title;
                                }
                            }
                        }
                        var td3 = tds[3];
                        if (td3 != null)
                        {
                            var time = td3.InnerText.CleanText();
                            if (time.Contains('.'))
                            {
                                post.Date = $"{DateTime.Now:yyyy}-{time.Replace(".", "-")}";
                            }
                            else if (time.Contains(':'))
                            {
                                post.Date = $"{DateTime.Now:yyyy-MM-dd} {time}";
                            }
                            else
                            {
                                post.Date = $"{DateTime.Now:yyyy-MM-dd HH:mm}";
                            }
                        }
                        var views = tds[4];
                        if (views != null)
                        {
                            post.Views = views.InnerText.CleanText();
                        }

                        //var repley = tds.Select

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

                Console.WriteLine($"총 {posts.Count}개의 더쿠 게시글을 파싱했습니다.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"더쿠 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }

        public async Task<List<PostInfo>?> ParseHtmlFileForDetail(string filePath, string url, string no)
        {
            var posts = new List<PostInfo>();

            try
            {
                int.TryParse(no, out int orgNo);

                Console.WriteLine($"더쿠 상세 HTML 파일 파싱 중...");

                // 파일에서 HTML 읽기
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // HtmlAgilityPack으로 파싱
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var eyeIcon = doc.DocumentNode.SelectSingleNode(".//i[contains(@class, 'fa-eye')]");
                var commentIcon = doc.DocumentNode.SelectSingleNode(".//i[contains(@class, 'fa-comment-dots')]");

                string? viewCount = eyeIcon?.NextSibling?.InnerText.Trim().CleanText();
                string? commentCount = commentIcon?.NextSibling?.InnerText.Trim().CleanText();

                Console.WriteLine($"조회수: {viewCount}");
                Console.WriteLine($"댓글수: {commentCount}");

                var contentDiv = doc.DocumentNode.SelectSingleNode("//div[@class='rd_body clear']//article//div");

                // p 또는 div 태그들 순회
                //var elements = contentDiv.SelectNodes("./p | ./div");

                var cleanContent = new StringBuilder();
                var result = await CreateOptimizedHtmlForTheqoo(contentDiv, orgNo);
                if (!string.IsNullOrWhiteSpace(result.Item1))
                {
                    cleanContent.AppendLine("<article class='shooq-content'>");
                    cleanContent.Append(result.Item1);
                    cleanContent.AppendLine("</article>");
                }

                // 상세 컨텐츠 에서 얻은 정보 및 슉라이브 컨텐츠 업데이트
                if (!string.IsNullOrWhiteSpace(cleanContent.ToString()))
                {
                    await new DatabaseManager().EditDetailPostCount(new PostInfo() { Url = url, Content = cleanContent.ToString(), img2 = result.Item2, Views = viewCount, ReplyNum = commentCount });
                }
                else
                {
                    await new DatabaseManager().EditDetailPostCount(new PostInfo() { Url = url, Views = viewCount, ReplyNum = commentCount });
                }

                // itemprop="articleBody"인 article만 선택
                // List<string> imageSources = doc.DocumentNode
                //     .SelectNodes(".//article[@itemprop='articleBody']//img[@src]")?
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
                Console.WriteLine($"더쿠 상세 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }
    }
}