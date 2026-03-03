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
    public class DdanziCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();
            string? htmlFileName = null;

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  Ddanzi 크롤링 시작: {Site.Ddanzi.url}");

            var options = GetChromeOptions();
            using var driver = new ChromeDriver(options);

            // 타임아웃 설정
            SetupDriverTimeouts(driver);

            try
            {
                driver.Navigate().GoToUrl(Site.Ddanzi.url);

                // webdriver 속성 숨기기
                ((IJavaScriptExecutor)driver).ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");

                // 페이지 로딩 대기
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                wait.Until(d => d.FindElement(By.TagName("body")));

                var html = driver.PageSource;
                Console.WriteLine($"HTML 길이: {html.Length}");

                htmlFileName = $"{Define.HtmlPath}temp_{Site.Ddanzi.text}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                await File.WriteAllTextAsync(htmlFileName, html, Encoding.UTF8);
                Console.WriteLine($"임시 파일 생성: {htmlFileName}");

                // 파일에서 파싱
                if (htmlFileName != null) posts = await ParseHtmlFile(htmlFileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ddanzi 크롤링 오류: {ex.Message}");
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

                // Ddanzi 게시글 테이블 파싱
                var rows = doc.DocumentNode.SelectNodes("//table[@class='board_list']//tbody//tr | //table[contains(@class, 'list')]//tr | //div[@class='board_list']//tr");

                if (rows == null || rows.Count == 0)
                {
                    // 다른 패턴도 시도
                    rows = doc.DocumentNode.SelectNodes("//table//tbody//tr | //table//tr[td]");
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

                        // 번호 추출 (document_srl 파라미터에서)
                        var numberMatch = Regex.Match(firstCellText, @"\d+");
                        if (numberMatch.Success)
                        {
                            post.Number = numberMatch.Value;
                        }

                        // 제목과 URL 찾기
                        HtmlNode? titleTd = null;
                        for (int i = 1; i < tds.Count; i++)
                        {
                            var linkNode = tds[i].SelectSingleNode(".//a[contains(@href, 'document_srl')]");
                            if (linkNode != null && !string.IsNullOrEmpty(linkNode.InnerText.Trim()))
                            {
                                titleTd = tds[i];
                                break;
                            }
                        }

                        if (titleTd != null)
                        {
                            var titleLink = titleTd.SelectSingleNode(".//a[contains(@href, 'document_srl')]");
                            if (titleLink != null)
                            {
                                post.Title = titleLink.InnerText.CleanText();
                                var href = titleLink.GetAttributeValue("href", "");
                                if (!string.IsNullOrEmpty(href))
                                {
                                    post.Url = href.StartsWith("http") ? href : $"https://www.ddanzi.com{href}";

                                    // URL에서 document_srl 추출하여 번호로 사용
                                    var urlNumberMatch = Regex.Match(href, @"document_srl=(\d+)");
                                    if (urlNumberMatch.Success && string.IsNullOrEmpty(post.Number))
                                    {
                                        post.Number = urlNumberMatch.Groups[1].Value;
                                    }
                                }
                            }

                            // 댓글 수 추출 (제목 td 내에서)
                            var replyMatch = Regex.Match(titleTd.InnerText, @"\[(\d+)\]|\((\d+)\)|댓글\s*(\d+)");
                            if (replyMatch.Success)
                            {
                                for (int i = 1; i <= 3; i++)
                                {
                                    if (replyMatch.Groups[i].Success)
                                    {
                                        post.ReplyNum = replyMatch.Groups[i].Value;
                                        break;
                                    }
                                }
                            }
                        }

                        // 작성자 찾기 (popup_menu_area 링크)
                        for (int i = 2; i < tds.Count; i++)
                        {
                            var authorLink = tds[i].SelectSingleNode(".//a[contains(@href, 'popup_menu_area')]");
                            if (authorLink != null)
                            {
                                post.Author = authorLink.InnerText.CleanText();
                                break;
                            }

                            // 링크가 없는 경우 텍스트에서 찾기
                            var authorText = tds[i].InnerText.Trim().CleanText();
                            if (!string.IsNullOrEmpty(authorText) && !Regex.IsMatch(authorText, @"^\d+$") &&
                                !authorText.Contains(":") && !authorText.Contains("조회") && authorText.Length < 20)
                            {
                                post.Author = authorText;
                                break;
                            }
                        }

                        // 날짜 찾기
                        for (int i = Math.Max(2, tds.Count - 3); i < tds.Count; i++)
                        {
                            var dateText = tds[i].InnerText.Trim().CleanText();
                            if (!string.IsNullOrEmpty(dateText))
                            {
                                // 시간 형식 (HH:mm)
                                if (Regex.IsMatch(dateText, @"^\d{2}:\d{2}$"))
                                {
                                    post.Date = $"{DateTime.Now:yyyy-MM-dd} {dateText}";
                                    break;
                                }
                                // 날짜 형식들
                                else if (Regex.IsMatch(dateText, @"\d{4}-\d{2}-\d{2}") ||
                                        Regex.IsMatch(dateText, @"\d{2}/\d{2}/\d{2}") ||
                                        Regex.IsMatch(dateText, @"\d{2}\.\d{2}\.\d{2}"))
                                {
                                    post.Date = dateText;
                                    break;
                                }
                            }
                        }

                        // 추천수 찾기 (icon_good.png 이미지와 함께 있는 숫자)
                        for (int i = 2; i < tds.Count; i++)
                        {
                            var goodIcon = tds[i].SelectSingleNode(".//img[contains(@src, 'icon_good.png')]");
                            if (goodIcon != null)
                            {
                                var likesText = tds[i].InnerText.Trim().CleanText();
                                post.Likes = Regex.Replace(likesText, @"[^\d]", "");
                                break;
                            }
                        }

                        // 조회수 찾기 (마지막 컬럼에서 숫자만)
                        for (int i = tds.Count - 1; i >= 2; i--)
                        {
                            var viewsText = tds[i].InnerText.Trim().CleanText();
                            if (!string.IsNullOrEmpty(viewsText) && Regex.IsMatch(viewsText, @"^\d+$") &&
                                viewsText.Length <= 6 && string.IsNullOrEmpty(post.Views))
                            {
                                post.Views = viewsText;
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(post.Title) && !string.IsNullOrEmpty(post.Number))
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