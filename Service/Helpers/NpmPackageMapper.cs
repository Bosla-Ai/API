namespace Service.Helpers;

public static class NpmPackageMapper
{
    // Maps user-facing skill tags to npm package names for download lookups
    private static readonly Dictionary<string, string> TagToPackage = new(StringComparer.OrdinalIgnoreCase)
    {
        // Frontend Frameworks
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
        ["gatsby"] = "gatsby",
        ["jquery"] = "jquery",

        // Backend Frameworks
        ["express"] = "express",
        ["express.js"] = "express",
        ["node"] = "node",
        ["node.js"] = "node",
        ["nodejs"] = "node",
        ["nest"] = "@nestjs/core",
        ["nestjs"] = "@nestjs/core",
        ["nest.js"] = "@nestjs/core",
        ["fastify"] = "fastify",

        // Languages & Compilers
        ["typescript"] = "typescript",

        // State Management
        ["redux"] = "redux",
        ["mobx"] = "mobx",
        ["zustand"] = "zustand",
        ["pinia"] = "pinia",
        ["vuex"] = "vuex",
        ["ngrx"] = "@ngrx/store",
        ["rxjs"] = "rxjs",

        // Styling
        ["tailwind"] = "tailwindcss",
        ["tailwindcss"] = "tailwindcss",
        ["bootstrap"] = "bootstrap",
        ["sass"] = "sass",
        ["scss"] = "sass",
        ["styled-components"] = "styled-components",
        ["emotion"] = "@emotion/react",

        // Build Tools
        ["webpack"] = "webpack",
        ["vite"] = "vite",
        ["rollup"] = "rollup",
        ["esbuild"] = "esbuild",

        // Testing
        ["jest"] = "jest",
        ["mocha"] = "mocha",
        ["cypress"] = "cypress",
        ["playwright"] = "playwright",
        ["vitest"] = "vitest",

        // API & Data
        ["graphql"] = "graphql",
        ["apollo"] = "@apollo/client",
        ["axios"] = "axios",
        ["prisma"] = "prisma",
        ["mongoose"] = "mongoose",
        ["sequelize"] = "sequelize",
        ["socket.io"] = "socket.io",

        // Utilities
        ["eslint"] = "eslint",
        ["prettier"] = "prettier",
        ["storybook"] = "storybook",
        ["d3"] = "d3",
        ["three.js"] = "three",
        ["tanstack"] = "@tanstack/react-query",

        // Cloud & Auth
        ["firebase"] = "firebase",
        ["supabase"] = "@supabase/supabase-js",
        ["jwt"] = "jsonwebtoken",
        ["passport"] = "passport",
        ["oauth"] = "passport-oauth2",
    };

    public static string? TryMapToPackage(string tag)
    {
        var normalized = TagNormalizer.StripSuffixes(tag);

        return TagToPackage.GetValueOrDefault(normalized);
    }
}
