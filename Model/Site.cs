using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Marvin.Tmthfh91.Crawling.Model
{
    /*
     * 완료여부 | 사이트 | Thumbnail
     * (X)딴지[딴지일보]
     * (X)이토[이토랜드]
     *
     * [-] (O)웃대[웃긴대학]
     * [V] (O)더쿠
     * [-] (O)뽐뿌
     * [V] (O)다뫙[다모앙]
     * [V] (O)엠팍[엠엘비파크]
     * [V] (O)루리[루리웹]

     * [V] (O)펨코[에펨코리아]
     * [V] (O)클리앙
     * [V] (O)보배[보배드림]
     * [V] (O)인벤
     * [V] (O)오늘의 유머
     * [V] (O)SLR 클럽
     * (O)82cook

     * (O)네이버뉴스
     * (O)구글뉴스
     */
    public record Site
    {
        public static class Humoruniv
        {
            public static readonly string text = "Humoruniv";
            public static readonly string url = "https://web.humoruniv.com/board/humor/list.html?table=pds&st=day";
        }

        public static class FMKorea
        {
            public static readonly string text = "FMKorea";
            public static readonly string url = "https://www.fmkorea.com/best";
        }

        public static class TheQoo
        {
            public static readonly string text = "TheQoo";
            public static readonly string url = "https://theqoo.net/hot";
        }

        public static class NaverNews
        {
            public static readonly string text = "NaverNews";
            public static readonly string url = "https://openapi.naver.com/v1/search/news.json";
        }

        public static class Ppomppu
        {
            public static readonly string text = "Ppomppu";
            public static readonly string url = "https://ppomppu.co.kr/hot.php";
        }

        public static class GoogleNews
        {
            public static readonly string text = "GoogleNews";
            public static readonly string url = "https://news.google.com/rss?hl=ko&gl=KR&ceid=KR:ko";
        }

        public static class Clien
        {
            public static readonly string text = "Clien";
            public static readonly string url = "https://www.clien.net/service/recommend";
        }

        public static class TodayHumor
        {
            public static readonly string text = "TodayHumor";
            public static readonly string url = "https://www.todayhumor.co.kr/board/list.php?table=humorbest";
        }

        public static class SlrClub
        {
            public static readonly string text = "SlrClub";
            public static readonly string url = "https://www.slrclub.com/bbs/zboard.php?id=best_article";
        }

        public static class _82Cook
        {
            public static readonly string text = "82Cook";
            public static readonly string url = "https://www.82cook.com/entiz/enti.php?bn=15&page=1";
        }

        public static class MlbPark
        {
            public static readonly string text = "MlbPark";
            public static readonly string url = "https://mlbpark.donga.com/mp/b.php?b=bullpen";
        }

        public static class BobaeDream
        {
            public static readonly string text = "BobaeDream";
            public static readonly string url = "https://www.bobaedream.co.kr/list?code=best";
        }

        public static class Inven
        {
            public static readonly string text = "Inven";
            public static readonly string url = "https://hot.inven.co.kr/best";
        }

        public static class Ruliweb
        {
            public static readonly string text = "Ruliweb";
            public static readonly string url = "https://bbs.ruliweb.com/best";
        }

        public static class Ddanzi
        {
            public static readonly string text = "Ddanzi";
            public static readonly string url = "https://www.ddanzi.com/hot_all";
        }

        public static class Etoland
        {
            public static readonly string text = "Etoland";
            public static readonly string url = "https://etoland.co.kr/bbs/hit.php";
        }

        public static class Damoang
        {
            public static readonly string text = "Damoang";
            //public static readonly string url = "https://damoang.net/free?bo_table=free&sop=and&sod=desc&sfl=wr_datetime&stx=2025-08-30&sca=&page=1&sst=wr_good";
            public static readonly string url = "https://damoang.net/free";
        }

        public static class YouTube
        {
            public static readonly string text = "YouTube";
            public static readonly string url = "https://www.youtube.com/feeds/videos.xml?chart_name=MostPopular";
        }
    }
}
