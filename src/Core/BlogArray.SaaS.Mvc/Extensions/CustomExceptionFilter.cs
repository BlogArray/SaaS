using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace BlogArray.SaaS.Mvc.Extensions;

public class CustomExceptionFilter(IModelMetadataProvider modelMetadataProvider) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        ViewResult result = new()
        {
            ViewName = "CustomError",
            ViewData = new ViewDataDictionary(modelMetadataProvider, context.ModelState)
        };

        result.ViewData.Add("Exception", context.Exception);

        context.ExceptionHandled = true;
        context.Result = result;
    }
}
