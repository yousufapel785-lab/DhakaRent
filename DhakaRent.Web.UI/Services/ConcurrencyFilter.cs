using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Services;

public class ConcurrencyFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is DbUpdateConcurrencyException)
        {
            context.Result = new ConflictObjectResult("This record changed while you were editing it. Reload the page and check its current values.");
            context.ExceptionHandled = true;
        }
    }
}
