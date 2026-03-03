using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack; // NuGet: HtmlAgilityPack
using System.Text;
using System.Linq;
using System.Threading;
using Newtonsoft.Json; // NuGet: Newtonsoft.Json
using System.IO;
using Npgsql; // NuGet: Npgsql
using Dapper;
using Marvin.Tmthfh91.Crawling;
using Marvin.Tmthfh91.Crawling.Model;
using System.Globalization;
using Marvin.Tmthfh91.Crawling.Crawler;

namespace Marvin.Tmthfh91.Crawling
{

    public class DatabaseManager
    {
        private readonly string _connectionString;

        public DatabaseManager()
        {
            EnvLoader.Load();

            var host = EnvLoader.Get("DB_HOST", "localhost");
            var port = EnvLoader.Get("DB_PORT", "5432");
            var database = EnvLoader.Get("DB_NAME", "postgres");
            var username = EnvLoader.Get("DB_USER", "postgres");
            var password = EnvLoader.Get("DB_PASSWORD", "");

            _connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
        }

        public async Task<bool> TestConnection()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                Console.WriteLine("✓ 데이터베이스 연결 성공!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 데이터베이스 연결 실패: {ex.Message}");
                return false;
            }
        }

        public async Task RuliwebTitleUpdate()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var updateCmdText = @"
                    UPDATE tmtmfhgi.site_bbs_info a
                    SET reply_num = REGEXP_REPLACE(title, '.*\((\d+)\)$', '\1')::INTEGER,
                        title = TRIM(REGEXP_REPLACE(title, '\s*\(\d+\)$', ''))
                    WHERE title SIMILAR TO '%\([0-9]+\)'
                    AND site = 'Ruliweb'
                    AND posted_dt >= CURRENT_DATE - INTERVAL '7 days'";

                var updateCmd = new NpgsqlCommand(updateCmdText, conn);
                await updateCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RuliwebTitleUpdate 오류: {ex.Message}");
            }
        }

        public async Task ClienTitleUpdate()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var updateCmdText = @"
                    UPDATE tmtmfhgi.site_bbs_info
                    SET title = TRIM(REGEXP_REPLACE(REGEXP_REPLACE(title, '^\s*모공\s*', ''), '\s*\(\d+\)$', ''))
                    WHERE (title ~ '^\s*모공' OR title ~ '\(\d+\)$')
                    AND site = 'Clien'
                    AND reg_date >= CURRENT_DATE - INTERVAL '7 days'";

                var updateCmd = new NpgsqlCommand(updateCmdText, conn);
                await updateCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ClienTitleUpdate 오류: {ex.Message}");
            }
        }

        public async Task<int> InsertOptimizedImagesAndReturnIdAsync(OptimizedImageData dto)
        {
            const string sql = @"
                INSERT INTO tmtmfhgi.optimized_images 
                    (cloudinary_url, cloudinary_public_id, title, is_active)
                VALUES 
                    (@CloudinaryUrl, @CloudinaryPublicId, @Title, @IsActive)
                RETURNING id";

            using var connection = new NpgsqlConnection(_connectionString);

            var id = await connection.QuerySingleAsync<int>(sql, dto);
            return id;
        }

        public async Task<bool> InsertOptimizedImagesAsync(OptimizedImageData dto)
        {
            const string sql = @"
                INSERT INTO tmtmfhgi.optimized_images
                    (cloudinary_url, cloudinary_public_id, title, is_active, no)
                VALUES
                    (@CloudinaryUrl, @CloudinaryPublicId, @Title, @IsActive, @No)";

            using var connection = new NpgsqlConnection(_connectionString);

            await connection.ExecuteAsync(sql, dto);

            return true;
        }

        public async Task<bool> GetExistsLink(PostInfo post)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string? cleanUrl = post.Url?.CleanText(); // URL 정리

            var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM tmtmfhgi.site_bbs_info WHERE url = @url AND img1 IS NOT NULL", conn);
            checkCmd.Parameters.AddWithValue("url", cleanUrl ?? "");
            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
            return exists;
        }

        public async Task<Tuple<int, int>> SavePosts(List<PostInfo> posts, string siteName = "humoruniv")
        {
            int savedCount = 0;
            int updatedCount = 0;

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                foreach (var post in posts)
                {
                    try
                    {
                        // URL 정리
                        string? cleanUrl = post.Url?.CleanText();

                        // 중복 체크 (URL 기준)
                        var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM tmtmfhgi.site_bbs_info WHERE url = @url", conn);
                        checkCmd.Parameters.AddWithValue("url", cleanUrl ?? "");
                        var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

                        if (exists == false)
                        {
                            var insertCmdText = @"INSERT INTO tmtmfhgi.site_bbs_info (
                                                ""number"", title, author, ""date"", ""views"", likes, url, site, reply_num, content, ""posted_dt"", img1
                                                    ) VALUES (
                                                @number, @title, @author, @date, @views, @likes, @url, @site, @reply_num, @content, @posted_dt, @img1
                                                ) RETURNING no";

                            var insertCmd = new NpgsqlCommand(insertCmdText, conn);

                            insertCmd.Parameters.AddWithValue("number", int.TryParse(post.Number?.Replace(",", ""), out int value) ? value : DBNull.Value);
                            insertCmd.Parameters.AddWithValue("title", string.IsNullOrEmpty(post.Title) ? DBNull.Value : post.Title.CleanText());
                            insertCmd.Parameters.AddWithValue("author", string.IsNullOrEmpty(post.Author) ? DBNull.Value : post.Author.CleanText());

                            if (!string.IsNullOrEmpty(post.Date))
                            {
                                if (DateTime.TryParseExact(post.Date, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime) ||
                                    DateTime.TryParseExact(post.Date, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime) ||
                                    DateTime.TryParseExact(post.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
                                {
                                    TimeSpan difference = dateTime - DateTime.Now;
                                    if (difference.TotalSeconds > 0) // 미래
                                    {
                                        dateTime = dateTime.AddDays(-1);
                                    }

                                    post.Date = $"{dateTime:yyyy-MM-dd HH:mm:ss}";
                                }

                                insertCmd.Parameters.AddWithValue("posted_dt", dateTime);
                            }

                            insertCmd.Parameters.AddWithValue("date", string.IsNullOrEmpty(post.Date) ? DBNull.Value : post.Date.CleanText());

                            insertCmd.Parameters.AddWithValue("url", cleanUrl ?? "");
                            insertCmd.Parameters.AddWithValue("site", siteName);


                            insertCmd.Parameters.AddWithValue("views", int.TryParse(post.Views?.Replace(",", ""), out int views) ? views : DBNull.Value);
                            insertCmd.Parameters.AddWithValue("likes", int.TryParse(post.Likes?.Replace(",", ""), out int likes) ? likes : DBNull.Value);
                            insertCmd.Parameters.AddWithValue("reply_num", int.TryParse(post.ReplyNum?.Replace(",", ""), out int reply_num) ? reply_num : DBNull.Value);

                            insertCmd.Parameters.AddWithValue("content", string.IsNullOrEmpty(post.Content) ? DBNull.Value : post.Content);

                            insertCmd.Parameters.AddWithValue("img1", post.img1 == null ? DBNull.Value : post.img1);

                            var insertedNo = await insertCmd.ExecuteScalarAsync();

                            savedCount++;

                            // 상세처리
                            switch (siteName)
                            {
                                case "FMKorea": await new FMKoreaCrawler().CrawlAndProcess($"{cleanUrl},{insertedNo}"); break;
                                case "TheQoo": await new TheQooCrawler().CrawlAndProcess($"{cleanUrl},{insertedNo}"); break;
                                case "Ppomppu": await new PpomppuCrawler().CrawlAndProcess($"{cleanUrl},{insertedNo}"); break;
                                case "Clien": await new ClienCrawler().CrawlAndProcess($"{cleanUrl},{insertedNo}"); break;
                                case "TodayHumor": await new TodayHumorCrawler().CrawlAndProcess($"{cleanUrl},{insertedNo}"); break;
                                case "SlrClub": await new SlrClubCrawler().CrawlAndProcess($"{cleanUrl},{insertedNo}"); break;
                                case "_82Cook": break;
                                case "MlbPark": await new MlbParkCrawler().CrawlAndProcess($"{cleanUrl},{insertedNo}"); break;
                                case "BobaeDream": await new BobaeDreamCrawler().CrawlAndProcess($"{cleanUrl},{insertedNo}"); break;
                                case "Inven": await new InvenCrawler().CrawlAndProcess($"{cleanUrl},{insertedNo}"); break;
                                case "Ruliweb": await new RuliwebCrawler().CrawlAndProcess($"{cleanUrl},{insertedNo}"); break;
                                case "Humoruniv": await new HumorUnivCrawler().CrawlAndProcess($"{cleanUrl},{insertedNo}"); break;
                                case "Damoang": await new DamoangCrawler().CrawlAndProcess($"{cleanUrl},{insertedNo}"); break;
                            }
                        }
                        else
                        {
                            var updateCmdText = @"UPDATE tmtmfhgi.site_bbs_info
                                                SET
                                                    ""views"" = @views,
                                                    likes = @likes,
                                                    reply_num = @reply_num,
                                                    img1 = @img1,
                                                    upd_date = now() AT TIME ZONE 'Asia/Seoul'
                                                WHERE url = @url";

                            var updateCmd = new NpgsqlCommand(updateCmdText, conn);
                            updateCmd.Parameters.AddWithValue("views", int.TryParse(post.Views?.Replace(",", ""), out int views) ? views : DBNull.Value);
                            updateCmd.Parameters.AddWithValue("likes", int.TryParse(post.Likes?.Replace(",", ""), out int likes) ? likes : DBNull.Value);
                            updateCmd.Parameters.AddWithValue("reply_num", int.TryParse(post.ReplyNum?.Replace(",", ""), out int reply_num) ? reply_num : DBNull.Value);

                            updateCmd.Parameters.AddWithValue("img1", post.img1 == null ? DBNull.Value : post.img1);

                            updateCmd.Parameters.AddWithValue("url", cleanUrl ?? "");

                            await updateCmd.ExecuteNonQueryAsync();

                            updatedCount++;
                        }

                        // var updateDateTimeCmd = new NpgsqlCommand(@"
                        // UPDATE tmtmfhgi.site_bbs_info
                        // SET date = (date::TIMESTAMP - INTERVAL '1 day')::VARCHAR
                        // WHERE date::TIMESTAMP > NOW()", conn);
                        // await updateDateTimeCmd.ExecuteNonQueryAsync();

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  게시글 저장 오류 [{post.Title?.CleanText()}]: {ex.Message}");
                        continue;
                    }
                }

                Console.WriteLine($"✓ 데이터베이스에 수정 및 저장 {savedCount}개 게시글 저장 완료!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 데이터베이스 저장 오류: {ex.Message}");
            }

            return new Tuple<int, int>(savedCount, updatedCount);
        }

        public async Task EditDetailPostCount(PostInfo post)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var updateCmdText = @"
                    UPDATE tmtmfhgi.site_bbs_info
                    SET ""views"" = @views,
                        likes = @likes,
                        reply_num = @reply_num,
                        upd_date = now() AT TIME ZONE 'Asia/Seoul',
                        content = @content,
                        img2 = @img2
                    WHERE url = @url";

                var updateCmd = new NpgsqlCommand(updateCmdText, conn);
                updateCmd.Parameters.AddWithValue("views", int.TryParse(post.Views?.Replace(",", ""), out int views) ? views : DBNull.Value);
                updateCmd.Parameters.AddWithValue("likes", int.TryParse(post.Likes?.Replace(",", ""), out int likes) ? likes : DBNull.Value);
                updateCmd.Parameters.AddWithValue("reply_num", int.TryParse(post.ReplyNum?.Replace(",", ""), out int reply_num) ? reply_num : DBNull.Value);
                updateCmd.Parameters.AddWithValue("content", !string.IsNullOrWhiteSpace(post.Content) ? post.Content : DBNull.Value);
                updateCmd.Parameters.AddWithValue("img2", post.img2 != null ? post.img2 : DBNull.Value);
                updateCmd.Parameters.AddWithValue("url", post.Url ?? "");

                await updateCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EditDetailPostCount 오류 [{post.Url}]: {ex.Message}");
            }
        }

        public async Task<List<PostInfo>> GetRecentPosts(string siteName = "humoruniv", int limit = 10)
        {
            var posts = new List<PostInfo>();

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new NpgsqlCommand(@"
                    SELECT ""number"", title, author, ""date"", ""views"", likes, url
                    FROM tmtmfhgi.site_bbs_info
                    WHERE 1=1
                    ORDER BY ""no"" DESC
                    LIMIT @limit", conn);

                cmd.Parameters.AddWithValue("site", siteName);
                cmd.Parameters.AddWithValue("limit", limit);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    posts.Add(new PostInfo
                    {
                        Number = reader["number"]?.ToString(),
                        Title = reader["title"]?.ToString(),
                        Author = reader["author"]?.ToString(),
                        Date = reader["date"]?.ToString(),
                        Views = reader["views"]?.ToString(),
                        Likes = reader["likes"]?.ToString(),
                        Url = reader["url"]?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"조회 오류: {ex.Message}");
            }

            return posts;
        }

        public async Task<int> GetTotalCount()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM tmtmfhgi.site_bbs_info", conn);

                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch
            {
                return 0;
            }
        }

        public async Task<int> GetTotalCount(string siteName = "humoruniv")
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var cmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM tmtmfhgi.site_bbs_info WHERE site = @site", conn);
                cmd.Parameters.AddWithValue("site", siteName);

                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch
            {
                return 0;
            }
        }

        public async Task InsertPost(List<PostInfo> posts, string siteName = "humoruniv")
        {
            try
            {
                // 데이터베이스 저장
                if (posts.Count > 0)
                {
                    var resultCount = await SavePosts(posts, siteName);
                    var savedCount = resultCount.Item1;
                    var updatedCount = resultCount.Item2;

                    var fileName = $"backup_{siteName}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

                    // JSON 백업도 생성 (옵션)
                    // if ((savedCount + updatedCount) < 1)
                    // {
                    //     var json = JsonConvert.SerializeObject(posts, Formatting.Indented);
                    //     await File.WriteAllTextAsync(fileName, json, Encoding.UTF8);
                    // }

                    // 요약 출력
                    Console.WriteLine($"\n수집 요약:");
                    Console.WriteLine($"  - 총 게시글: {posts.Count}개");
                    Console.WriteLine($"  - DB 저장: {savedCount}개 (신규)");
                    Console.WriteLine($"  - DB 저장: {updatedCount}개 (수정)");

                    if (savedCount < 1)
                        Console.WriteLine($"  - 백업 파일: {fileName}");

                    Console.WriteLine($"  - 상위 3개:");
                    foreach (var post in posts.Take(3))
                    {
                        Console.WriteLine($"    • {post.Title} (조회수: {post.Views})");
                    }

                    // DB 통계
                    var totalCount = await GetTotalCount(siteName);
                    Console.WriteLine($"\n📊 DB 통계: 총 {totalCount}개 게시글 보관 중");
                }
                else
                {
                    Console.WriteLine("수집된 게시글이 없습니다.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InsertPost 오류: {ex.Message}");
            }
        }
    }
}
