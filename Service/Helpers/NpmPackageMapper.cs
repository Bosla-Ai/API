namespace Service.Helpers;

public static class NpmPackageMapper
{
    // Maps user-facing skill tags to npm package names for download lookups
    private static readonly Dictionary<string, string> TagToPackage = new(StringComparer.OrdinalIgnoreCase)
    {
        ["react"] = "react",
        ["reactjs"] = "react",
        ["react.js"] = "react",
        ["angular"] = "@angular/core",
        ["vue"] = "vue",
        ["vuejs"] = "vue",
        ["vue.js"] = "vue",
        ["svelte"] = "svelte",
        ["next.js"] = "next",
        ["nextjs"] = "next",
        ["nuxt"] = "nuxt",
        ["nuxt.js"] = "nuxt",
        ["express"] = "express",
        ["express.js"] = "express",
        ["node"] = "node",
        ["node.js"] = "node",
        ["nodejs"] = "node",
        ["typescript"] = "typescript",
        ["tailwind"] = "tailwindcss",
        ["tailwindcss"] = "tailwindcss",
        ["bootstrap"] = "bootstrap",
        ["jquery"] = "jquery",
        ["webpack"] = "webpack",
        ["vite"] = "vite",
        ["jest"] = "jest",
        ["mocha"] = "mocha",
        ["redux"] = "redux",
        ["mobx"] = "mobx",
        ["axios"] = "axios",
        ["prisma"] = "prisma",
        ["mongoose"] = "mongoose",
        ["nest"] = "@nestjs/core",
        ["nestjs"] = "@nestjs/core",
        ["fastify"] = "fastify",
        ["graphql"] = "graphql",
        ["apollo"] = "@apollo/client",
        ["d3"] = "d3",
        ["three.js"] = "three",
        ["socket.io"] = "socket.io",
        ["cypress"] = "cypress",
        ["playwright"] = "playwright",
        ["eslint"] = "eslint",
        ["prettier"] = "prettier",
        ["storybook"] = "storybook",
        ["zustand"] = "zustand",
        ["tanstack"] = "@tanstack/react-query",
    };

    public static string? TryMapToPackage(string tag)
    {
        var normalized = TagNormalizer.StripSuffixes(tag);

        return TagToPackage.GetValueOrDefault(normalized);
    }
}
