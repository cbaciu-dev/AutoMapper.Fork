# BKS.CustomMapper

A lightweight, explicit, and high-performance object-to-object mapping library designed as a drop-in replacement for AutoMapper in the EMZ solution. This package is intended for internal use and will be distributed via a private BKS.EMZ feed.

---

## Why BKS.CustomMapper?

AutoMapper is moving to a commercial license. BKS.CustomMapper provides a fully in-house, open, and maintainable alternative with similar core features, optimized for the EMZ solution's needs.

---

## Features

- **Explicit Mapping Profiles:** Define clear, type-safe mapping rules using profiles.
- **Custom Member Mapping:** Support for custom value resolvers, converters, and member-level configuration.
- **Collection & Nested Mapping:** Handles lists, arrays, and nested objects.
- **Ignore & Flattening:** Easily ignore or flatten properties.
- **Validation API:** Detect unmapped or duplicate members at configuration time.
- **Zero Runtime Reflection:** All mapping plans are compiled and cached after configuration.
- **Dependency Injection Ready:** Integrates with Microsoft.Extensions.DependencyInjection.
- **No Hidden Global State:** All configuration is explicit and controlled by you.

---

## Installation

1. **Add the internal NuGet feed** (Azure Artifacts) to your `NuGet.config`.
2. **Install the package:** services.AddMappingServices(executingAssembly);