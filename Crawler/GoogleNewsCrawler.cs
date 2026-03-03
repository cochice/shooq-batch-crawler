using Marvin.Tmthfh91.Crawling.Crawler;
using Marvin.Tmthfh91.Crawling.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Marvin.Tmthfh91.Crawling.Crawler
{
    public class GoogleNewsCrawler : BaseCrawler
    {
        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 구글뉴스 RSS 크롤링 시작: {Site.GoogleNews.url}");

            try
            {
                // RSS 피드 가져오기
                var response = await _httpClient.GetStringAsync(Site.GoogleNews.url);
                Console.WriteLine($"RSS 피드 길이: {response.Length}");

                // XML 파싱
                posts = ParseRssFeed(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"구글뉴스 RSS 크롤링 오류: {ex.Message}");
            }

            return posts;
        }

        private List<PostInfo> ParseRssFeed(string rssContent)
        {
            var posts = new List<PostInfo>();

            try
            {
                Console.WriteLine("구글뉴스 RSS 피드 파싱 중...");

                var doc = new XmlDocument();
                doc.LoadXml(rssContent);

                // RSS 네임스페이스 관리자 설정
                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("content", "http://purl.org/rss/1.0/modules/content/");

                // RSS 아이템들 선택
                var items = doc.SelectNodes("//item");

                if (items == null || items.Count == 0)
                {
                    Console.WriteLine("RSS 아이템을 찾을 수 없습니다.");
                    return posts;
                }

                foreach (XmlNode item in items)
                {
                    try
                    {
                        var post = new PostInfo();

                        // 제목
                        var titleNode = item.SelectSingleNode("title");
                        if (titleNode != null)
                        {
                            var titleText = titleNode.InnerText?.Trim().CleanText();

                            if (string.IsNullOrWhiteSpace(titleText)) continue;
                            if (titleText.Contains("조선일보")) continue;

                            post.Title = titleText;
                            post.Author = titleText.Split('-').Last().Trim();
                        }

                        // URL
                        var linkNode = item.SelectSingleNode("link");
                        if (linkNode != null)
                        {
                            post.Url = linkNode.InnerText?.Trim();
                        }

                        // 설명 (description에서 작성자와 내용 추출)
                        var descriptionNode = item.SelectSingleNode("description");
                        if (descriptionNode != null)
                        {
                            var description = descriptionNode.InnerText?.Trim();
                            //post.Content = description;



                        }

                        // 발행일
                        var pubDateNode = item.SelectSingleNode("pubDate");
                        if (pubDateNode != null)
                        {
                            var pubDateText = pubDateNode.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(pubDateText))
                            {
                                if (DateTime.TryParse(pubDateText, out DateTime pubDate))
                                {
                                    post.Date = pubDate.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                else
                                {
                                    post.Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                            }
                        }

                        // GUID를 번호로 사용 (RSS에는 조회수, 추천수가 없으므로)
                        var guidNode = item.SelectSingleNode("guid");
                        if (guidNode != null)
                        {
                            var guidText = guidNode.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(guidText))
                            {
                                // GUID에서 숫자만 추출하여 번호로 사용
                                var numberMatch = Regex.Match(guidText, @"(\d+)");
                                if (numberMatch.Success)
                                {
                                    post.Number = numberMatch.Groups[1].Value;
                                }
                            }
                        }

                        // RSS 피드에는 조회수, 추천수가 없으므로 기본값
                        post.Views = null;
                        post.Likes = null;
                        post.ReplyNum = null;

                        if (!string.IsNullOrEmpty(post.Title))
                        {
                            posts.Add(post);
                            Console.WriteLine($"  [{post.Number}] {post.Title} - {post.Author}");
                        }
                    }
                    catch
                    {
                        continue; // 개별 아이템 파싱 오류는 무시하고 계속
                    }
                }

                Console.WriteLine($"총 {posts.Count}개의 구글뉴스 기사를 파싱했습니다.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"구글뉴스 RSS 파싱 오류: {ex.Message}");
            }

            return posts;
        }
    }
}