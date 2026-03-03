using Marvin.Tmthfh91.Crawling.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Marvin.Tmthfh91.Crawling.Crawler
{
    public class NaverNewsCrawler : BaseCrawler
    {
        private readonly string _clientId;
        private readonly string _clientSecret;

        public NaverNewsCrawler()
        {
            EnvLoader.Load();
            _clientId = EnvLoader.Get("NAVER_CLIENT_ID");
            _clientSecret = EnvLoader.Get("NAVER_CLIENT_SECRET");
        }

        public override async Task<List<PostInfo>> CrawlAndProcess(string urlAndNo = "")
        {
            var posts = new List<PostInfo>();

            try
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 네이버 뉴스 크롤링 시작");

                // 인기 키워드로 뉴스 검색
                var popularKeywords = new[] { "정치", "경제", "사회", "문화", "스포츠", "IT", "연예" };

                foreach (var keyword in popularKeywords)
                {
                    var keywordPosts = await SearchNews(keyword, 5); // 키워드당 5개씩
                    posts.AddRange(keywordPosts);
                }

                Console.WriteLine($"총 {posts.Count}개의 뉴스를 수집했습니다.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"크롤링 오류: {ex.Message}");
            }

            return posts;
        }

        public async Task<List<PostInfo>> SearchNews(string query, int display = 10)
        {
            var posts = new List<PostInfo>();

            try
            {
                // HTTP 클라이언트 설정
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-Naver-Client-Id", _clientId);
                _httpClient.DefaultRequestHeaders.Add("X-Naver-Client-Secret", _clientSecret);

                // API 요청 URL 구성
                var apiUrl = $"https://openapi.naver.com/v1/search/news.json?query={Uri.EscapeDataString(query)}&display={display}&sort=date";

                var response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var newsResult = JsonConvert.DeserializeObject<NaverNewsResponse>(jsonContent);

                    if (newsResult?.Items != null)
                    {
                        foreach (var item in newsResult.Items)
                        {
                            var post = new PostInfo
                            {
                                Title = CleanHtmlTags(item.Title ?? ""),
                                Author = ExtractPublisher(item.OriginalLink ?? ""),
                                Date = pubDateToDate(item.PubDate ?? ""),
                                Content = CleanHtmlTags(item.Description ?? ""),
                                Views = null, // 네이버 API에서는 조회수 제공 안함
                                Likes = null, // 네이버 API에서는 추천수 제공 안함
                                Url = item.Link ?? "",
                                ReplyNum = null
                            };

                            string pubDateToDate(string s)
                            {
                                DateTimeOffset dto = DateTimeOffset.ParseExact(
                                    s,
                                    "ddd, dd MMM yyyy HH:mm:ss zzz",
                                    CultureInfo.InvariantCulture
                                );
                                string result = dto.ToString("yyyy-MM-dd HH:mm:ss");
                                return result;
                            }

                            posts.Add(post);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"네이버 API 호출 실패: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"뉴스 검색 오류 [{query}]: {ex.Message}");
            }

            return posts;
        }

        private string CleanHtmlTags(string text)
        {
            return Regex.Replace(text, @"<.*?>", "");
        }

        private string ExtractPublisher(string link)
        {
            try
            {
                var uri = new Uri(link);
                var host = uri.Host;

                // 주요 언론사 호스트명에서 언론사명 추출
                if (host.Contains("chosun")) return "조선일보";
                if (host.Contains("donga")) return "동아일보";
                if (host.Contains("joongang")) return "중앙일보";
                if (host.Contains("hani")) return "한겨레";
                if (host.Contains("khan")) return "경향신문";
                if (host.Contains("yonhap")) return "연합뉴스";
                if (host.Contains("yna")) return "연합뉴스";
                if (host.Contains("news1")) return "뉴스1";
                if (host.Contains("newsis")) return "뉴시스";

                // National General Daily Newspapers
                if (host.Contains("khan")) return "경향신문";
                if (host.Contains("kmib")) return "국민일보";
                if (host.Contains("naeil")) return "내일신문";
                if (host.Contains("seoul.co.kr")) return "서울신문";
                if (host.Contains("segye")) return "세계일보";
                if (host.Contains("asiatoday")) return "아시아투데이";
                if (host.Contains("hani")) return "한겨레";
                if (host.Contains("hankookilbo")) return "한국일보";

                // Regional Daily Newspapers
                if (host.Contains("kado")) return "강원도민일보";
                if (host.Contains("kwnews")) return "강원일보";
                if (host.Contains("kgnews")) return "경기신문";
                if (host.Contains("kyeonggi")) return "경기일보";
                if (host.Contains("idomin")) return "경남도민일보";
                if (host.Contains("knnews")) return "경남신문";
                if (host.Contains("idaegu.co.kr")) return "대구신문";
                if (host.Contains("idaegu.com")) return "대구일보";
                if (host.Contains("busan")) return "부산일보";
                if (host.Contains("yeongnam")) return "영남일보";
                if (host.Contains("iusm")) return "울산매일";
                if (host.Contains("ulsanpress")) return "울산신문";

                // Economic Daily Newspapers
                if (host.Contains("dnews")) return "대한경제";
                if (host.Contains("mt.co.kr")) return "머니투데이";
                if (host.Contains("metroseoul")) return "메트로경제";
                if (host.Contains("sedaily")) return "서울경제";
                if (host.Contains("asiae")) return "아시아경제";
                if (host.Contains("ajunews")) return "아주경제";
                if (host.Contains("edaily")) return "이데일리";
                if (host.Contains("etoday")) return "이투데이";
                if (host.Contains("fnnews")) return "파이낸셜뉴스";
                if (host.Contains("heraldcorp")) return "헤럴드경제";

                // Sports Daily Newspapers
                if (host.Contains("sportsseoul")) return "스포츠서울";
                if (host.Contains("sportsworldi")) return "스포츠월드";

                // English Daily Newspapers
                if (host.Contains("koreatimes")) return "코리아타임스";
                if (host.Contains("koreaherald")) return "코리아헤럴드";

                // Internet Newspapers
                if (host.Contains("ebn")) return "EBN";
                if (host.Contains("nocutnews")) return "노컷뉴스";
                if (host.Contains("newspenguin")) return "뉴스펭귄";
                if (host.Contains("newspim")) return "뉴스핌";
                if (host.Contains("dailian")) return "데일리안";
                if (host.Contains("mediapen")) return "미디어펜";
                if (host.Contains("seoulwire")) return "서울와이어";
                if (host.Contains("kukinews")) return "쿠키뉴스";
                if (host.Contains("pressian")) return "프레시안";

                // Broadcast Stations
                if (host.Contains("imbc")) return "MBC";
                if (host.Contains("mbn")) return "MBN";
                if (host.Contains("obs")) return "OBS";
                if (host.Contains("ytn")) return "YTN";
                if (host.Contains("sbs")) return "SBS";
                if (host.Contains("kbs")) return "KBS";
                if (host.Contains("mbc")) return "MBC";

                // 기본적으로 호스트명 반환
                return host.Replace("www.", "");
            }
            catch
            {
                return "알 수 없음";
            }
        }

        private string ParsePubDate(string pubDate)
        {
            try
            {
                // RFC 2822 형식을 한국어 형식으로 변환
                if (DateTime.TryParse(pubDate, out DateTime date))
                {
                    return date.ToString("yyyy-MM-dd HH:mm:ss");
                }
                return pubDate;
            }
            catch
            {
                return pubDate;
            }
        }
    }

    // 네이버 뉴스 API 응답 모델
    public class NaverNewsResponse
    {
        public string? LastBuildDate { get; set; }
        public int Total { get; set; }
        public int Start { get; set; }
        public int Display { get; set; }
        public List<NewsItem>? Items { get; set; }
    }

    public class NewsItem
    {
        public string? Title { get; set; }
        public string? OriginalLink { get; set; }
        public string? Link { get; set; }
        public string? Description { get; set; }
        public string? PubDate { get; set; }
    }
}