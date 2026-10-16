using System.Text.Json;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Server.Security;
using Erable.Core.Categories;
using Erable.Core.Elements;
using Erable.Core.Properties;
using Erable.Server.Projects;
using Erable.Server.Users;
using Erable.Storage;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Erable.Server.Api;

/// <summary>Categories, property definitions and resolved values (docs/architecture.md §8.1, F07, F08).</summary>
public static class StructureEndpoints
{
    public static RouteGroupBuilder MapStructureEndpoints(this RouteGroupBuilder project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var categories = project.MapGroup("/categories").WithTags("Categories");
        categories.MapGet("/", ListCategories).WithName("ListCategories");
        categories.MapPost("/", CreateCategory).WithName("CreateCategory");
        categories.MapGet("/{id}", GetCategory).WithName("GetCategory");
        categories.MapPut("/{id}", UpdateCategory).WithName("UpdateCategory");
        categories.MapGet("/{id}/pages", GetCategoryPages).WithName("GetCategoryPages");
        categories.MapPut("/{id}/values/{propertyId}", SetCategoryValue).WithName("SetCategoryValue");
        categories.MapDelete("/{id}/values/{propertyId}", RemoveCategoryValue).WithName("RemoveCategoryValue");

        var properties = project.MapGroup("/properties").WithTags("Properties");
        properties.MapGet("/", ListProperties).WithName("ListProperties");
        properties.MapPost("/", CreateProperty).WithName("CreateProperty");
        properties.MapGet("/{id}", GetProperty).WithName("GetProperty");
        properties.MapPut("/{id}", UpdateProperty).WithName("UpdateProperty");

        project.MapGet("/pages/{id}/properties", GetPageProperties).WithName("GetPageProperties").WithTags("Pages");
        project.MapPost("/pages/{id}/properties/preview", PreviewPageProperties).WithName("PreviewPageProperties").WithTags("Pages").RequireProjectRole(ProjectRole.Reader);
        return project;
    }

    // ─── Categories ─────────────────────────────────────────────────────────

