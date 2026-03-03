using Marvin.Tmthfh91.Crawling;
using Marvin.Tmthfh91.Crawling.Crawler;
using Marvin.Tmthfh91.Crawling.Model;
using NLog;
using System.Net;
using System.Net.Http;
using System.Text;

class Program
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    static void PrintLogo()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        var logo = @"
███╗   ███╗ █████╗ ██████╗ ██╗   ██╗██╗███╗   ██╗
████╗ ████║██╔══██╗██╔══██╗██║   ██║██║████╗  ██║
██╔████╔██║███████║██████╔╝██║   ██║██║██╔██╗ ██║
██║╚██╔╝██║██╔══██║██╔══██╗╚██╗ ██╔╝██║██║╚██╗██║
██║ ╚═╝ ██║██║  ██║██║  ██║ ╚████╔╝ ██║██║ ╚████║
╚═╝     ╚═╝╚═╝  ╚═╝╚═╝  ╚═╝  ╚═══╝  ╚═╝╚═╝  ╚═══╝
            ";
        Console.WriteLine(logo);
        Console.ForegroundColor = ConsoleColor.Yellow;
        var logo1 = @"       SHOOQ • CRAWLING SYSTEM v1.0";
        Console.WriteLine(logo1);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        var logo2 = @"       ═══════════════════════════════";
        Console.WriteLine(logo2);
        Console.ResetColor();
        Console.WriteLine();
    }

    static void PrintSpider()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        var spider = @"
              \  |  /
               \ | /
                \|/
            ____███____
           /  o     o  \
          |      >      |
           \    ___    /
            '--|   |--'
               |   |
              /|   |\
             / |   | \
            ";
        Console.WriteLine(spider);
        Console.ResetColor();
    }

    static void PrintLoadingAnimation()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        var loadingAnimation = @"
    [크롤링 중...]
         ╱|、
        (˚ˎ 。7
         |、˜〵
         じしˍ,)ノ
            ";
        Console.WriteLine(loadingAnimation);
        Console.ResetColor();
    }

    static async Task Main(string[] args)
    {
        var mm = 30; // 전체사이클 대기 분
        var ss = 10; // 크롤러 사이 대기 초

        logger.Info("Application started");

        // EUC-KR 인코딩 지원을 위한 프로바이더 등록 (필수!)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        logger.Debug("EUC-KR encoding provider registered");

        // 콘솔 설정
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "Marvin Shoooq Crawler";
        logger.Debug("Console configuration completed");

        var humorUnivCrawler = new HumorUnivCrawler();
        var fmKoreaCrawler = new FMKoreaCrawler();
        var theQooCrawler = new TheQooCrawler();
        var naverNewsCrawler = new NaverNewsCrawler();
        var ppomppuCrawler = new PpomppuCrawler();
        var googleNewsCrawler = new GoogleNewsCrawler();
        var clienCrawler = new ClienCrawler();
        var todayHumorCrawler = new TodayHumorCrawler();
        var slrClubCrawler = new SlrClubCrawler();
        var _82CookCrawler = new _82CookCrawler();
        var mlbParkCrawler = new MlbParkCrawler();
        var bobaeDreamCrawler = new BobaeDreamCrawler();
        var invenCrawler = new InvenCrawler();
        var ruliwebCrawler = new RuliwebCrawler();
        var ddanziCrawler = new DdanziCrawler();
        var etolandCrawler = new EtolandCrawler();
        var damoangCrawler = new DamoangCrawler();
        var youtubeCrawler = new YouTubeCrawler();

        // 로고 출력
        PrintLogo();

        // DB 연결 테스트
        Console.ForegroundColor = ConsoleColor.DarkGray;
        logger.Info("데이터베이스 연결 확인 중...");
        Console.ResetColor();

        var dbManager = new DatabaseManager();
        bool dbConnected = await dbManager.TestConnection();

        if (dbConnected)
        {
            var totalCount = await dbManager.GetTotalCount();
            Console.ForegroundColor = ConsoleColor.Green;
            //Console.WriteLine($"📊 현재 DB에 {totalCount}개의 게시글이 저장되어 있습니다.\n");
            logger.Info($"📊 현재 DB에 {totalCount}개의 게시글이 저장되어 있습니다.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            //Console.WriteLine("⚠️  DB 연결 실패 - JSON 파일로만 저장됩니다.\n");
            logger.Warn("⚠️  DB 연결 실패 - JSON 파일로만 저장됩니다.");
            Console.ResetColor();
        }

        // 🔄 정기 크롤링 (10초 10분 간격으로 순차 실행)
        Console.Clear();
        PrintSpider();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($@"
┌─────────────────────────────────┐
│   🕷️  순차 크롤링 시작  🕷️   │
│   {mm}분 간격으로 순차 실행합니다 │
│   Ctrl+C로 종료할 수 있습니다   │
└─────────────────────────────────┘
                    ");
        Console.ResetColor();

        // 크롤러 리스트와 이름 정의
        var crawlers = new List<(BaseCrawler crawler, string name, string site)>
        {
            (fmKoreaCrawler, "FM코리아", Site.FMKorea.text),
            (theQooCrawler, "더쿠", Site.TheQoo.text),
            //(naverNewsCrawler, "네이버뉴스", Site.NaverNews.text),
            (ppomppuCrawler, "뽐뿌", Site.Ppomppu.text),
            //(googleNewsCrawler, "구글뉴스", Site.GoogleNews.text),
            (clienCrawler, "클리앙", Site.Clien.text),
            (todayHumorCrawler, "오늘의유머", Site.TodayHumor.text),
            (slrClubCrawler, "SLR클럽", Site.SlrClub.text),
            (_82CookCrawler, "82쿡", Site._82Cook.text),
            (mlbParkCrawler, "MLB파크", Site.MlbPark.text),
            (bobaeDreamCrawler, "보배드림", Site.BobaeDream.text),
            (invenCrawler, "인벤", Site.Inven.text),
            (ruliwebCrawler, "루리웹", Site.Ruliweb.text),
            (humorUnivCrawler, "유머대학", Site.Humoruniv.text),
            (damoangCrawler, "다모앙", Site.Damoang.text),
            // (youtubeCrawler, "유튜브", Site.YouTube.text),
        };

        //Console.WriteLine($"\n📋 총 {crawlers.Count}개 사이트를 10초 간격으로 순차 실행합니다.");
        //Console.WriteLine("🔄 전체 크롤링 완료 후 10분 대기 후 다시 반복합니다.\n");
        logger.Info($"🔄 전체 크롤링 완료 후 {mm}분 대기 후 다시 반복합니다.");
        logger.Info($"순차 크롤링 시작 - {crawlers.Count}개 사이트, {ss}초 간격, {mm}분 주기 반복");

        int cycleCount = 1;

        // 무한 반복
        while (true)
        {
            logger.Info($"이전 로그 파일 확인");
            var now = DateTime.Now;

            // 현재 월 이전 로그 파일 삭제
            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (Directory.Exists(logsDir))
            {
                var firstDayOfThisMonth = new DateTime(now.Year, now.Month, 1);
                foreach (var logFile in Directory.GetFiles(logsDir, "????-??-??.log"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(logFile);
                    if (DateTime.TryParseExact(fileName, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var fileDate) && fileDate < firstDayOfThisMonth)
                    {
                        File.Delete(logFile);
                        logger.Info($"이전 월 로그 파일 삭제: {Path.GetFileName(logFile)}");
                    }
                }
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 === 크롤링 사이클 {cycleCount} 시작 ===");
            logger.Info($"[{DateTime.Now:HH:mm:ss}] 🔄 === 크롤링 사이클 {cycleCount} 시작 ===");
            Console.ResetColor();

            // 각 크롤러를 순차적으로 실행
            for (int i = 0; i < crawlers.Count; i++)
            {
                var (crawler, name, site) = crawlers[i];

                Console.ForegroundColor = ConsoleColor.Cyan;
                //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🚀 {name} 크롤링 시작 ({i + 1}/{crawlers.Count})");
                logger.Info($"[{DateTime.Now:HH:mm:ss}] 🚀 {name} 크롤링 시작 ({i + 1}/{crawlers.Count})");
                Console.ResetColor();

                try
                {
                    var posts = await crawler.CrawlAndProcess();

                    if (posts.Count > 0)
                    {
                        await dbManager.InsertPost(posts, site);

                        // if ("루리웹".Equals(name)) dbManager.RuliwebTitleUpdate();
                        // if ("클리앙".Equals(name)) dbManager.ClienTitleUpdate();

                        Console.ForegroundColor = ConsoleColor.Green;
                        //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ {name} 완료 - {posts.Count}개 게시글 수집");
                        logger.Info($"[{DateTime.Now:HH:mm:ss}] ✅ {name} 완료 - {posts.Count}개 게시글 수집");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️  {name} 완료 - 수집된 게시글 없음");
                        logger.Warn($"[{DateTime.Now:HH:mm:ss}] ⚠️  {name} 완료 - 수집된 게시글 없음");
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ {name} 오류: {ex.Message}");
                    logger.Error(ex, $"❌ {name} 크롤링 중 오류 발생");
                    Console.ResetColor();
                }

                // 마지막 크롤러가 아닐 경우에만 대기
                if (i < crawlers.Count - 1)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⏳ 다음 크롤링까지 {ss}초 대기... (다음: {crawlers[i + 1].name})");
                    logger.Debug($"다음 크롤링까지 {ss}초 대기 (다음: {crawlers[i + 1].name})");
                    Console.ResetColor();

                    // ss초 대기
                    await Task.Delay(TimeSpan.FromSeconds(ss));
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            //Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] 🎉 크롤링 사이클 {cycleCount} 완료!");
            logger.Info($"크롤링 사이클 {cycleCount} 완료");
            Console.ResetColor();

            cycleCount++;

            // 전체 크롤링 완료 후 mm분 대기
            Console.ForegroundColor = ConsoleColor.DarkGray;
            //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⏳ 다음 전체 크롤링까지 {mm}분 대기...");
            logger.Info($"다음 전체 크롤링까지 {mm}분 대기");
            Console.ResetColor();
            await Task.Delay(TimeSpan.FromMinutes(mm));
        }
    }

    static async Task MainBackup(string[] args)
    {
        var mm = 30; // 전체사이클 대기 분
        var ss = 10; // 크롤러 사이 대기 초

        logger.Info("Application started");

        // EUC-KR 인코딩 지원을 위한 프로바이더 등록 (필수!)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        logger.Debug("EUC-KR encoding provider registered");

        // 콘솔 설정
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "Marvin Shoooq Crawler";
        logger.Debug("Console configuration completed");

        var humorUnivCrawler = new HumorUnivCrawler();
        var fmKoreaCrawler = new FMKoreaCrawler();
        var theQooCrawler = new TheQooCrawler();
        var naverNewsCrawler = new NaverNewsCrawler();
        var ppomppuCrawler = new PpomppuCrawler();
        var googleNewsCrawler = new GoogleNewsCrawler();
        var clienCrawler = new ClienCrawler();
        var todayHumorCrawler = new TodayHumorCrawler();
        var slrClubCrawler = new SlrClubCrawler();
        var _82CookCrawler = new _82CookCrawler();
        var mlbParkCrawler = new MlbParkCrawler();
        var bobaeDreamCrawler = new BobaeDreamCrawler();
        var invenCrawler = new InvenCrawler();
        var ruliwebCrawler = new RuliwebCrawler();
        var ddanziCrawler = new DdanziCrawler();
        var etolandCrawler = new EtolandCrawler();
        var damoangCrawler = new DamoangCrawler();
        var youtubeCrawler = new YouTubeCrawler();

        // 로고 출력
        PrintLogo();

        // DB 연결 테스트
        Console.ForegroundColor = ConsoleColor.DarkGray;
        logger.Info("데이터베이스 연결 확인 중...");
        Console.ResetColor();

        var dbManager = new DatabaseManager();
        bool dbConnected = await dbManager.TestConnection();

        if (dbConnected)
        {
            var totalCount = await dbManager.GetTotalCount();
            Console.ForegroundColor = ConsoleColor.Green;
            //Console.WriteLine($"📊 현재 DB에 {totalCount}개의 게시글이 저장되어 있습니다.\n");
            logger.Info($"📊 현재 DB에 {totalCount}개의 게시글이 저장되어 있습니다.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            //Console.WriteLine("⚠️  DB 연결 실패 - JSON 파일로만 저장됩니다.\n");
            logger.Warn("⚠️  DB 연결 실패 - JSON 파일로만 저장됩니다.");
            Console.ResetColor();
        }

        Console.WriteLine("실행 모드를 선택하세요:");
        logger.Info("실행 모드를 선택하세요:");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  [1] 🔄 순차 크롤링 ({ss}초 및 {mm}분 간격)");
        Console.WriteLine("  [2] 📄 기존 HTML 파일 파싱");
        Console.WriteLine("  [3] 📊 DB 데이터 조회");
        Console.WriteLine("  [4] 🔍 웃대 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [5] 🔍 FM코리아 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [6] 🔍 더쿠 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [7] 📰 네이버뉴스 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [8] 🔥 뽐뿌 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [9] 📰 구글뉴스 RSS 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [10] 💻 클리앙 추천 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [11] 😄 오늘의유머 베스트 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [12] 📷 SLR클럽 베스트 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [13] 🍳 82Cook 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [14] ⚾ MLB Park 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [15] 🚗 Bobae Dream 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [16] 🎮 Inven 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [17] 🎯 Ruliweb 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [18] 📰 Ddanzi 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [19] 🌐 Etoland 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [20] 💬 Damoang 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [21]  YouTube 단일 크롤링 (1회 실행)");
        Console.WriteLine("  [0] ❌ 종료");
        Console.ResetColor();
        Console.WriteLine();
        Console.Write("선택 >> ");

        var choice = Console.ReadLine();
        logger.Info($"User selected option: {choice}");

        switch (choice)
        {
            case "1":
                // 🔄 정기 크롤링 (10초 10분 간격으로 순차 실행)
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(@"
┌─────────────────────────────────┐
│   🕷️  순차 크롤링 시작  🕷️   │
│   10분 간격으로 순차 실행합니다 │
│   Ctrl+C로 종료할 수 있습니다   │
└─────────────────────────────────┘
                    ");
                Console.ResetColor();

                // 크롤러 리스트와 이름 정의
                var crawlers = new List<(BaseCrawler crawler, string name, string site)>
                {
                    (fmKoreaCrawler, "FM코리아", Site.FMKorea.text),
                    (theQooCrawler, "더쿠", Site.TheQoo.text),
                    //(naverNewsCrawler, "네이버뉴스", Site.NaverNews.text),
                    (ppomppuCrawler, "뽐뿌", Site.Ppomppu.text),
                    //(googleNewsCrawler, "구글뉴스", Site.GoogleNews.text),
                    (clienCrawler, "클리앙", Site.Clien.text),
                    (todayHumorCrawler, "오늘의유머", Site.TodayHumor.text),
                    (slrClubCrawler, "SLR클럽", Site.SlrClub.text),
                    (_82CookCrawler, "82쿡", Site._82Cook.text),
                    (mlbParkCrawler, "MLB파크", Site.MlbPark.text),
                    (bobaeDreamCrawler, "보배드림", Site.BobaeDream.text),
                    (invenCrawler, "인벤", Site.Inven.text),
                    (ruliwebCrawler, "루리웹", Site.Ruliweb.text),
                    (humorUnivCrawler, "유머대학", Site.Humoruniv.text),
                    (damoangCrawler, "다모앙", Site.Damoang.text),
                    // (youtubeCrawler, "유튜브", Site.YouTube.text),
                };

                //Console.WriteLine($"\n📋 총 {crawlers.Count}개 사이트를 10초 간격으로 순차 실행합니다.");
                //Console.WriteLine("🔄 전체 크롤링 완료 후 10분 대기 후 다시 반복합니다.\n");
                logger.Info($"🔄 전체 크롤링 완료 후 {mm}분 대기 후 다시 반복합니다.");
                logger.Info($"순차 크롤링 시작 - {crawlers.Count}개 사이트, {ss}초 간격, {mm}분 주기 반복");

                int cycleCount = 1;

                // 무한 반복
                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 === 크롤링 사이클 {cycleCount} 시작 ===");
                    logger.Info($"[{DateTime.Now:HH:mm:ss}] 🔄 === 크롤링 사이클 {cycleCount} 시작 ===");
                    Console.ResetColor();

                    // 각 크롤러를 순차적으로 실행
                    for (int i = 0; i < crawlers.Count; i++)
                    {
                        var (crawler, name, site) = crawlers[i];

                        Console.ForegroundColor = ConsoleColor.Cyan;
                        //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🚀 {name} 크롤링 시작 ({i + 1}/{crawlers.Count})");
                        logger.Info($"[{DateTime.Now:HH:mm:ss}] 🚀 {name} 크롤링 시작 ({i + 1}/{crawlers.Count})");
                        Console.ResetColor();

                        try
                        {
                            var posts = await crawler.CrawlAndProcess();

                            if (posts.Count > 0)
                            {
                                await dbManager.InsertPost(posts, site);

                                // if ("루리웹".Equals(name)) dbManager.RuliwebTitleUpdate();
                                // if ("클리앙".Equals(name)) dbManager.ClienTitleUpdate();

                                Console.ForegroundColor = ConsoleColor.Green;
                                //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ {name} 완료 - {posts.Count}개 게시글 수집");
                                logger.Info($"[{DateTime.Now:HH:mm:ss}] ✅ {name} 완료 - {posts.Count}개 게시글 수집");
                                Console.ResetColor();
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️  {name} 완료 - 수집된 게시글 없음");
                                logger.Warn($"[{DateTime.Now:HH:mm:ss}] ⚠️  {name} 완료 - 수집된 게시글 없음");
                                Console.ResetColor();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ {name} 오류: {ex.Message}");
                            logger.Error(ex, $"❌ {name} 크롤링 중 오류 발생");
                            Console.ResetColor();
                        }

                        // 마지막 크롤러가 아닐 경우에만 대기
                        if (i < crawlers.Count - 1)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⏳ 다음 크롤링까지 {ss}초 대기... (다음: {crawlers[i + 1].name})");
                            logger.Debug($"다음 크롤링까지 {ss}초 대기 (다음: {crawlers[i + 1].name})");
                            Console.ResetColor();

                            // ss초 대기
                            await Task.Delay(TimeSpan.FromSeconds(ss));
                        }
                    }

                    Console.ForegroundColor = ConsoleColor.Green;
                    //Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] 🎉 크롤링 사이클 {cycleCount} 완료!");
                    logger.Info($"크롤링 사이클 {cycleCount} 완료");
                    Console.ResetColor();

                    cycleCount++;

                    // 전체 크롤링 완료 후 mm분 대기
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⏳ 다음 전체 크롤링까지 {mm}분 대기...");
                    logger.Info($"다음 전체 크롤링까지 {mm}분 대기");
                    Console.ResetColor();
                    await Task.Delay(TimeSpan.FromMinutes(mm));
                }

            //break;

            #region  [ cases ]

            case "2":
                Console.WriteLine("선택하세요:");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("  [1] 웃대");
                Console.WriteLine("  [2] FM코리아");
                Console.WriteLine("  [3] 더쿠");
                Console.WriteLine("  [4] 뽐뿌");
                Console.WriteLine("  [5] 클리앙");
                Console.WriteLine("  [6] 오늘의유머");
                Console.WriteLine("  [7] SLR클럽");
                Console.WriteLine("  [8] 82Cook");
                Console.WriteLine("  [9] MLB Park");
                Console.WriteLine("  [10] Bobae Dream");
                Console.WriteLine("  [11] Inven");
                Console.WriteLine("  [12] Ruliweb");
                Console.WriteLine("  [13] Ddanzi");
                Console.WriteLine("  [14] Etoland");
                Console.WriteLine("  [15] Damoang");
                Console.WriteLine("  [0] ❌ 종료");
                Console.ResetColor();
                Console.WriteLine();
                Console.Write("선택 >> ");

                var ch = Console.ReadLine();

                // 최신 HTML 파일 자동 찾기
                var filePath = string.Empty;
                string[]? htmlFiles = null;
                Func<string, string[]?> funcHtmlPath = s => Directory.GetFiles(Define.HtmlPath, $"temp_{s}_*.html");
                switch (ch)
                {
                    case "1": htmlFiles = funcHtmlPath(Site.Humoruniv.text); break;
                    case "2": htmlFiles = funcHtmlPath(Site.FMKorea.text); break;
                    case "3": htmlFiles = funcHtmlPath(Site.TheQoo.text); break;
                    case "4": htmlFiles = funcHtmlPath(Site.Ppomppu.text); break;
                    case "5": htmlFiles = funcHtmlPath(Site.Clien.text); break;
                    case "6": htmlFiles = funcHtmlPath(Site.TodayHumor.text); break;
                    case "7": htmlFiles = funcHtmlPath(Site.SlrClub.text); break;
                    case "8": htmlFiles = funcHtmlPath(Site._82Cook.text); break;
                    case "9": htmlFiles = funcHtmlPath(Site.MlbPark.text); break;
                    case "10": htmlFiles = funcHtmlPath(Site.BobaeDream.text); break;
                    case "11": htmlFiles = funcHtmlPath(Site.Inven.text); break;
                    case "12": htmlFiles = funcHtmlPath(Site.Ruliweb.text); break;
                    case "13": htmlFiles = funcHtmlPath(Site.Ddanzi.text); break;
                    case "14": htmlFiles = funcHtmlPath(Site.Etoland.text); break;
                    case "15": htmlFiles = funcHtmlPath(Site.Damoang.text); break;
                }

                if (htmlFiles?.Length > 0)
                {
                    filePath = htmlFiles.OrderByDescending(f => f).First();
                    //Console.WriteLine($"최신 파일 선택: {filePath}");
                    logger.Info($"최신 파일 선택: {filePath}");
                }
                else
                {
                    //Console.WriteLine("HTML 파일을 찾을 수 없습니다.");
                    logger.Warn("HTML 파일을 찾을 수 없습니다.");
                    return;
                }

                if (File.Exists(filePath))
                {
                    List<PostInfo> filePosts = [];

                    switch (ch)
                    {
                        case "1": filePosts = await humorUnivCrawler.ParseHtmlFile(filePath); break;
                        case "2": filePosts = await fmKoreaCrawler.ParseHtmlFile(filePath); break;
                        case "3": filePosts = await theQooCrawler.ParseHtmlFile(filePath); break;
                        case "4": filePosts = await ppomppuCrawler.ParseHtmlFile(filePath); break;
                        case "5": filePosts = await clienCrawler.ParseHtmlFile(filePath); break;
                        case "6": filePosts = await todayHumorCrawler.ParseHtmlFile(filePath); break;
                        case "7": filePosts = await slrClubCrawler.ParseHtmlFile(filePath); break;
                        case "8": filePosts = await _82CookCrawler.ParseHtmlFile(filePath); break;
                        case "9": filePosts = await mlbParkCrawler.ParseHtmlFile(filePath); break;
                        case "10": filePosts = await bobaeDreamCrawler.ParseHtmlFile(filePath); break;
                        case "11": filePosts = await invenCrawler.ParseHtmlFile(filePath); break;
                        case "12": filePosts = await ruliwebCrawler.ParseHtmlFile(filePath); break;
                        case "13": filePosts = await ddanziCrawler.ParseHtmlFile(filePath); break;
                        case "14": filePosts = await etolandCrawler.ParseHtmlFile(filePath); break;
                        case "15": filePosts = await damoangCrawler.ParseHtmlFile(filePath); break;
                    }

                    if (filePosts != null && filePosts.Count > 0)
                    {
                        var siteName = string.Empty;
                        switch (ch)
                        {
                            case "1": siteName = Site.Humoruniv.text; break;
                            case "2": siteName = Site.FMKorea.text; break;
                            case "3": siteName = Site.TheQoo.text; break;
                            case "4": siteName = Site.Ppomppu.text; break;
                            case "5": siteName = Site.Clien.text; break;
                            case "6": siteName = Site.TodayHumor.text; break;
                            case "7": siteName = Site.SlrClub.text; break;
                            case "8": siteName = Site._82Cook.text; break;
                            case "9": siteName = Site.MlbPark.text; break;
                            case "10": siteName = Site.BobaeDream.text; break;
                            case "11": siteName = Site.Inven.text; break;
                            case "12": siteName = Site.Ruliweb.text; break;
                            case "13": siteName = Site.Ddanzi.text; break;
                            case "14": siteName = Site.Etoland.text; break;
                            case "15": siteName = Site.Damoang.text; break;
                        }

                        await dbManager.InsertPost(filePosts, siteName);
                    }
                    else
                    {
                        //Console.WriteLine("수집된 게시글이 없습니다.");
                        logger.Warn("수집된 게시글이 없습니다.");
                    }
                }
                else
                {
                    //Console.WriteLine($"파일을 찾을 수 없습니다: {filePath}");
                    logger.Error($"파일을 찾을 수 없습니다: {filePath}");
                }
                break;

            case "3":
                // DB 데이터 조회
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=== DB 데이터 조회 ===\n");
                Console.ResetColor();

                var recentPosts = await dbManager.GetRecentPosts(Site.Humoruniv.text, 20);
                if (recentPosts.Count > 0)
                {
                    //Console.WriteLine($"최근 저장된 게시글 {recentPosts.Count}개:\n");
                    logger.Info($"최근 저장된 게시글 {recentPosts.Count}개 조회");
                    foreach (var post in recentPosts)
                    {
                        Console.WriteLine($"[{post.Number}] {post.Title}");
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"   작성자: {post.Author} | 날짜: {post.Date} | 조회: {post.Views} | 추천: {post.Likes}");
                        Console.ResetColor();
                    }

                    var totalInDb = await dbManager.GetTotalCount(Site.Humoruniv.text);
                    Console.WriteLine($"\n📊 전체 DB 저장 게시글: {totalInDb}개");
                    logger.Info($"전체 DB 저장 게시글: {totalInDb}개");
                }
                else
                {
                    Console.WriteLine("저장된 데이터가 없습니다.");
                    logger.Warn("저장된 데이터가 없습니다.");
                }
                break;

            case "4":
                // 웃대 정기 크롤링
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(@"
┌─────────────────────────────────┐
│   🕷️  정기 크롤링 시작  🕷️   │
│   Ctrl+C로 종료할 수 있습니다   │
└─────────────────────────────────┘
                    ");
                PrintLoadingAnimation();

                var huPosts = await humorUnivCrawler.CrawlAndProcess();

                if (huPosts.Count > 0)
                {
                    await dbManager.InsertPost(huPosts, Site.Humoruniv.text);
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("웃대 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "5":
                // FM코리아 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("=== FM코리아 단일 크롤링 실행 ===\n");
                Console.ResetColor();

                PrintLoadingAnimation();

                var fmPosts = await fmKoreaCrawler.CrawlAndProcess();

                if (fmPosts.Count > 0)
                {
                    await dbManager.InsertPost(fmPosts, Site.FMKorea.text);
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("FM코리아 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "6":
                // 더쿠 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("=== 더쿠 단일 크롤링 실행 ===\n");
                Console.ResetColor();

                var theQooPosts = await theQooCrawler.CrawlAndProcess();

                if (theQooPosts.Count > 0)
                {
                    await dbManager.InsertPost(theQooPosts, Site.TheQoo.text);
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("더쿠 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "7":
                // 네이버뉴스 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("=== 네이버뉴스 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var naverPosts = await naverNewsCrawler.CrawlAndProcess();

                if (naverPosts.Count > 0)
                {
                    await dbManager.InsertPost(naverPosts, Site.NaverNews.text);
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 뉴스가 없습니다.");
                    logger.Warn("네이버뉴스 크롤링 - 수집된 뉴스가 없습니다.");
                }
                break;

            case "8":
                // 뽐뿌 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("=== 뽐뿌 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var ppomppuPosts = await ppomppuCrawler.CrawlAndProcess();

                if (ppomppuPosts.Count > 0)
                {
                    await dbManager.InsertPost(ppomppuPosts, Site.Ppomppu.text);
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                }
                break;

            case "9":
                // 구글뉴스 RSS 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("=== 구글뉴스 RSS 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var googleNewsPosts = await googleNewsCrawler.CrawlAndProcess();

                if (googleNewsPosts.Count > 0)
                {
                    await dbManager.InsertPost(googleNewsPosts, Site.GoogleNews.text);
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 뉴스가 없습니다.");
                }
                break;

            case "10":
                // 클리앙 추천 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=== 클리앙 추천 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var clienPosts = await clienCrawler.CrawlAndProcess();

                if (clienPosts.Count > 0)
                {
                    await dbManager.InsertPost(clienPosts, Site.Clien.text);
                    logger.Info($"클리앙 크롤링 완료 - {clienPosts.Count}개 게시글 수집");
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("클리앙 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "11":
                // 오늘의유머 베스트 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("=== 오늘의유머 베스트 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var todayHumorPosts = await todayHumorCrawler.CrawlAndProcess();

                if (todayHumorPosts.Count > 0)
                {
                    await dbManager.InsertPost(todayHumorPosts, Site.TodayHumor.text);
                    logger.Info($"오늘의유머 크롤링 완료 - {todayHumorPosts.Count}개 게시글 수집");
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("오늘의유머 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "12":
                // SLR클럽 베스트 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("=== SLR클럽 베스트 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var slrClubPosts = await slrClubCrawler.CrawlAndProcess();

                if (slrClubPosts.Count > 0)
                {
                    await dbManager.InsertPost(slrClubPosts, Site.SlrClub.text);
                    logger.Info($"SLR클럽 크롤링 완료 - {slrClubPosts.Count}개 게시글 수집");
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("SLR클럽 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "13":
                // 82Cook 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("=== 82Cook 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var _82CookPosts = await _82CookCrawler.CrawlAndProcess();

                if (_82CookPosts.Count > 0)
                {
                    await dbManager.InsertPost(_82CookPosts, Site._82Cook.text);
                    logger.Info($"82쿡 크롤링 완료 - {_82CookPosts.Count}개 게시글 수집");
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("82쿡 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "14":
                // MLB Park 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("=== MLB Park 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var mlbParkPosts = await mlbParkCrawler.CrawlAndProcess();

                if (mlbParkPosts.Count > 0)
                {
                    await dbManager.InsertPost(mlbParkPosts, Site.MlbPark.text);
                    logger.Info($"MLB파크 크롤링 완료 - {mlbParkPosts.Count}개 게시글 수집");
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("MLB파크 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "15":
                // Bobae Dream 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("=== Bobae Dream 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var bobaeDreamPosts = await bobaeDreamCrawler.CrawlAndProcess();

                if (bobaeDreamPosts.Count > 0)
                {
                    await dbManager.InsertPost(bobaeDreamPosts, Site.BobaeDream.text);
                    logger.Info($"보배드림 크롤링 완료 - {bobaeDreamPosts.Count}개 게시글 수집");
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("보배드림 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "16":
                // Inven 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("=== Inven 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var invenPosts = await invenCrawler.CrawlAndProcess();

                if (invenPosts.Count > 0)
                {
                    await dbManager.InsertPost(invenPosts, Site.Inven.text);
                    logger.Info($"인벤 크롤링 완료 - {invenPosts.Count}개 게시글 수집");
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("인벤 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "17":
                // Ruliweb 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=== Ruliweb 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var ruliwebPosts = await ruliwebCrawler.CrawlAndProcess();

                if (ruliwebPosts.Count > 0)
                {
                    await dbManager.InsertPost(ruliwebPosts, Site.Ruliweb.text);
                    logger.Info($"루리웹 크롤링 완료 - {ruliwebPosts.Count}개 게시글 수집");
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("루리웹 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "18":
                // Ddanzi 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("=== Ddanzi 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var ddanziPosts = await ddanziCrawler.CrawlAndProcess();

                if (ddanziPosts.Count > 0)
                {
                    await dbManager.InsertPost(ddanziPosts, Site.Ddanzi.text);
                    logger.Info($"따드지 크롤링 완료 - {ddanziPosts.Count}개 게시글 수집");
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("따드지 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "19":
                // Etoland 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("=== Etoland 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var etolandPosts = await etolandCrawler.CrawlAndProcess();

                if (etolandPosts.Count > 0)
                {
                    await dbManager.InsertPost(etolandPosts, Site.Etoland.text);
                    logger.Info($"이토랜드 크롤링 완료 - {etolandPosts.Count}개 게시글 수집");
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("이토랜드 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "20":
                // Damoang 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("=== Damoang 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var damoangPosts = await damoangCrawler.CrawlAndProcess();

                if (damoangPosts.Count > 0)
                {
                    await dbManager.InsertPost(damoangPosts, Site.Damoang.text);
                    logger.Info($"다모아 크롤링 완료 - {damoangPosts.Count}개 게시글 수집");
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("다모아 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "21":
                // Youtube 단일 실행
                Console.Clear();
                PrintSpider();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("=== YouTube 단일 크롤링 실행 ===\n");
                Console.ResetColor();
                PrintLoadingAnimation();

                var youtubePosts = await youtubeCrawler.CrawlAndProcess();

                if (youtubePosts.Count > 0)
                {
                    await dbManager.InsertPost(youtubePosts, Site.YouTube.text);
                    logger.Info($"YouTube 크롤링 완료 - {youtubePosts.Count}개 게시글 수집");
                }
                else
                {
                    Console.WriteLine("\n✗ 수집된 게시글이 없습니다.");
                    logger.Warn("YouTube 크롤링 - 수집된 게시글이 없습니다.");
                }
                break;

            case "0":
                Console.WriteLine("파일 업로드 테스트");
                var imageSources = new List<string>() { "https://img-cdn.theqoo.net/aqGxAd.jpg" };
                var results = await new CloudflareR2Uploader().UploadMultipleSequential(imageSources);
                break;

            #endregion

            default:
                Console.WriteLine("잘못된 선택입니다.");
                logger.Warn($"잘못된 선택: {choice}");
                break;
        }

        Console.WriteLine("\n프로그램이 종료됩니다.");
        logger.Info("프로그램 종료");

        if (Environment.UserInteractive && !Console.IsInputRedirected)
        {
            Console.ReadKey();
        }
    }
}