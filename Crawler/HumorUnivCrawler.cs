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
    public class HumorUnivCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;
            string? url = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[0];
            string? no = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[1];
            string setUrlVal = string.IsNullOrEmpty(url) ? Site.Humoruniv.url : url;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 유머대학{(string.IsNullOrEmpty(url) ? "" : " 상세")} 크롤링 시작: {setUrlVal}");

            var options = GetChromeOptions();

            try
            {
                using var driver = new ChromeDriver(options);

                // 타임아웃 설정
                SetupDriverTimeouts(driver);

                driver.Navigate().GoToUrl(setUrlVal);

                // webdriver 속성 숨기기
                ((IJavaScriptExecutor)driver).ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

                // 페이지 로딩 대기
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                wait.Until(d => d.FindElement(By.TagName("body")));

                var html = driver.PageSource;
                Console.WriteLine($"HTML 길이: {html.Length}");

                // 임시 HTML 파일로 저장
                htmlFileName = $"{Define.HtmlPath}temp_{Site.Humoruniv.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
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
                Console.WriteLine($"크롤링 오류: {ex.Message}");
            }
            finally
            {
                // HTML 파일 삭제
                if (!string.IsNullOrEmpty(htmlFileName) && File.Exists(htmlFileName))
                {
                    try
                    {
                        // if (posts == null || posts.Count < 0)
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

                // 여러 패턴으로 시도
                var rows = doc.DocumentNode.SelectNodes("//table[@id='post_list']//tr[position()>1]") ??
                          doc.DocumentNode.SelectNodes("//table[@id='post_list']//tr[td]") ??
                          doc.DocumentNode.SelectNodes("//table[@id='post_list']//tr");

                if (rows == null || rows.Count == 0)
                {
                    Console.WriteLine("게시글을 찾을 수 없습니다.");

                    // 디버깅 정보
                    var table = doc.DocumentNode.SelectSingleNode("//table[@id='post_list']");
                    if (table == null)
                    {
                        Console.WriteLine("post_list 테이블을 찾을 수 없습니다.");
                    }
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

                        var title = tds[1].SelectSingleNode(".//span[starts-with(@id, 'title_')]");
                        if (title != null)
                        {
                            post.Title = title.InnerText.CleanText();
                        }

                        var titleLink = row.SelectSingleNode(".//a");
                        if (titleLink != null)
                        {
                            var href = titleLink.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                            {
                                post.Url = $"https://web.humoruniv.com/board/humor/{href.Replace("amp;", "")}";
                            }
                        }

                        var date = tds[3].SelectNodes(".//span[@class='w_date']");
                        var time = tds[3].SelectNodes(".//span[@class='w_time']");
                        if (date != null || time != null)
                        {
                            post.Date = $"{date?[0].InnerText.CleanText()} {time?[0].InnerText.CleanText()}";
                        }

                        // 조회수
                        post.Views = tds[4]?.InnerText.Trim().CleanText() ?? "";

                        // 추천
                        post.Likes = tds.Count > 5 ? tds[5]?.InnerText.Trim().CleanText() ?? "" : "";

                        // 썸네일 처리 주석
                        // if (await new DatabaseManager().GetExistsLink(post) == false)
                        // {
                        //     var img1Node = row.SelectSingleNode(".//img[@class='thumb']");
                        //     if (img1Node != null)
                        //     {
                        //         var src = img1Node.GetAttributeValue("src", "");
                        //         if (src != null && !string.IsNullOrEmpty(src))
                        //         {
                        //             //src = src.StartsWith("//") ? src[2..] : src;
                        //             var img_id = await new List<CrawledImage> { new() { ImageUrl = src, Title = post.Title, SourceSite = "humoruniv.com" } }.UploadImageAndReturnId();
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

                Console.WriteLine($"유머대학 상세 HTML 파일 파싱 중...");

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
                //     .SelectNodes(".//div[@class='body_editor']//img[@src]")?
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

                var tableContent = doc.DocumentNode.SelectNodes("//div[@id='cnts']//wrap_copy//table//tr//td");
                var bodyContent = doc.DocumentNode.SelectSingleNode("//div[@id='cnts']//wrap_copy//div[@id='wrap_body']");

                // 두 영역을 담을 임시 컨테이너 생성
                var contentDiv = doc.CreateElement("div");

                // table의 td 내용 추가
                if (tableContent != null)
                {
                    foreach (var td in tableContent)
                    {
                        foreach (var child in td.ChildNodes)
                        {
                            contentDiv.AppendChild(child.CloneNode(true));
                        }
                    }
                }

                // wrap_body 내용 추가
                if (bodyContent != null)
                {
                    foreach (var child in bodyContent.ChildNodes)
                    {
                        contentDiv.AppendChild(child.CloneNode(true));
                    }
                }

                // p 또는 div 태그들 순회
                var elements = contentDiv.SelectNodes("./p | ./div");

                var result = await CreateOptimizedHtmlForHumorUniv(elements, orgNo);
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
                Console.WriteLine($"유머대학 상세 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }
    }
}