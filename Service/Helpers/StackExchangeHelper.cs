using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using Shared.Options;

namespace Service.Helpers;

public class StackExchangeHelper(
    HttpClient httpClient,
    ILogger<StackExchangeHelper> logger)
{
    public async Task<TechTagInsightDTO[]?> FetchTagInfoAsync(string tagsBatch, StackExchangeOptions config, CancellationToken cancellationToken)
    {
        var encodedTags = Uri.EscapeDataString(tagsBatch);
        var url = $"{config.BaseUrl}/tags/{encodedTags}/info?site=stackoverflow&key={config.ApiKey}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
        using var json = await FetchGzippedJsonAsync(url, cts.Token);
        if (json is null) return null;

        var results = new List<TechTagInsightDTO>();
        if (json.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString() ?? "";
                var count = item.GetProperty("count").GetInt32();
                var hasSynonyms = item.TryGetProperty("has_synonyms", out var syn) && syn.GetBoolean();

                results.Add(new TechTagInsightDTO
                {
                    TagName = name,
                    QuestionCount = count,
                    HasSynonyms = hasSynonyms
                });
            }
        }

        return results.ToArray();
    }

    public async Task<string[]> FetchRelatedTagsAsync(string tag, StackExchangeOptions config, CancellationToken cancellationToken)
    {
        var encodedTag = Uri.EscapeDataString(tag);
        var url = $"{config.BaseUrl}/tags/{encodedTag}/related?site=stackoverflow&key={config.ApiKey}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
        using var json = await FetchGzippedJsonAsync(url, cts.Token);
        if (json is null) return [];

        var related = new List<string>();
        if (json.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray().Take(5))
            {
                var name = item.GetProperty("name").GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    related.Add(name);
            }
        }

        return related.ToArray();
    }

    public async Task<JsonDocument?> FetchGzippedJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.AcceptEncoding.ParseAdd("gzip");
        request.Headers.UserAgent.ParseAdd("Bosla/1.0");

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("StackExchange API returned {StatusCode}: {Error}", response.StatusCode, error);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        // StackExchange always returns gzip-compressed responses
        Stream decompressed = response.Content.Headers.ContentEncoding.Contains("gzip")
            ? new GZipStream(stream, CompressionMode.Decompress)
            : stream;

        await using (decompressed)
        {
            return await JsonDocument.ParseAsync(decompressed, cancellationToken: cancellationToken);
        }
    }

    public static string NormalizeTag(string tag)
    {
        var normalized = TagNormalizer.StripSuffixes(tag);
        normalized = normalized.Replace(' ', '-');

        return normalized switch
        {
            // .NET Ecosystem
            "c#" or "csharp" => "c#",
            "asp.net-core" or "aspnet-core" or "asp.net-core-web-api" or ".net-core" or ".net" => "asp.net-core",
            "entity-framework-core" or "ef-core" => "entity-framework-core",
            "linq" => "linq",
            "blazor" => "blazor",
            "signalr" => "signalr",

            // C/C++
            "c++" or "cpp" => "c++",
            "c" => "c",

            // JavaScript Ecosystem
            "javascript" or "js" => "javascript",
            "typescript" or "ts" => "typescript",
            "react" or "reactjs" or "react.js" or "react-js" => "reactjs",
            "angular" or "angularjs" => "angular",
            "vue" or "vuejs" or "vue.js" or "vue-js" => "vue.js",
            "svelte" => "svelte",
            "node" or "nodejs" or "node.js" or "node-js" => "node.js",
            "express" or "expressjs" or "express.js" => "express",
            "next.js" or "nextjs" or "next-js" => "next.js",
            "nuxt" or "nuxt.js" => "nuxt.js",
            "gatsby" => "gatsby",
            "nestjs" or "nest.js" => "nestjs",
            "jquery" => "jquery",
            "redux" => "redux",
            "webpack" => "webpack",
            "vite" => "vite",

            // Python Ecosystem
            "python" => "python",
            "django" => "django",
            "flask" => "flask",
            "fastapi" => "fastapi",

            // Java/JVM Ecosystem
            "java" => "java",
            "spring" or "spring-boot" => "spring-boot",
            "kotlin" => "kotlin",
            "scala" => "scala",

            // Other Languages
            "go" or "golang" => "go",
            "rust" => "rust",
            "swift" => "swift",
            "ruby" or "ruby-on-rails" or "rails" => "ruby-on-rails",
            "php" or "laravel" => "php",
            "dart" or "flutter" => "flutter",
            "elixir" => "elixir",
            "haskell" => "haskell",
            "perl" => "perl",
            "lua" => "lua",
            "r" or "r-programming" => "r",
            "objective-c" => "objective-c",

            // Mobile
            "android" => "android",
            "ios" => "ios",
            "react-native" => "react-native",
            "swiftui" => "swiftui",
            "jetpack-compose" => "android-jetpack-compose",

            // Databases
            "sql" => "sql",
            "mysql" => "mysql",
            "postgresql" or "postgres" => "postgresql",
            "mongodb" or "mongo" => "mongodb",
            "redis" => "redis",
            "elasticsearch" => "elasticsearch",
            "sqlite" => "sqlite",
            "cassandra" => "cassandra",
            "dynamodb" => "amazon-dynamodb",
            "neo4j" => "neo4j",
            "graphql" => "graphql",

            // DevOps & Cloud
            "docker" => "docker",
            "kubernetes" or "k8s" => "kubernetes",
            "aws" or "amazon-web-services" => "amazon-web-services",
            "azure" => "azure",
            "gcp" or "google-cloud" or "google-cloud-platform" => "google-cloud-platform",
            "terraform" => "terraform",
            "ansible" => "ansible",
            "jenkins" => "jenkins",
            "github-actions" => "github-actions",
            "nginx" => "nginx",
            "helm" => "kubernetes-helm",
            "prometheus" => "prometheus",
            "grafana" => "grafana",

            // Version Control & Tools
            "git" => "git",
            "github" => "github",
            "gitlab" => "gitlab",
            "linux" => "linux",
            "bash" or "shell" or "shell-scripting" => "bash",

            // Frontend Basics
            "html" => "html",
            "css" => "css",
            "sass" or "scss" => "sass",
            "tailwind" or "tailwindcss" => "tailwind-css",
            "bootstrap" => "twitter-bootstrap",

            // Data Science & AI
            "machine-learning" or "ml" => "machine-learning",
            "deep-learning" or "dl" => "deep-learning",
            "data-science" => "data-science",
            "tensorflow" => "tensorflow",
            "pytorch" => "pytorch",
            "keras" => "keras",
            "scikit-learn" => "scikit-learn",
            "pandas" => "pandas",
            "numpy" => "numpy",
            "nlp" or "natural-language-processing" => "nlp",
            "computer-vision" => "computer-vision",
            "ai" or "artificial-intelligence" => "artificial-intelligence",

            // Testing
            "jest" => "jestjs",
            "pytest" => "pytest",
            "selenium" => "selenium",
            "cypress" => "cypress",

            // Architecture & Design
            "design-patterns" => "design-patterns",
            "microservices" or "micro-services" => "microservices",
            "system-design" => "system-design",
            "rest-api" or "restful-api" or "api-design" => "rest",
            "oop" or "object-oriented-programming" => "oop",

            // Security
            "cybersecurity" or "cyber-security" => "cybersecurity",
            "cryptography" => "cryptography",

            // Other
            "blockchain" => "blockchain",
            "web3" => "web3",
            "firebase" => "firebase",
            "supabase" => "supabase",

            _ => normalized
        };
    }
}
