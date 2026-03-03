# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET 9.0 console application for web crawling Korean community sites (Humoruniv, FM Korea, TheQoo). The application supports both single-run and scheduled crawling modes, with data persistence to PostgreSQL database and JSON backup files.

## Development Commands

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run
```

### Clean
```bash
dotnet clean
```

### Restore packages
```bash
dotnet restore
```

## Architecture Overview

The application follows a modular crawler architecture:

- **BaseCrawler**: Abstract base class providing common HTTP client setup with user-agent rotation and Korean locale headers
- **Site-specific Crawlers**: HumorUnivCrawler, FMKoreaCrawler, TheQooCrawler - each inheriting from BaseCrawler
- **DatabaseManager**: Handles PostgreSQL connections, duplicate detection, and data persistence
- **PostInfo Model**: Record type representing crawled post data (number, title, author, date, views, likes, URL, reply count)
- **Site Model**: Static classes containing site-specific URLs and identifiers

## Key Dependencies

- **HtmlAgilityPack**: HTML parsing and DOM manipulation
- **Selenium WebDriver**: Browser automation for dynamic content
- **Npgsql**: PostgreSQL database connectivity
- **NLog**: Logging framework
- **Newtonsoft.Json**: JSON serialization for backup files
- **System.Text.Encoding.CodePages**: EUC-KR encoding support for Korean sites

## Database Schema

The application uses PostgreSQL with schema `tmtmfhgi.site_bbs_info` containing fields:
- number, title, author, date, views, likes, url, site, reply_num, no (auto-increment)

## Crawling Features

- Interactive console menu for selecting crawling modes
- Support for single-run and scheduled (1-hour interval) crawling
- Automatic duplicate detection based on URL
- HTML file parsing for offline analysis
- Database statistics and recent posts viewing
- Automatic JSON backup when database save fails