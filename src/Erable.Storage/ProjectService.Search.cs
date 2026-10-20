using Erable.Core;
using Erable.Core.Categories;
using Erable.Core.Search;

namespace Erable.Storage;

public sealed partial class ProjectService
{
    // ─── Search (F10) ────────────────────────────────────────────────

    /// <summary>
    /// Builds the condition « <paramref name="propertyId"/> <paramref name="op"/> <paramref name="raw"/> »: the operand is
    /// read according to the type of the property.
    /// </summary>
    public Result<PropertyCondition> CreateCondition(ElementId propertyId, ConditionOp op, string? raw = null)
    {
        using var connection = Project.Database.OpenConnection();
        return StructureRepository.GetDefinition(connection, propertyId) is { } definition && Repository.LoadTree(connection).Contains(propertyId)
            ? PropertyCondition.Create(propertyId, definition.Type.Scalar, op, raw)
            : new DomainError(ErrorCodes.PropertyNotFound, $"Property {propertyId} does not exist.");
    }

    /// <summary>
    /// Runs a search: full text and filters, one consistent read of the project. Categories with
    /// <see cref="SearchFilter.IncludeSubCategories"/> stand for themselves and everything that inherits from them.
    /// </summary>
    public SearchResults Search(SearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var connection = Project.Database.OpenConnection();
        var filter = request.Filter;
        if (filter.Categories.Count > 0 && filter.IncludeSubCategories)
        {
            var tree = Repository.LoadTree(connection);
            var graph = CategoryGraph.Load(StructureRepository.LoadCategories(connection)
                .Where(c => tree.Contains(c.Id)).Select(c => new CategoryNode(c.Id, c.ParentCategoryId)));
            filter = filter with
            {
                Categories = [.. filter.Categories.SelectMany(c => graph.Descendants(c).Prepend(c)).Distinct()],
            };
        }

        return SearchRepository.Search(Repository, connection, request with
        {
            Filter = filter,
            Limit = Math.Clamp(request.Limit, 1, SearchRequest.MaxLimit),
            Offset = Math.Max(request.Offset, 0),
        });
    }
}