    private static Results<Ok<CategoryListItem[]>, ProblemHttpResult> ListCategories(ElementId projectId, ProjectWorkspace workspace)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var state = service.LoadStructure([]);
        return TypedResults.Ok(state.Categories.Values
            .Select(c => new CategoryListItem(c.Id, NameOf(state, c.Id), state.Graph.ParentOf(c.Id) is { IsNone: false } p ? p : null))
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(c => c.Id.Value)
            .ToArray());
    }

    private static async Task<Results<Created<CategoryView>, ProblemHttpResult>> CreateCategory(
        ElementId projectId, CreateCategoryRequest request, ProjectWorkspace workspace, UserDirectory users, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var created = await service.CreateCategoryAsync(
            request.ParentId, request.Name, request.Index, new CategoryInput(request.Description ?? string.Empty, request.ParentCategoryId ?? ElementId.None),
            CurrentUser.Id(http), http.RequestAborted).ConfigureAwait(false);
        return created.IsSuccess
            ? TypedResults.Created($"/api/v1/projects/{projectId}/categories/{created.Value.Id}", View(service, users, created.Value.Id)!)
            : Problems.From(created.Error);
    }

    private static Results<Ok<CategoryView>, ProblemHttpResult> GetCategory(ElementId projectId, ElementId id, ProjectWorkspace workspace, UserDirectory users)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        return View(service, users, id) is { } view ? TypedResults.Ok(view) : Problems.ElementNotFound(id);
    }

    private static async Task<Results<Ok<CategoryView>, ProblemHttpResult>> UpdateCategory(
        ElementId projectId, ElementId id, UpdateCategoryRequest request, ProjectWorkspace workspace, UserDirectory users, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var updated = await service.UpdateCategoryAsync(
            id, new CategoryInput(request.Description, request.ParentCategoryId ?? ElementId.None), CurrentUser.Id(http), http.RequestAborted)
            .ConfigureAwait(false);
        return updated.IsSuccess ? TypedResults.Ok(View(service, users, id)!) : Problems.From(updated.Error);
    }

    private static Results<Ok<CategoryPageItem[]>, ProblemHttpResult> GetCategoryPages(
        ElementId projectId, ElementId id, ProjectWorkspace workspace, [FromQuery] bool includeSubCategories = false)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var state = service.LoadStructure([]);
        if (!state.Categories.ContainsKey(id))
        {
            return Problems.ElementNotFound(id);
        }

        var language = service.Project.Manifest.ContentLanguage;
        return TypedResults.Ok(service.GetCategoryPages(id, includeSubCategories)
            .Select(m => new CategoryPageItem(
                m.PageId, NameOf(state, m.PageId), [.. state.Tree.Ancestors(m.PageId).Reverse().Select(n => ProjectEndpoints.PathItemOf(n, language))],
                Summary(state, m.ViaCategoryId)))
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.Id.Value)
            .ToArray());
    }

    private static async Task<Results<Ok<CategoryView>, ProblemHttpResult>> SetCategoryValue(
        ElementId projectId, ElementId id, ElementId propertyId, CategoryValueRequest request, ProjectWorkspace workspace, UserDirectory users,
        HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var updated = await service.SetCategoryValueAsync(id, propertyId, JsonSerializer.SerializeToElement(request.Value), CurrentUser.Id(http), http.RequestAborted)
            .ConfigureAwait(false);
        return updated.IsSuccess ? TypedResults.Ok(View(service, users, id)!) : Problems.From(updated.Error);
    }

    private static async Task<Results<Ok<CategoryView>, ProblemHttpResult>> RemoveCategoryValue(
        ElementId projectId, ElementId id, ElementId propertyId, ProjectWorkspace workspace, UserDirectory users, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var updated = await service.SetCategoryValueAsync(id, propertyId, null, CurrentUser.Id(http), http.RequestAborted).ConfigureAwait(false);
        return updated.IsSuccess ? TypedResults.Ok(View(service, users, id)!) : Problems.From(updated.Error);
    }

    private static CategoryView? View(ProjectService service, UserDirectory users, ElementId id)
    {
        var state = service.LoadStructure(null);
        if (!state.Categories.TryGetValue(id, out var category) || service.GetElement(id) is not { } found)
        {
            return null;
        }

        var graph = state.Graph;
        var properties = state.PropertiesOfCategory(id).Select(definition =>
        {
            var resolution = state.ResolveCategory(id, definition);
            var source = resolution is Resolution<PropertyValue>.Inherited inherited ? Summary(state, inherited.Source) : null;
            var overrides = resolution is Resolution<PropertyValue>.Own
                && graph.Ancestors(id).Any(a => state.ValueOf(a, definition.Id) is not null);
            var origin = resolution switch
            {
                Resolution<PropertyValue>.Own => ValueOrigin.Own,
                Resolution<PropertyValue>.Inherited => ValueOrigin.Inherited,
                _ => ValueOrigin.Default,
            };
            return new CategoryPropertyItem(Definition(definition), Json(resolution.EffectiveValue), origin, source, overrides);
        }).ToArray();
        var pages = state.LivePages(null).Select(p => (p, state.CategoriesOf(p))).ToList();
        var parent = graph.ParentOf(id);
        return new CategoryView(
            ProjectEndpoints.Details(found, service, users),
            category.Description,
            parent.IsNone ? null : Summary(state, parent),
            [.. graph.Ancestors(id).Reverse().Select(a => Summary(state, a))],
            [.. graph.Children(id).Select(c => Summary(state, c)).OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)],
            properties,
            CategoryMembership.PagesOf(graph, id, false, pages).Count,
            CategoryMembership.PagesOf(graph, id, true, pages).Count);
    }

    // ─── Properties ─────────────────────────────────────────────────────────

    private static Results<Ok<PropertyDefinitionItem[]>, ProblemHttpResult> ListProperties(ElementId projectId, ProjectWorkspace workspace) =>
        workspace.Find(projectId) is { } service
            ? TypedResults.Ok(service.ListDefinitions().Select(Definition).ToArray())
            : Problems.ProjectNotOpen(projectId);

    private static async Task<Results<Created<PropertyView>, ProblemHttpResult>> CreateProperty(
        ElementId projectId, CreatePropertyRequest request, ProjectWorkspace workspace, UserDirectory users, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var input = new PropertyInput(
            request.Key, request.Description ?? string.Empty, request.Type, Element(request.Default), Element(request.Rules),
            [.. (request.Options ?? []).Select(o => (o.Key, o.Label))]);
        var created = await service.CreatePropertyAsync(request.ParentId, request.Name, request.Index, input, CurrentUser.Id(http), http.RequestAborted)
            .ConfigureAwait(false);
        return created.IsSuccess
            ? TypedResults.Created($"/api/v1/projects/{projectId}/properties/{created.Value.Id}", PropertyViewOf(service, users, created.Value.Id)!)
            : Problems.From(created.Error);
    }

    private static Results<Ok<PropertyView>, ProblemHttpResult> GetProperty(ElementId projectId, ElementId id, ProjectWorkspace workspace, UserDirectory users)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        return PropertyViewOf(service, users, id) is { } view ? TypedResults.Ok(view) : Problems.ElementNotFound(id);
    }

    private static async Task<Results<Ok<PropertyView>, ProblemHttpResult>> UpdateProperty(
        ElementId projectId, ElementId id, UpdatePropertyRequest request, ProjectWorkspace workspace, UserDirectory users, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var input = new PropertyInput(
            request.Key, request.Description, request.Type, Element(request.Default), Element(request.Rules),
            [.. request.Options.Select(o => (o.Key, o.Label))], request.Replacements);
        var updated = await service.UpdatePropertyAsync(id, input, CurrentUser.Id(http), http.RequestAborted).ConfigureAwait(false);
        return updated.IsSuccess ? TypedResults.Ok(PropertyViewOf(service, users, id)!) : Problems.From(updated.Error);
    }

    private static PropertyView? PropertyViewOf(ProjectService service, UserDirectory users, ElementId id)
    {
        var state = service.LoadStructure(null);
        if (!state.Definitions.TryGetValue(id, out var definition) || service.GetElement(id) is not { } found)
        {
            return null;
        }

        var usedBy = state.Categories.Keys.Where(c => state.ValueOf(c, id) is not null).Select(c => Summary(state, c))
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var resolved = state.LivePages(null).SelectMany(p => state.ResolvePage(p, [id])).ToList();
        var filled = resolved.Count(r => r.Origin is ValueOrigin.Own or ValueOrigin.Inherited or ValueOrigin.Conflict);
        var violations = resolved.Where(r => r.Violations.Count > 0)
            .Select(r => new PropertyViolationPage(r.PageId, NameOf(state, r.PageId), Json(r.Resolution.EffectiveValue), Violations(r.Violations)))
            .OrderBy(v => v.PageName, StringComparer.CurrentCultureIgnoreCase).ThenBy(v => v.PageId.Value)
            .ToArray();
        return new PropertyView(ProjectEndpoints.Details(found, service, users), Definition(definition), usedBy, filled, violations);
    }

    // ─── Pages ──────────────────────────────────────────────────────────────

    private static Results<Ok<PageProperties>, ProblemHttpResult> GetPageProperties(ElementId projectId, ElementId id, ProjectWorkspace workspace)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        return PagePropertiesOf(service, id) is { } properties ? TypedResults.Ok(properties) : Problems.ElementNotFound(id);
    }

    /// <summary>Resolution of a page as the editor would save it, for the live panel (nothing is written).</summary>
    private static Results<Ok<PageProperties>, ProblemHttpResult> PreviewPageProperties(
        ElementId projectId, ElementId id, PagePreviewRequest request, ProjectWorkspace workspace)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var state = service.LoadStructure([id]);
        if (state.Tree.Find(id) is not { Kind: ElementKind.Page })
        {
            return Problems.ElementNotFound(id);
        }

        var values = new List<(ElementId, PropertyValue)>();
        foreach (var item in request.Properties.DistinctBy(p => p.PropertyId))
        {
            if (state.Definitions.TryGetValue(item.PropertyId, out var definition)
                && PropertyValues.FromJson(JsonSerializer.SerializeToElement(item.Value), definition.Type, definition.Options) is { IsSuccess: true } parsed)
            {
                values.Add((item.PropertyId, parsed.Value));
            }
        }

        state.Overlay(id, request.Categories, values);
        return TypedResults.Ok(PagePropertiesOf(state, id)!);
    }

    /// <summary>Categories and resolved properties of a live page; null otherwise.</summary>
    internal static PageProperties? PagePropertiesOf(ProjectService service, ElementId pageId) =>
        PagePropertiesOf(service.LoadStructure([pageId]), pageId);

    private static PageProperties? PagePropertiesOf(StructureState state, ElementId pageId)
    {
        if (state.Tree.Find(pageId) is not { Kind: ElementKind.Page })
        {
            return null;
        }

        return new PageProperties(
            [.. state.CategoriesOf(pageId).Select(c => Summary(state, c))],
            [.. state.ResolvePage(pageId).Select(r => new PagePropertyItem(
                Definition(r.Definition),
                Json(r.OwnValue),
                Json(r.Resolution.EffectiveValue),
                r.Origin,
                SourcesOf(state, r.Resolution),
                CandidatesOf(state, r.Resolution),
                Violations(r.Violations),
                r.Fallback is { } fallback
                    ? new FallbackItem(Json(fallback.EffectiveValue), OriginOf(fallback), SourcesOf(state, fallback), CandidatesOf(state, fallback))
                    : null))]);
    }

    private static ValueOrigin OriginOf(Resolution<PropertyValue> resolution) => resolution switch
    {
        Resolution<PropertyValue>.Own => ValueOrigin.Own,
        Resolution<PropertyValue>.Inherited => ValueOrigin.Inherited,
        Resolution<PropertyValue>.FromDefinition => ValueOrigin.Default,
        _ => ValueOrigin.Conflict,
    };

    private static CategorySummary[] SourcesOf(StructureState state, Resolution<PropertyValue> resolution) =>
        resolution is Resolution<PropertyValue>.Inherited inherited ? [.. inherited.Sources.Select(s => Summary(state, s))] : [];

    private static ConflictCandidateItem[] CandidatesOf(StructureState state, Resolution<PropertyValue> resolution) =>
        resolution is Resolution<PropertyValue>.Conflict conflict
            ? [.. conflict.Candidates.Select(c => new ConflictCandidateItem(c.CategoryId, NameOf(state, c.CategoryId), PropertyValues.ToJson(c.Value)))]
            : [];

    // ─── Mapping ────────────────────────────────────────────────────────────

    internal static PropertyDefinitionItem Definition(PropertyDefinition d) => new(
        d.Id, d.Name, d.Key, d.Description, d.Type.ToString(), Json(d.Default), ValidationRules.ListToJson(d.Rules),
        [.. d.Options.Select(o => new EnumOptionItem(o.Key, o.Label))]);

    private static ViolationItem[] Violations(IEnumerable<RuleViolation> violations) =>
        [.. violations.Select(v => new ViolationItem(ValidationRules.ToJson(v.Rule), v.Index))];

    private static JsonNode? Json(PropertyValue? value) => value is null ? null : PropertyValues.ToJson(value);

    private static JsonElement? Element(JsonNode? node) => node is null ? null : JsonSerializer.SerializeToElement(node);

    private static string NameOf(StructureState state, ElementId id) => state.Tree.Find(id)?.Name ?? id.ToString();

    private static CategorySummary Summary(StructureState state, ElementId id) => new(id, NameOf(state, id));
}
