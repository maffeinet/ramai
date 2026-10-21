using System.Data.Common;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace Ramai.Modules.Tenants.Persistence;

internal sealed class TenantQueryBoundary : ExpressionVisitor, IQueryExpressionInterceptor
{
    internal static readonly TenantQueryBoundary Instance = new();
    public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData) => Visit(queryExpression);
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
            && node.Method.Name == nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters))
        { throw new InvalidOperationException("Disabling tenant query filters is not permitted."); }
        return base.VisitMethodCall(node);
    }
    protected override Expression VisitExtension(Expression node)
    {
        // Avoid dependencies on EF internal CLR types; command-source policy independently rejects raw SQL.
        if (node is QueryRootExpression && node.GetType().Name is "FromSqlQueryRootExpression" or "SqlQueryRootExpression")
        { throw new InvalidOperationException("Raw SQL is not permitted in tenant operations."); }
        return base.VisitExtension(node);
    }
}

internal sealed class TenantCommandBoundary : DbCommandInterceptor
{
    internal static readonly TenantCommandBoundary Instance = new();
    private static void Check(CommandEventData data)
    {
        if (data.CommandSource is not (CommandSource.LinqQuery or CommandSource.SaveChanges))
        { throw new InvalidOperationException("Command source is not permitted in tenant operations."); }
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    { Check(eventData); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { Check(eventData); return ValueTask.FromResult(result); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result)
    { Check(eventData); return result; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Check(eventData); return ValueTask.FromResult(result); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result)
    { Check(eventData); return result; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result, CancellationToken cancellationToken = default)
    { Check(eventData); return ValueTask.FromResult(result); }
}
