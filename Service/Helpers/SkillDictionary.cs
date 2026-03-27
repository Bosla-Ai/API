using System.Text.RegularExpressions;

namespace Service.Helpers;

public static class SkillDictionary
{
    // ── Programming Languages ───────────────────────────────────────
    public static readonly HashSet<string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        "JavaScript", "TypeScript", "Python", "Java", "C#", "C++", "Go", "Rust",
        "Ruby", "PHP", "Swift", "Kotlin", "Scala", "R", "Dart", "Elixir",
        "Haskell", "Lua", "Perl", "Objective-C", "MATLAB", "Julia", "Groovy",
        "Shell", "Bash", "PowerShell", "SQL", "T-SQL", "PL/SQL", "GraphQL",
        "HTML", "CSS", "SASS", "LESS", "Solidity"
    };

    // ── Frameworks & Libraries ──────────────────────────────────────
    public static readonly HashSet<string> Frameworks = new(StringComparer.OrdinalIgnoreCase)
    {
        "React", "Angular", "Vue", "Vue.js", "Next.js", "Nuxt.js", "Svelte",
        "Node.js", "Express", "Express.js", "NestJS", "Fastify",
        "Django", "Flask", "FastAPI", "Spring", "Spring Boot",
        "ASP.NET", ".NET", ".NET Core", "Blazor", "Entity Framework",
        "Ruby on Rails", "Rails", "Laravel", "Symfony",
        "Flutter", "React Native", "SwiftUI", "Jetpack Compose",
        "jQuery", "Bootstrap", "Tailwind CSS", "Material UI", "Chakra UI",
        "Redux", "MobX", "Zustand", "TanStack Query", "React Query",
        "Prisma", "Sequelize", "Hibernate", "Dapper",
        "TensorFlow", "PyTorch", "Keras", "scikit-learn", "Pandas", "NumPy",
        "OpenCV", "Hugging Face", "LangChain", "LlamaIndex",
        "Electron", "Tauri", "Qt", "WPF", "WinForms",
        "Unity", "Unreal Engine", "Godot",
        "Gatsby", "Remix", "Astro", "SolidJS", "Qwik",
        "Selenium", "Cypress", "Playwright", "Puppeteer",
        "Jest", "Mocha", "Vitest", "xUnit", "NUnit", "JUnit", "pytest",
        "Storybook", "Webpack", "Vite", "Rollup", "esbuild"
    };

    // ── Cloud & Infrastructure ──────────────────────────────────────
    public static readonly HashSet<string> Cloud = new(StringComparer.OrdinalIgnoreCase)
    {
        "AWS", "Amazon Web Services", "Azure", "Google Cloud", "GCP",
        "Heroku", "Vercel", "Netlify", "DigitalOcean", "Cloudflare",
        "Lambda", "EC2", "S3", "DynamoDB", "CloudFront", "API Gateway",
        "Azure Functions", "Azure DevOps", "App Service",
        "Cloud Functions", "Cloud Run", "BigQuery", "Firestore",
        "Firebase", "Supabase", "Amplify"
    };

    // ── DevOps & CI/CD ──────────────────────────────────────────────
    public static readonly HashSet<string> DevOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Docker", "Kubernetes", "K8s", "Terraform", "Ansible", "Pulumi",
        "Jenkins", "GitHub Actions", "GitLab CI", "CircleCI", "Travis CI",
        "ArgoCD", "Helm", "Istio", "Prometheus", "Grafana", "Datadog",
        "New Relic", "ELK", "Elasticsearch", "Logstash", "Kibana",
        "Nginx", "Apache", "HAProxy", "Caddy",
        "CI/CD", "Infrastructure as Code", "IaC",
        "Linux", "Ubuntu", "CentOS", "RHEL",
        "Vagrant", "Packer", "Consul", "Vault"
    };

    // ── Databases ───────────────────────────────────────────────────
    public static readonly HashSet<string> Databases = new(StringComparer.OrdinalIgnoreCase)
    {
        "PostgreSQL", "MySQL", "MariaDB", "SQL Server", "Oracle",
        "MongoDB", "Redis", "Cassandra", "CouchDB", "Neo4j",
        "SQLite", "DynamoDB", "CosmosDB", "Cosmos DB", "Firestore",
        "InfluxDB", "TimescaleDB", "CockroachDB", "PlanetScale",
        "Supabase", "Memcached", "RabbitMQ", "Kafka", "Apache Kafka",
        "NATS", "ZeroMQ", "ActiveMQ", "SQS", "SNS", "EventBridge"
    };

    // ── Architecture & Practices ────────────────────────────────────
    public static readonly HashSet<string> Practices = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microservices", "REST", "RESTful", "gRPC", "WebSocket",
        "Event-Driven", "CQRS", "Domain-Driven Design", "DDD",
        "Clean Architecture", "Hexagonal Architecture", "SOLID",
        "Design Patterns", "System Design", "API Design",
        "OAuth", "JWT", "SSO", "SAML", "OpenID Connect",
        "TDD", "BDD", "Unit Testing", "Integration Testing", "E2E Testing",
        "Code Review", "Pair Programming", "Mob Programming",
        "Agile", "Scrum", "Kanban", "SAFe",
        "DevSecOps", "SRE", "Observability", "Monitoring",
        "Performance Optimization", "Scalability", "High Availability",
        "Data Modeling", "ETL", "Data Pipeline", "Data Warehouse",
        "Machine Learning", "Deep Learning", "NLP", "Computer Vision",
        "MLOps", "Feature Engineering", "A/B Testing",
        "Accessibility", "WCAG", "i18n", "l10n",
        "SEO", "PWA", "Web Performance", "Core Web Vitals"
    };

    // ── Tools & Platforms ───────────────────────────────────────────
    public static readonly HashSet<string> Tools = new(StringComparer.OrdinalIgnoreCase)
    {
        "Git", "GitHub", "GitLab", "Bitbucket",
        "Jira", "Confluence", "Linear", "Notion", "Trello",
        "Figma", "Sketch", "Adobe XD", "InVision",
        "Postman", "Swagger", "OpenAPI", "Insomnia",
        "VS Code", "IntelliJ", "Visual Studio", "Vim", "Neovim",
        "SonarQube", "SonarCloud", "ESLint", "Prettier",
        "Sentry", "PagerDuty", "Splunk",
        "Stripe", "PayPal", "Twilio", "SendGrid",
        "Auth0", "Okta", "Keycloak",
        "Terraform Cloud", "Vault", "Consul"
    };

    // ── Certifications ──────────────────────────────────────────────
    public static readonly HashSet<string> Certifications = new(StringComparer.OrdinalIgnoreCase)
    {
        "AWS Certified", "Azure Certified", "GCP Certified",
        "Certified Kubernetes", "CKA", "CKAD",
        "PMP", "Scrum Master", "CSM", "PSM",
        "CISSP", "CEH", "CompTIA Security+", "CompTIA Network+",
        "TOGAF", "ITIL", "Six Sigma"
    };

    public static readonly IReadOnlyList<(string Category, HashSet<string> Skills)> AllCategories =
    [
        ("Language", Languages),
        ("Framework", Frameworks),
        ("Cloud", Cloud),
        ("DevOps", DevOps),
        ("Database", Databases),
        ("Practice", Practices),
        ("Tool", Tools),
        ("Certification", Certifications)
    ];

    public static Dictionary<string, string> ExtractSkills(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return [];

        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (category, skills) in AllCategories)
        {
            foreach (var skill in skills)
            {
                if (results.ContainsKey(skill))
                    continue;

                bool matched = skill.Length <= 2 && skill.All(char.IsLetterOrDigit)
                    ? Regex.IsMatch(description, @$"\b{Regex.Escape(skill)}\b", RegexOptions.IgnoreCase)
                    : description.Contains(skill, StringComparison.OrdinalIgnoreCase);

                if (matched)
                {
                    results[skill] = category;
                }
            }
        }

        return results;
    }
}
