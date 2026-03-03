using HtmlAgilityPack;
using Marvin.Tmthfh91.Crawling.Crawler;
using Marvin.Tmthfh91.Crawling.Model;
using Newtonsoft.Json;
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
    public class PpomppuCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;
            string? url = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[0];
            string? no = string.IsNullOrEmpty(urlAndNo) ? string.Empty : urlAndNo.Split(",")[1];
            string setUrlVal = string.IsNullOrEmpty(url) ? Site.Ppomppu.url : url;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 뽐뿌{(string.IsNullOrEmpty(url) ? "" : " 상세")} 크롤링 시작: {setUrlVal}");

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

                htmlFileName = $"{Define.HtmlPath}temp_{Site.Ppomppu.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
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
                Console.WriteLine($"뽐뿌 크롤링 오류: {ex.Message}");
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
                Console.WriteLine($"뽐뿌 HTML 파일 파싱 중...");

                // 파일에서 HTML 읽기
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // HtmlAgilityPack으로 파싱
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // 뽐뿌 핫딜 페이지의 구조에 맞게 셀렉터 수정
                var rows = doc.DocumentNode.SelectNodes("//table[@class='board_table']//tr[@class='baseList bbs_new1 ']");

                if (rows == null || rows.Count == 0)
                {
                    // 대체 셀렉터 시도
                    rows = doc.DocumentNode.SelectNodes("//tr[contains(@class,'list')]");
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
                        var post = new PostInfo();
                        var cells = row.SelectNodes(".//td");

                        // 제목과 URL 추출
                        var linkNode = row.SelectNodes(".//a[@class='baseList-title']")[1];
                        var adIcon = linkNode.SelectSingleNode(".//span[@id='ad-icon']");
                        if (adIcon != null) continue; //광고 게시글 감지됨 - 건너뛰기

                        if (linkNode != null)
                        {
                            var href = linkNode.GetAttributeValue("href", "");
                            if (!href.StartsWith("http"))
                            {
                                post.Url = $"https://ppomppu.co.kr{href.Replace("amp;", "")}";
                            }
                            else
                            {
                                post.Url = href.Replace("amp;", "");
                            }

                            var title = linkNode.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(title))
                            {
                                post.Title = title.CleanText();
                            }
                        }

                        // 작성자 추출
                        var authorNode = row.SelectSingleNode(".//div[@class='list_name']");
                        if (authorNode != null)
                        {
                            post.Author = authorNode.InnerText?.Trim().CleanText();
                        }

                        // 날짜 추출
                        var timeCell = cells[4];


                        if (timeCell != null)
                        {
                            var dateText = timeCell.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(dateText))
                            {
                                // 뽐뿌는 시간 형식이 "HH:mm:ss" 또는 "yy/MM/dd" 형태
                                if (Regex.IsMatch(dateText, @"^\d{2}:\d{2}:\d{2}$"))
                                {
                                    post.Date = $"{DateTime.Now:yyyy-MM-dd} {dateText}";
                                }
                                else if (Regex.IsMatch(dateText, @"^\d{2}/\d{2}/\d{2}$"))
                                {
                                    DateTime date = DateTime.ParseExact(dateText, "yy/MM/dd", CultureInfo.InvariantCulture);
                                    post.Date = $"{date:yyyy-MM-dd}";
                                }
                                else
                                {
                                    post.Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                            }
                        }

                        // 추천 추출
                        var likesText = cells[5].InnerText.Trim().CleanText();
                        if (likesText != null && !string.IsNullOrWhiteSpace(likesText))
                        {
                            var likesTexts = likesText.Split(['-']);
                            if (likesTexts.Length > 0)
                            {
                                post.Likes = Regex.Replace(likesTexts[0].Trim().CleanText(), @"[^\d]", "");
                            }
                        }

                        // 조회수 추출
                        var viewsText = cells[6].InnerText.Trim().CleanText();
                        if (viewsText != null && !string.IsNullOrWhiteSpace(viewsText)) post.Views = Regex.Replace(viewsText, @"[^\d]", "");

                        // 썸네일 주석 처리
                        // if (await new DatabaseManager().GetExistsLink(post) == false)
                        // {
                        //     var img1Node = row.SelectSingleNode(".//img");
                        //     if (img1Node != null)
                        //     {
                        //         var src = img1Node.GetAttributeValue("src", "");
                        //         if (src != null && !string.IsNullOrEmpty(src))
                        //         {
                        //             //src = src.StartsWith("//") ? src[2..] : src;
                        //             var img_id = await new List<CrawledImage> { new() { ImageUrl = src, Title = post.Title, SourceSite = "ppomppu.co.kr" } }.UploadImageAndReturnId();
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

                Console.WriteLine($"총 {posts.Count}개의 뽐뿌 게시글을 파싱했습니다.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"뽐뿌 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }

        public async Task<List<PostInfo>?> ParseHtmlFileForDetail(string filePath, string url, string no)
        {
            var posts = new List<PostInfo>();

            try
            {
                int.TryParse(no, out int orgNo);

                Console.WriteLine($"뽐뿌 상세 HTML 파일 파싱 중...");

                // 파일에서 HTML 읽기
                var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // HtmlAgilityPack으로 파싱
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var contentDiv = doc.DocumentNode.SelectSingleNode("//table//tbody//tr//td[@class='board-contents']");

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

                // var eyeIcon = doc.DocumentNode.SelectSingleNode(".//i[contains(@class, 'fa-eye')]");
                // var commentIcon = doc.DocumentNode.SelectSingleNode(".//i[contains(@class, 'fa-comment-dots')]");

                // string? viewCount = eyeIcon?.NextSibling?.InnerText.Trim().CleanText();
                // string? commentCount = commentIcon?.NextSibling?.InnerText.Trim().CleanText();

                // Console.WriteLine($"조회수: {viewCount}");
                // Console.WriteLine($"댓글수: {commentCount}");

                // // 상세 컨텐츠 에서 얻은 정보 업데이트
                // new DatabaseManager().EditDetailPostCount(new PostInfo() { Url = url, Views = viewCount, ReplyNum = commentCount });

                // List<string> imageSources = doc.DocumentNode
                //     .SelectNodes(".//td[@class='board-contents']//img[@src]")?
                //     .Select(img => img.GetAttributeValue("src", ""))
                //     .Where(src => !string.IsNullOrEmpty(src))
                //     .ToList() ?? [];

                // List<string> imageSources = doc.DocumentNode
                //     .SelectNodes(".//td[@class='board-contents']//img[@src]")?
                //     .Select(img =>
                //     {
                //         var src = img.GetAttributeValue("src", "");
                //         if (string.IsNullOrEmpty(src)) return "";

                //         // 'https:'가 없으면 추가
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
                Console.WriteLine($"뽐뿌 상세 파일 파싱 오류: {ex.Message}");
            }

            return posts;
        }
    }
}